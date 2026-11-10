#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace SqlNexus.McpServer
{
    /// <summary>
    /// Command-line modes that manage the DPAPI-protected SQL credential file. They run instead of
    /// the MCP protocol loop and never log or print a password.
    ///
    ///   --set-credential    --server S --user-id U [--password-stdin]
    ///   --check-credential  --server S --user-id U
    ///   --delete-credential --server S --user-id U
    ///
    /// --set-credential prompts for a masked password on an interactive console. --password-stdin
    /// reads a single line from redirected standard input so the registration script can supply a
    /// password without placing it on a command line or in the environment.
    /// </summary>
    internal static class CredentialCommands
    {
        internal const string SetFlag = "--set-credential";
        internal const string CheckFlag = "--check-credential";
        internal const string DeleteFlag = "--delete-credential";
        internal const string PasswordStdinFlag = "--password-stdin";

        internal const int ExitSuccess = 0;
        internal const int ExitFailure = 1;
        internal const int ExitUsage = 2;

        private static readonly string[] CommandFlags = { SetFlag, CheckFlag, DeleteFlag };

        internal static bool IsCredentialCommand(string[] args)
        {
            return args.Any(a => CommandFlags.Any(f => string.Equals(a, f, StringComparison.OrdinalIgnoreCase)));
        }

        /// <param name="readSecretInteractively">Reads a masked line from the console; null when cancelled.</param>
        internal static int Execute(
            string[] args,
            TextReader input,
            bool inputRedirected,
            TextWriter output,
            TextWriter error,
            Func<char[]?> readSecretInteractively,
            string? baseDirectory = null)
        {
            string? command = null;
            string? server = null;
            string? userId = null;
            bool passwordFromStdin = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (CommandFlags.Any(f => string.Equals(arg, f, StringComparison.OrdinalIgnoreCase)))
                {
                    if (command != null)
                        return Usage(error, "Specify only one of " + string.Join(", ", CommandFlags) + ".");
                    command = arg.ToLowerInvariant();
                }
                else if (string.Equals(arg, PasswordStdinFlag, StringComparison.OrdinalIgnoreCase))
                {
                    passwordFromStdin = true;
                }
                else if (string.Equals(arg, "--server", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    server = args[++i];
                }
                else if (string.Equals(arg, "--user-id", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    userId = args[++i];
                }
                else
                {
                    return Usage(error, "Unrecognized or incomplete argument at position " + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " for a credential command.");
                }
            }

            if (command == null)
                return Usage(error, "No credential command was specified.");
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(userId))
                return Usage(error, command + " requires --server and --user-id.");
            if (passwordFromStdin && command != SetFlag)
                return Usage(error, PasswordStdinFlag + " is only valid with " + SetFlag + ".");

            string label = CredentialStore.Describe(server, userId);
            try
            {
                switch (command)
                {
                    case SetFlag:
                        return ExecuteSet(server!, userId!, label, passwordFromStdin, input, inputRedirected, output, error, readSecretInteractively, baseDirectory);

                    case CheckFlag:
                        if (CredentialStore.TryLoad(server, userId, out _, out string loadError, baseDirectory))
                        {
                            output.WriteLine("A stored SQL credential exists for " + label + " and can be decrypted.");
                            return ExitSuccess;
                        }

                        error.WriteLine(loadError);
                        return ExitFailure;

                    default:
                        output.WriteLine(CredentialStore.Delete(server, userId, baseDirectory)
                            ? "Deleted the stored SQL credential for " + label + "."
                            : "No stored SQL credential exists for " + label + ".");
                        return ExitSuccess;
                }
            }
            catch (ArgumentException ex)
            {
                error.WriteLine(ex.Message);
                return ExitUsage;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is CryptographicException || ex is InvalidOperationException)
            {
                // Messages from these exceptions describe the failing operation; none contains the password.
                error.WriteLine("The credential operation failed for " + label + ": " + ex.Message);
                return ExitFailure;
            }
        }

        private static int ExecuteSet(
            string server,
            string userId,
            string label,
            bool passwordFromStdin,
            TextReader input,
            bool inputRedirected,
            TextWriter output,
            TextWriter error,
            Func<char[]?> readSecretInteractively,
            string? baseDirectory)
        {
            char[]? password = null;
            try
            {
                if (passwordFromStdin)
                {
                    if (!inputRedirected)
                        return Usage(error, PasswordStdinFlag + " requires redirected standard input.");

                    // Bounded read (limit + a trailing line break). Exactly one trailing line break is
                    // removed; any other CR/LF is left in place so Save rejects it instead of the
                    // password being silently truncated at the first line break.
                    var buffer = new char[CredentialStore.MaxPasswordLength + 3];
                    int count = input.ReadBlock(buffer, 0, buffer.Length);
                    if (count >= 2 && buffer[count - 2] == '\r' && buffer[count - 1] == '\n')
                        count -= 2;
                    else if (count >= 1 && buffer[count - 1] == '\n')
                        count -= 1;
                    password = count == 0 ? null : buffer.Take(count).ToArray();
                    Array.Clear(buffer, 0, buffer.Length);
                }
                else
                {
                    if (inputRedirected)
                        return Usage(error, SetFlag + " needs an interactive console. Use " + PasswordStdinFlag + " to supply the password through standard input.");

                    output.Write("Enter the SQL password for " + label + ": ");
                    password = readSecretInteractively();
                    output.WriteLine();
                    if (password != null && password.Length > 0)
                    {
                        output.Write("Confirm the password: ");
                        char[]? confirmation = readSecretInteractively();
                        output.WriteLine();
                        bool matches = confirmation != null && confirmation.SequenceEqual(password);
                        if (confirmation != null) Array.Clear(confirmation, 0, confirmation.Length);
                        if (!matches)
                        {
                            error.WriteLine("The passwords did not match. No credential was stored.");
                            return ExitFailure;
                        }
                    }
                }

                if (password == null || password.Length == 0)
                {
                    error.WriteLine("No password was provided. No credential was stored.");
                    return ExitFailure;
                }

                CredentialStore.Save(server, userId, password, baseDirectory);
                output.WriteLine("Stored the SQL credential for " + label + ".");
                return ExitSuccess;
            }
            finally
            {
                if (password != null) Array.Clear(password, 0, password.Length);
            }
        }

        private static int Usage(TextWriter error, string message)
        {
            error.WriteLine(message);
            error.WriteLine("Usage: SqlNexus.McpServer.exe " + SetFlag + " | " + CheckFlag + " | " + DeleteFlag + " --server <server> --user-id <user> [" + PasswordStdinFlag + "]");
            return ExitUsage;
        }

        /// <summary>Reads a line from the console without echoing it; each character is shown as '*'.</summary>
        internal static char[]? ReadSecretFromConsole()
        {
            var buffer = new char[CredentialStore.MaxPasswordLength + 1];
            int length = 0;
            try
            {
                while (true)
                {
                    ConsoleKeyInfo key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Enter)
                        break;
                    if (key.Key == ConsoleKey.Escape)
                        return null;
                    if (key.Key == ConsoleKey.Backspace)
                    {
                        if (length > 0)
                        {
                            length--;
                            buffer[length] = '\0';
                            Console.Write("\b \b");
                        }

                        continue;
                    }

                    if (char.IsControl(key.KeyChar) || length >= buffer.Length)
                        continue;

                    buffer[length++] = key.KeyChar;
                    Console.Write('*');
                }

                var result = new char[length];
                Array.Copy(buffer, result, length);
                return result;
            }
            finally
            {
                Array.Clear(buffer, 0, buffer.Length);
            }
        }
    }
}
