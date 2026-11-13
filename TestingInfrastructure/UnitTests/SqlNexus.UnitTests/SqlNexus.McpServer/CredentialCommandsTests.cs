using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Commands = global::SqlNexus.McpServer.CredentialCommands;
using Store = global::SqlNexus.McpServer.CredentialStore;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class CredentialCommandsTests
    {
        private const string Server = "localhost\\SQLEXPRESS";
        private const string User = "sqlnexus_reader";
        private static readonly string Password = TestSecrets.NewPassword();

        private string root;
        private string directory;

        [TestInitialize]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "SqlNexusCredentialCommandsTests_" + Guid.NewGuid().ToString("N"));
            directory = Path.Combine(root, "McpCredentials");
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private sealed class Result
        {
            public int ExitCode;
            public string Output;
            public string Error;
            public string Combined { get { return Output + Error; } }
        }

        private Result Run(string[] args, string stdin = null, bool redirected = true, params string[] interactiveSecrets)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var secrets = new Queue<string>(interactiveSecrets);
            Func<char[]> readSecret = () =>
            {
                if (secrets.Count == 0)
                {
                    return null;
                }

                string next = secrets.Dequeue();
                return next == null ? null : next.ToCharArray();
            };

            int exitCode = Commands.Execute(
                args, new StringReader(stdin ?? string.Empty), redirected, output, error, readSecret, directory);
            return new Result { ExitCode = exitCode, Output = output.ToString(), Error = error.ToString() };
        }

        private static string[] Args(string command, params string[] extra)
        {
            var list = new List<string> { command, "--server", Server, "--user-id", User };
            list.AddRange(extra);
            return list.ToArray();
        }

        [TestMethod]
        public void IsCredentialCommand_RecognizesOnlyTheThreeCommands()
        {
            Assert.IsTrue(Commands.IsCredentialCommand(new[] { "--set-credential" }));
            Assert.IsTrue(Commands.IsCredentialCommand(new[] { "--server", "x", "--CHECK-CREDENTIAL" }));
            Assert.IsTrue(Commands.IsCredentialCommand(new[] { "--delete-credential" }));
            Assert.IsFalse(Commands.IsCredentialCommand(new[] { "--server", "x", "--trusted-connection", "false" }));
            Assert.IsFalse(Commands.IsCredentialCommand(new string[0]));
        }

        [TestMethod]
        public void Set_PasswordFromStdin_StoresCredentialWithoutEchoingPassword()
        {
            Result result = Run(Args("--set-credential", "--password-stdin"), Password + "\n");

            Assert.AreEqual(Commands.ExitSuccess, result.ExitCode, result.Error);
            StringAssert.Contains(result.Output, "Stored the SQL credential");
            Assert.IsFalse(result.Combined.Contains(Password));
            Assert.IsTrue(Store.TryLoad(Server, User, out string stored, out string error, directory), error);
            Assert.AreEqual(Password, stored);
        }

        [TestMethod]
        public void Set_PasswordFromStdinWithCrLf_StoresPasswordWithoutLineBreak()
        {
            Result result = Run(Args("--set-credential", "--password-stdin"), Password + "\r\n");

            Assert.AreEqual(Commands.ExitSuccess, result.ExitCode, result.Error);
            Assert.IsTrue(Store.TryLoad(Server, User, out string stored, out string error, directory), error);
            Assert.AreEqual(Password, stored);
        }

        [TestMethod]
        public void Set_PasswordStdinWithoutRedirectedInput_IsRejected()
        {
            Result result = Run(Args("--set-credential", "--password-stdin"), Password, redirected: false);

            Assert.AreEqual(Commands.ExitUsage, result.ExitCode);
            Assert.IsFalse(Directory.Exists(directory));
        }

        [TestMethod]
        public void Set_RedirectedInputWithoutPasswordStdin_IsRejected()
        {
            // Guards against an agent/stdio launch ever storing a credential by accident.
            Result result = Run(Args("--set-credential"), Password + "\n");

            Assert.AreEqual(Commands.ExitUsage, result.ExitCode);
            StringAssert.Contains(result.Error, "--password-stdin");
            Assert.IsFalse(Directory.Exists(directory));
        }

        [TestMethod]
        public void Set_EmptyStdin_StoresNothing()
        {
            Assert.AreEqual(Commands.ExitFailure, Run(Args("--set-credential", "--password-stdin"), "").ExitCode);
            Assert.AreEqual(Commands.ExitFailure, Run(Args("--set-credential", "--password-stdin"), "\n").ExitCode);
            Assert.IsFalse(Directory.Exists(directory));
        }

        [TestMethod]
        public void Set_InteractiveMatchingEntries_StoresCredential()
        {
            Result result = Run(Args("--set-credential"), redirected: false, interactiveSecrets: new[] { Password, Password });

            Assert.AreEqual(Commands.ExitSuccess, result.ExitCode, result.Error);
            Assert.IsFalse(result.Combined.Contains(Password));
            Assert.IsTrue(Store.TryLoad(Server, User, out string stored, out string error, directory), error);
            Assert.AreEqual(Password, stored);
        }

        [TestMethod]
        public void Set_InteractiveMismatchedConfirmation_StoresNothing()
        {
            Result result = Run(Args("--set-credential"), redirected: false, interactiveSecrets: new[] { Password, Password + "x" });

            Assert.AreEqual(Commands.ExitFailure, result.ExitCode);
            StringAssert.Contains(result.Error, "did not match");
            Assert.IsFalse(result.Combined.Contains(Password));
            Assert.IsFalse(Directory.Exists(directory));
        }

        [TestMethod]
        public void Set_InteractiveCancelledOrEmpty_StoresNothing()
        {
            Assert.AreEqual(Commands.ExitFailure, Run(Args("--set-credential"), redirected: false, interactiveSecrets: new string[] { null }).ExitCode);
            Assert.AreEqual(Commands.ExitFailure, Run(Args("--set-credential"), redirected: false, interactiveSecrets: new[] { "" }).ExitCode);
            Assert.IsFalse(Directory.Exists(directory));
        }

        [TestMethod]
        public void Set_PasswordTooLong_IsRejectedWithoutEchoingPassword()
        {
            string tooLong = new string('p', Store.MaxPasswordLength + 1);

            Result result = Run(Args("--set-credential", "--password-stdin"), tooLong + "\n");

            Assert.AreEqual(Commands.ExitUsage, result.ExitCode);
            Assert.IsFalse(result.Combined.Contains(tooLong));
            Assert.IsFalse(Directory.Exists(directory));
        }

        [TestMethod]
        public void Check_ExistingCredential_ReturnsSuccessWithoutRevealingPassword()
        {
            Run(Args("--set-credential", "--password-stdin"), Password + "\n");

            Result result = Run(Args("--check-credential"));

            Assert.AreEqual(Commands.ExitSuccess, result.ExitCode, result.Error);
            Assert.IsFalse(result.Combined.Contains(Password));
        }

        [TestMethod]
        public void Check_MissingCredential_ReturnsFailureWithSetInstruction()
        {
            Result result = Run(Args("--check-credential"));

            Assert.AreEqual(Commands.ExitFailure, result.ExitCode);
            StringAssert.Contains(result.Error, "--set-credential");
        }

        [TestMethod]
        public void Check_CorruptCredential_ReturnsFailure()
        {
            Run(Args("--set-credential", "--password-stdin"), Password + "\n");
            File.WriteAllBytes(Store.GetCredentialPath(Server, User, directory), new byte[] { 1, 2, 3, 4, 5, 6, 7 });

            Assert.AreEqual(Commands.ExitFailure, Run(Args("--check-credential")).ExitCode);
        }

        [TestMethod]
        public void Delete_ExistingCredential_RemovesIt()
        {
            Run(Args("--set-credential", "--password-stdin"), Password + "\n");

            Result result = Run(Args("--delete-credential"));

            Assert.AreEqual(Commands.ExitSuccess, result.ExitCode, result.Error);
            StringAssert.Contains(result.Output, "Deleted");
            Assert.AreEqual(Commands.ExitFailure, Run(Args("--check-credential")).ExitCode);
        }

        [TestMethod]
        public void Delete_MissingCredential_IsIdempotentSuccess()
        {
            Result result = Run(Args("--delete-credential"));

            Assert.AreEqual(Commands.ExitSuccess, result.ExitCode, result.Error);
            StringAssert.Contains(result.Output, "No stored SQL credential");
        }

        [TestMethod]
        public void Execute_InvalidUsage_ReturnsUsageErrorAndStoresNothing()
        {
            Assert.AreEqual(Commands.ExitUsage, Run(new[] { "--set-credential", "--user-id", User, "--password-stdin" }, Password).ExitCode);
            Assert.AreEqual(Commands.ExitUsage, Run(new[] { "--set-credential", "--server", Server, "--password-stdin" }, Password).ExitCode);
            Assert.AreEqual(Commands.ExitUsage, Run(new[] { "--set-credential", "--check-credential", "--server", Server, "--user-id", User }).ExitCode);
            Assert.AreEqual(Commands.ExitUsage, Run(Args("--set-credential", "--password", Password)).ExitCode);
            Assert.AreEqual(Commands.ExitUsage, Run(Args("--check-credential", "--password-stdin")).ExitCode);
            Assert.AreEqual(Commands.ExitUsage, Run(Args("--delete-credential", "--unknown")).ExitCode);
            Assert.IsFalse(Directory.Exists(directory));
        }

        [TestMethod]
        public void Execute_PasswordSuppliedAsArgument_IsRejectedWithoutEchoingIt()
        {
            Result result = Run(Args("--set-credential", "--password", Password), redirected: false);

            Assert.AreEqual(Commands.ExitUsage, result.ExitCode);
            Assert.IsFalse(result.Combined.Contains(Password));
        }

        [TestMethod]
        public void Set_PasswordWithEmbeddedLineBreak_IsRejectedInsteadOfTruncated()
        {
            // Regression: ReadLine() used to cut the password at the first CR/LF and store the prefix.
            Result carriageReturn = Run(Args("--set-credential", "--password-stdin"), "abc\rdef\n");
            Result lineFeed = Run(Args("--set-credential", "--password-stdin"), "abc\ndef\n");

            Assert.AreEqual(Commands.ExitUsage, carriageReturn.ExitCode);
            Assert.AreEqual(Commands.ExitUsage, lineFeed.ExitCode);
            Assert.IsFalse(Directory.Exists(directory));
        }

        [TestMethod]
        public void Set_PasswordStdinWithoutTrailingLineBreak_StoresPassword()
        {
            Result result = Run(Args("--set-credential", "--password-stdin"), Password);

            Assert.AreEqual(Commands.ExitSuccess, result.ExitCode, result.Error);
            Assert.IsTrue(Store.TryLoad(Server, User, out string stored, out string error, directory), error);
            Assert.AreEqual(Password, stored);
        }

        [TestMethod]
        public void Execute_UnrecognizedPositionalArgument_DoesNotEchoIt()
        {
            // A mistyped positional password must not be echoed to stderr.
            string mistyped = TestSecrets.NewPassword();
            Result result = Run(Args("--set-credential", mistyped), redirected: false);

            Assert.AreEqual(Commands.ExitUsage, result.ExitCode);
            Assert.IsFalse(result.Combined.Contains(mistyped));
        }

        [TestMethod]
        public void Execute_InvalidServerOrUser_ReturnsUsageError()
        {
            Result result = Run(new[] { "--set-credential", "--server", "bad\u0001server", "--user-id", User, "--password-stdin" }, Password + "\n");

            Assert.AreEqual(Commands.ExitUsage, result.ExitCode);
            Assert.IsFalse(result.Combined.Contains(Password));
        }
    }
}
