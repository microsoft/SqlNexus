using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Store = global::SqlNexus.McpServer.CredentialStore;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class CredentialStoreTests
    {
        private const string Server = "localhost\\SQLEXPRESS";
        private const string User = "sqlnexus_reader";
        private static readonly string Password = TestSecrets.NewPassword();

        private string root;
        private string directory;

        [TestInitialize]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "SqlNexusCredentialStoreTests_" + Guid.NewGuid().ToString("N"));
            directory = Path.Combine(root, "McpCredentials");
        }

        [TestCleanup]
        public void TearDown()
        {
            // Remove the junction itself first; a recursive delete would try to traverse it.
            string link = Path.Combine(root, "McpCredentialsLink");
            if (Directory.Exists(link))
            {
                Directory.Delete(link, false);
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void SaveThenTryLoad_ValidCredential_RoundTrips()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);

            bool loaded = Store.TryLoad(Server, User, out string password, out string error, directory);

            Assert.IsTrue(loaded, error);
            Assert.AreEqual(Password, password);
            Assert.AreEqual(string.Empty, error);
        }

        [TestMethod]
        public void SaveThenTryLoad_NonAsciiPassword_RoundTrips()
        {
            string unicodePassword = TestSecrets.NewPassword() + "-pässwörd-日本語-🔑";

            Store.Save(Server, User, unicodePassword.ToCharArray(), directory);

            Assert.IsTrue(Store.TryLoad(Server, User, out string password, out string error, directory), error);
            Assert.AreEqual(unicodePassword, password);
        }

        [TestMethod]
        public void Save_Credential_DoesNotWritePlaintextToDisk()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);

            byte[] content = File.ReadAllBytes(Store.GetCredentialPath(Server, User, directory));

            Assert.IsFalse(Contains(content, Encoding.UTF8.GetBytes(Password)));
            Assert.IsFalse(Contains(content, Encoding.Unicode.GetBytes(Password)));
            Assert.IsFalse(Contains(content, Encoding.ASCII.GetBytes(Password)));
        }

        [TestMethod]
        public void Save_ExistingCredential_ReplacesPasswordAndLeavesNoTemporaryFiles()
        {
            string first = TestSecrets.NewPassword();
            string second = TestSecrets.NewPassword();
            Store.Save(Server, User, first.ToCharArray(), directory);
            Store.Save(Server, User, second.ToCharArray(), directory);

            Assert.IsTrue(Store.TryLoad(Server, User, out string password, out string error, directory), error);
            Assert.AreEqual(second, password);
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
            Assert.AreEqual(1, Directory.GetFiles(directory).Length);
        }

        [TestMethod]
        public void TryLoad_MissingCredential_FailsWithActionableMessageAndNoPassword()
        {
            bool loaded = Store.TryLoad(Server, User, out string password, out string error, directory);

            Assert.IsFalse(loaded);
            Assert.AreEqual(string.Empty, password);
            StringAssert.Contains(error, "--set-credential");
            StringAssert.Contains(error, Server);
            StringAssert.Contains(error, User);
        }

        [TestMethod]
        public void TryLoad_TamperedBlob_FailsClosed()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);
            string path = Store.GetCredentialPath(Server, User, directory);
            byte[] content = File.ReadAllBytes(path);
            content[content.Length - 1] ^= 0xFF;
            File.WriteAllBytes(path, content);

            bool loaded = Store.TryLoad(Server, User, out string password, out string error, directory);

            Assert.IsFalse(loaded);
            Assert.AreEqual(string.Empty, password);
            StringAssert.Contains(error, "could not be decrypted");
            Assert.IsFalse(error.Contains(Password));
        }

        [TestMethod]
        public void TryLoad_TruncatedFile_FailsClosed()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);
            string path = Store.GetCredentialPath(Server, User, directory);
            File.WriteAllBytes(path, File.ReadAllBytes(path).Take(3).ToArray());

            Assert.IsFalse(Store.TryLoad(Server, User, out string password, out string error, directory));
            Assert.AreEqual(string.Empty, password);
            StringAssert.Contains(error, "not valid");
        }

        [TestMethod]
        public void TryLoad_WrongMagic_FailsClosed()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);
            string path = Store.GetCredentialPath(Server, User, directory);
            byte[] content = File.ReadAllBytes(path);
            content[0] = (byte)'X';
            File.WriteAllBytes(path, content);

            Assert.IsFalse(Store.TryLoad(Server, User, out string password, out string error, directory));
            Assert.AreEqual(string.Empty, password);
            StringAssert.Contains(error, "not valid");
        }

        [TestMethod]
        public void TryLoad_UnsupportedVersion_FailsClosed()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);
            string path = Store.GetCredentialPath(Server, User, directory);
            byte[] content = File.ReadAllBytes(path);
            content[4] = 99;
            File.WriteAllBytes(path, content);

            Assert.IsFalse(Store.TryLoad(Server, User, out string password, out string error, directory));
            Assert.AreEqual(string.Empty, password);
            StringAssert.Contains(error, "not valid");
        }

        [TestMethod]
        public void TryLoad_OversizedFile_IsRejected()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);
            string path = Store.GetCredentialPath(Server, User, directory);
            File.WriteAllBytes(path, new byte[Store.MaxFileBytes + 1]);

            Assert.IsFalse(Store.TryLoad(Server, User, out string password, out string error, directory));
            Assert.AreEqual(string.Empty, password);
            StringAssert.Contains(error, "not valid");
        }

        [TestMethod]
        public void TryLoad_BlobCopiedToAnotherServerUser_FailsBecauseEntropyDiffers()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);
            string source = Store.GetCredentialPath(Server, User, directory);
            string other = Store.GetCredentialPath("otherserver", "otheruser", directory);
            File.Copy(source, other);

            bool loaded = Store.TryLoad("otherserver", "otheruser", out string password, out string error, directory);

            Assert.IsFalse(loaded);
            Assert.AreEqual(string.Empty, password);
            StringAssert.Contains(error, "could not be decrypted");
        }

        [TestMethod]
        public void Save_EmptyOrNullPassword_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => Store.Save(Server, User, null, directory));
            Assert.ThrowsException<ArgumentException>(() => Store.Save(Server, User, new char[0], directory));
            Assert.IsFalse(Directory.Exists(directory));
        }

        [TestMethod]
        public void Save_PasswordWithLineBreakOrNull_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => Store.Save(Server, User, "ab\ncd".ToCharArray(), directory));
            Assert.ThrowsException<ArgumentException>(() => Store.Save(Server, User, "ab\rcd".ToCharArray(), directory));
            Assert.ThrowsException<ArgumentException>(() => Store.Save(Server, User, "ab\0cd".ToCharArray(), directory));
        }

        [TestMethod]
        public void Save_PasswordLongerThanLimit_Throws()
        {
            var tooLong = new string('a', Store.MaxPasswordLength + 1).ToCharArray();

            Assert.ThrowsException<ArgumentException>(() => Store.Save(Server, User, tooLong, directory));
        }

        [TestMethod]
        public void Save_PasswordAtLimit_RoundTrips()
        {
            string atLimit = new string('a', Store.MaxPasswordLength);

            Store.Save(Server, User, atLimit.ToCharArray(), directory);

            Assert.IsTrue(Store.TryLoad(Server, User, out string password, out string error, directory), error);
            Assert.AreEqual(atLimit, password);
        }

        [TestMethod]
        public void Save_InvalidIdentifiers_Throw()
        {
            char[] pw = Password.ToCharArray();

            Assert.ThrowsException<ArgumentException>(() => Store.Save(null, User, pw, directory));
            Assert.ThrowsException<ArgumentException>(() => Store.Save("  ", User, pw, directory));
            Assert.ThrowsException<ArgumentException>(() => Store.Save(Server, "", pw, directory));
            Assert.ThrowsException<ArgumentException>(() => Store.Save("srv\r\nname", User, pw, directory));
            Assert.ThrowsException<ArgumentException>(() => Store.Save(Server, new string('u', Store.MaxIdentifierLength + 1), pw, directory));
        }

        [TestMethod]
        public void TryLoad_InvalidIdentifier_FailsWithoutThrowing()
        {
            Assert.IsFalse(Store.TryLoad("", User, out string password, out string error, directory));
            Assert.AreEqual(string.Empty, password);
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        [TestMethod]
        public void BuildIdentity_DelimiterAmbiguousPairs_DoNotCollide()
        {
            string first = Store.BuildIdentity("a|b,c", "x");
            string second = Store.BuildIdentity("a", "b,c|x");
            string third = Store.BuildIdentity("a|b", "c");
            string fourth = Store.BuildIdentity("a", "b|c");

            Assert.AreNotEqual(first, second);
            Assert.AreNotEqual(third, fourth);
            Assert.AreNotEqual(Store.GetCredentialPath("a|b", "c", directory), Store.GetCredentialPath("a", "b|c", directory));
        }

        [TestMethod]
        public void GetCredentialPath_CaseAndSurroundingWhitespace_MapToSameFile()
        {
            string baseline = Store.GetCredentialPath("MyServer", "SA_Reader", directory);

            Assert.AreEqual(baseline, Store.GetCredentialPath("  myserver ", "sa_reader", directory));
            Assert.AreEqual(baseline, Store.GetCredentialPath("MYSERVER", "SA_READER", directory));
        }

        [TestMethod]
        public void GetCredentialPath_AliasesPortsAndInstances_AreNotTreatedAsEquivalent()
        {
            string baseline = Store.GetCredentialPath("localhost", User, directory);

            Assert.AreNotEqual(baseline, Store.GetCredentialPath(".", User, directory));
            Assert.AreNotEqual(baseline, Store.GetCredentialPath("localhost,1433", User, directory));
            Assert.AreNotEqual(baseline, Store.GetCredentialPath("localhost\\SQLEXPRESS", User, directory));
            Assert.AreNotEqual(baseline, Store.GetCredentialPath("localhost", "other_user", directory));
        }

        [TestMethod]
        public void GetCredentialPath_TraversalSequencesInIdentifiers_StayInsideBaseDirectory()
        {
            string path = Store.GetCredentialPath("..\\..\\evil\\server", "..\\user:name", directory);

            Assert.AreEqual(Path.GetFullPath(directory), Path.GetFullPath(Path.GetDirectoryName(path)));
            StringAssert.Matches(Path.GetFileName(path), new System.Text.RegularExpressions.Regex("^[0-9a-f]{64}\\.bin$"));
        }

        [TestMethod]
        public void GetDefaultDirectory_IsUnderLocalApplicationData()
        {
            string expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string defaultDirectory = Store.GetDefaultDirectory();

            StringAssert.StartsWith(defaultDirectory, expectedRoot);
            StringAssert.EndsWith(defaultDirectory, Path.Combine("SqlNexus", "McpCredentials"));
        }

        [TestMethod]
        public void Delete_ExistingCredential_RemovesFileAndReturnsTrue()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);

            Assert.IsTrue(Store.Delete(Server, User, directory));

            Assert.IsFalse(File.Exists(Store.GetCredentialPath(Server, User, directory)));
            Assert.IsFalse(Store.TryLoad(Server, User, out _, out _, directory));
        }

        [TestMethod]
        public void Delete_MissingCredentialOrDirectory_ReturnsFalse()
        {
            Assert.IsFalse(Store.Delete(Server, User, directory));

            Store.Save("other", "other", Password.ToCharArray(), directory);
            Assert.IsFalse(Store.Delete(Server, User, directory));
        }

        [TestMethod]
        public void Delete_OnlyRemovesTheRequestedCredential()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);
            Store.Save(Server, "second_user", Password.ToCharArray(), directory);

            Assert.IsTrue(Store.Delete(Server, User, directory));

            Assert.IsTrue(Store.TryLoad(Server, "second_user", out _, out string error, directory), error);
        }

        [TestMethod]
        public void Save_CreatedFileAndDirectory_AreRestrictedToCurrentUser()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);
            SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User;

            DirectorySecurity directorySecurity = new DirectoryInfo(directory).GetAccessControl();
            FileSecurity fileSecurity = new FileInfo(Store.GetCredentialPath(Server, User, directory)).GetAccessControl();

            Assert.IsTrue(directorySecurity.AreAccessRulesProtected);
            Assert.IsTrue(fileSecurity.AreAccessRulesProtected);
            foreach (AuthorizationRule rule in directorySecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                Assert.AreEqual(currentUser, rule.IdentityReference);
            }
            foreach (AuthorizationRule rule in fileSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                Assert.AreEqual(currentUser, rule.IdentityReference);
            }
        }

        [TestMethod]
        public void Save_DirectoryIsReparsePoint_IsRejectedAndWritesNothingThroughIt()
        {
            string target = Path.Combine(root, "redirect-target");
            Directory.CreateDirectory(target);
            string link = Path.Combine(root, "McpCredentialsLink");
            if (!TryCreateJunction(link, target))
            {
                Assert.Inconclusive("A directory junction could not be created in this environment.");
            }

            Assert.ThrowsException<IOException>(() => Store.Save(Server, User, Password.ToCharArray(), link));

            Assert.AreEqual(0, Directory.GetFileSystemEntries(target).Length);
        }

        [TestMethod]
        public void TryLoad_DirectoryIsReparsePoint_IsNotUsed()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);
            string link = Path.Combine(root, "McpCredentialsLink");
            if (!TryCreateJunction(link, directory))
            {
                Assert.Inconclusive("A directory junction could not be created in this environment.");
            }

            bool loaded = Store.TryLoad(Server, User, out string password, out string error, link);

            Assert.IsFalse(loaded);
            Assert.AreEqual(string.Empty, password);
            StringAssert.Contains(error, "not trusted");
        }

        [TestMethod]
        public void Save_Credential_DoesNotModifyCallerPasswordArray()
        {
            char[] pw = Password.ToCharArray();

            Store.Save(Server, User, pw, directory);

            // The caller owns (and clears) the array; the store only reads it.
            CollectionAssert.AreEqual(Password.ToCharArray(), pw);
        }

        private static bool TryCreateJunction(string link, string target)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec"),
                Arguments = "/c mklink /J \"" + link + "\" \"" + target + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(startInfo))
            {
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 && Directory.Exists(link);
            }
        }

        private static bool Contains(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length && match; j++)
                {
                    match = haystack[i + j] == needle[j];
                }

                if (match)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
