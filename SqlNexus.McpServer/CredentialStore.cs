#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace SqlNexus.McpServer
{
    /// <summary>
    /// Stores the SQL authentication password for an MCP server connection in a Windows DPAPI
    /// (CurrentUser scope) protected file, so no plaintext password is kept in configuration.
    ///
    /// The file location is derived from the server and user (never supplied by the caller), so the
    /// credential cannot be redirected with a crafted path. The derived identity is also used as DPAPI
    /// entropy, which binds a blob to its server/user pair. Neither the file name, the entropy nor the
    /// file format is secret: protection depends on the DPAPI key of the Windows user.
    /// </summary>
    internal static class CredentialStore
    {
        internal const int MaxIdentifierLength = 256;
        internal const int MaxPasswordLength = 128;
        internal const int MaxFileBytes = 8192;

        private const byte FormatVersion = 1;
        private static readonly byte[] Magic = { (byte)'S', (byte)'N', (byte)'X', (byte)'C' };
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static string GetDefaultDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
                throw new InvalidOperationException("The local application data folder could not be determined.");

            return Path.Combine(localAppData, "SqlNexus", "McpCredentials");
        }

        /// <summary>
        /// Builds an unambiguous identity for a server/user pair. Values are trimmed and lower-cased
        /// (invariant); aliases, ports and instance names are deliberately NOT treated as equivalent,
        /// so the credential is keyed by exactly what is passed in --server. Each value is
        /// length-prefixed so different pairs can never produce the same identity.
        /// </summary>
        internal static string BuildIdentity(string? server, string? userId)
        {
            string normalizedServer = Normalize(server, "server");
            string normalizedUser = Normalize(userId, "user");
            return "SqlNexus.McpServer.Credential.v1|"
                + normalizedServer.Length.ToString(CultureInfo.InvariantCulture) + ":" + normalizedServer + "|"
                + normalizedUser.Length.ToString(CultureInfo.InvariantCulture) + ":" + normalizedUser;
        }

        internal static string GetCredentialPath(string? server, string? userId, string? baseDirectory = null)
        {
            string identity = BuildIdentity(server, userId);
            byte[] hash;
            using (SHA256 sha = SHA256.Create())
            {
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
            }

            var name = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                name.Append(b.ToString("x2", CultureInfo.InvariantCulture));

            return Path.Combine(baseDirectory ?? GetDefaultDirectory(), name + ".bin");
        }

        internal static void Save(string? server, string? userId, char[]? password, string? baseDirectory = null)
        {
            if (password == null || password.Length == 0)
                throw new ArgumentException("The password must not be empty.");
            if (password.Length > MaxPasswordLength)
                throw new ArgumentException("The password must not be longer than " + MaxPasswordLength.ToString(CultureInfo.InvariantCulture) + " characters.");
            foreach (char c in password)
            {
                if (c == '\0' || c == '\r' || c == '\n')
                    throw new ArgumentException("The password must not contain null or line-break characters.");
            }

            string identity = BuildIdentity(server, userId);
            string path = GetCredentialPath(server, userId, baseDirectory);
            string directory = Path.GetDirectoryName(path)!;

            byte[]? plain = null;
            byte[]? blob = null;
            try
            {
                plain = Encoding.UTF8.GetBytes(password);
                blob = ProtectedData.Protect(plain, Encoding.UTF8.GetBytes(identity), DataProtectionScope.CurrentUser);
                EnsureSecureDirectory(directory);
                WriteFile(directory, path, blob);
            }
            finally
            {
                if (plain != null) Array.Clear(plain, 0, plain.Length);
                if (blob != null) Array.Clear(blob, 0, blob.Length);
            }
        }

        /// <summary>
        /// Loads and decrypts a stored credential. Failure messages contain only the server and user
        /// identifiers, never the password, the protected blob or exception data.
        /// </summary>
        internal static bool TryLoad(string? server, string? userId, out string password, out string error, string? baseDirectory = null)
        {
            password = string.Empty;
            error = string.Empty;

            string identity;
            string path;
            try
            {
                identity = BuildIdentity(server, userId);
                path = GetCredentialPath(server, userId, baseDirectory);
            }
            catch (ArgumentException ex)
            {
                error = ex.Message;
                return false;
            }

            string label = Describe(server, userId);
            string directory = Path.GetDirectoryName(path)!;
            byte[]? data = null;
            byte[]? plain = null;
            try
            {
                var file = new FileInfo(path);
                if (!Directory.Exists(directory) || !file.Exists)
                {
                    error = "No stored SQL credential exists for " + label + ". Create one with: SqlNexus.McpServer.exe --set-credential --server \""
                        + (server ?? string.Empty).Trim() + "\" --user-id \"" + (userId ?? string.Empty).Trim() + "\"";
                    return false;
                }

                if (IsReparsePoint(new DirectoryInfo(directory).Attributes) || IsReparsePoint(file.Attributes) || !IsTrustedOwner(new DirectoryInfo(directory)))
                {
                    error = "The stored SQL credential for " + label + " is in a location that is not trusted and was not used.";
                    return false;
                }

                if (file.Length <= Magic.Length + 1 || file.Length > MaxFileBytes)
                {
                    error = "The stored SQL credential for " + label + " is not valid. Recreate it with --set-credential.";
                    return false;
                }

                data = File.ReadAllBytes(path);
                if (!HasValidHeader(data))
                {
                    error = "The stored SQL credential for " + label + " is not valid. Recreate it with --set-credential.";
                    return false;
                }

                var blob = new byte[data.Length - Magic.Length - 1];
                Buffer.BlockCopy(data, Magic.Length + 1, blob, 0, blob.Length);
                plain = ProtectedData.Unprotect(blob, Encoding.UTF8.GetBytes(identity), DataProtectionScope.CurrentUser);
                password = StrictUtf8.GetString(plain);
                if (password.Length == 0)
                {
                    error = "The stored SQL credential for " + label + " is empty. Recreate it with --set-credential.";
                    return false;
                }

                return true;
            }
            catch (CryptographicException)
            {
                password = string.Empty;
                error = "The stored SQL credential for " + label + " could not be decrypted. It was created by a different Windows user or on a different machine, or it is damaged. Recreate it with --set-credential.";
                return false;
            }
            catch (DecoderFallbackException)
            {
                password = string.Empty;
                error = "The stored SQL credential for " + label + " is not valid. Recreate it with --set-credential.";
                return false;
            }
            catch (IOException)
            {
                error = "The stored SQL credential for " + label + " could not be read.";
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                error = "Access to the stored SQL credential for " + label + " was denied.";
                return false;
            }
            finally
            {
                if (data != null) Array.Clear(data, 0, data.Length);
                if (plain != null) Array.Clear(plain, 0, plain.Length);
            }
        }

        /// <returns>True when a credential file was deleted; false when none existed.</returns>
        internal static bool Delete(string? server, string? userId, string? baseDirectory = null)
        {
            string path = GetCredentialPath(server, userId, baseDirectory);
            string directory = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(directory))
                return false;
            if (IsReparsePoint(new DirectoryInfo(directory).Attributes))
                throw new IOException("The credential folder is a reparse point and was not used.");
            if (!File.Exists(path))
                return false;

            File.Delete(path);
            return true;
        }

        internal static string Describe(string? server, string? userId)
        {
            return "server '" + (server ?? string.Empty).Trim() + "' and user '" + (userId ?? string.Empty).Trim() + "'";
        }

        private static string Normalize(string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("The " + label + " must not be empty.");

            string trimmed = value!.Trim();
            if (trimmed.Length > MaxIdentifierLength)
                throw new ArgumentException("The " + label + " must not be longer than " + MaxIdentifierLength.ToString(CultureInfo.InvariantCulture) + " characters.");
            foreach (char c in trimmed)
            {
                if (char.IsControl(c))
                    throw new ArgumentException("The " + label + " must not contain control characters.");
            }

            return trimmed.ToLowerInvariant();
        }

        private static bool HasValidHeader(byte[] data)
        {
            if (data.Length <= Magic.Length + 1)
                return false;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                    return false;
            }

            return data[Magic.Length] == FormatVersion;
        }

        private static bool IsReparsePoint(FileAttributes attributes)
        {
            return (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }

        private static SecurityIdentifier CurrentUserSid()
        {
            return WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("The current Windows user could not be determined.");
        }

        // The owner must be the current user or the local Administrators group (the default owner of
        // objects created by an elevated process). Anything else means someone else controls the folder.
        private static bool IsTrustedOwner(DirectoryInfo directory)
        {
            var owner = directory.GetAccessControl().GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (owner == null)
                return false;

            return owner.Equals(CurrentUserSid())
                || owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
        }

        private static DirectorySecurity BuildDirectorySecurity()
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(
                CurrentUserSid(),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            return security;
        }

        private static FileSecurity BuildFileSecurity()
        {
            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(CurrentUserSid(), FileSystemRights.FullControl, AccessControlType.Allow));
            return security;
        }

        private static void EnsureSecureDirectory(string directory)
        {
            string? parent = Path.GetDirectoryName(directory);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
                if (IsReparsePoint(new DirectoryInfo(parent).Attributes))
                    throw new IOException("The credential parent folder is a reparse point and was not used.");
            }

            var info = new DirectoryInfo(directory);
            if (!info.Exists)
            {
                Directory.CreateDirectory(directory, BuildDirectorySecurity());
                return;
            }

            if (IsReparsePoint(info.Attributes))
                throw new IOException("The credential folder is a reparse point and was not used.");
            if (!IsTrustedOwner(info))
                throw new IOException("The credential folder is owned by another account and was not used.");

            info.SetAccessControl(BuildDirectorySecurity());
        }

        private static void WriteFile(string directory, string path, byte[] blob)
        {
            var target = new FileInfo(path);
            if (target.Exists && (IsReparsePoint(target.Attributes) || (target.Attributes & FileAttributes.Directory) != 0))
                throw new IOException("The credential file location is not a regular file and was not used.");

            string temporaryPath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(
                    temporaryPath, FileMode.CreateNew, FileSystemRights.Write, FileShare.None, 4096, FileOptions.WriteThrough, BuildFileSecurity()))
                {
                    stream.Write(Magic, 0, Magic.Length);
                    stream.WriteByte(FormatVersion);
                    stream.Write(blob, 0, blob.Length);
                    stream.Flush(true);
                }

                if (target.Exists)
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);

                new FileInfo(path).SetAccessControl(BuildFileSecurity());
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }
}
