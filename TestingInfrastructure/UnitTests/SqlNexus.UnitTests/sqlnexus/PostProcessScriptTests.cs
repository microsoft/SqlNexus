using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class PostProcessScriptTests
    {
        [TestMethod]
        public void ImportPath_WithSpacesParenthesesAndPlanNameWithSpaces_CompletesPostProcessing()
        {
            using (var fixture = new PostProcessScriptFixture("output (2)"))
            {
                File.WriteAllText(Path.Combine(fixture.ImportPath, "plan with spaces.sqlplan"), "test plan");

                ProcessResult result = fixture.Run(includeImportPath: true);

                Assert.AreEqual(0, result.ExitCode, result.Error);
                StringAssert.Contains(result.Output, "Calling SQLNexus_PostProcessing.sql");
                StringAssert.Contains(result.Output, "SQL Nexus PostProcessing complete");
            }
        }

        [TestMethod]
        public void ImportPath_WithoutPlanFiles_CompletesPostProcessing()
        {
            using (var fixture = new PostProcessScriptFixture("empty output"))
            {
                ProcessResult result = fixture.Run(includeImportPath: true);

                Assert.AreEqual(0, result.ExitCode, result.Error);
                StringAssert.Contains(result.Output, "Calling SQLNexus_PostProcessing.sql");
                StringAssert.Contains(result.Output, "SQL Nexus PostProcessing complete");
            }
        }

        [TestMethod]
        public void ImportPath_Missing_ReturnsFailureAndUsage()
        {
            using (var fixture = new PostProcessScriptFixture("unused"))
            {
                ProcessResult result = fixture.Run(includeImportPath: false);

                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "Proper usage:");
            }
        }

        [TestMethod]
        public void PostProcessExitCode_Zero_DoesNotCreateErrorMessage()
        {
            string message = fmImport.FormatPostProcessExitCodeError(0);

            Assert.IsNull(message);
        }

        [TestMethod]
        public void PostProcessExitCode_Nonzero_CreatesActionableErrorMessage()
        {
            string message = fmImport.FormatPostProcessExitCodeError(1);

            StringAssert.Contains(message, "PostProcess.cmd failed with exit code 1");
            StringAssert.Contains(message, "SQL Nexus log");
        }

        [TestMethod]
        public void PostProcessExitCode_Negative_CreatesErrorMessageWithExactCode()
        {
            string message = fmImport.FormatPostProcessExitCodeError(int.MinValue);

            StringAssert.Contains(message, int.MinValue.ToString());
        }

        private sealed class PostProcessScriptFixture : IDisposable
        {
            private readonly string root;
            private readonly string scriptPath;

            public PostProcessScriptFixture(string importDirectoryName)
            {
                root = Path.Combine(Path.GetTempPath(), "SqlNexusPostProcessTest_" + Guid.NewGuid().ToString("N"));
                ImportPath = Path.Combine(root, importDirectoryName);
                Directory.CreateDirectory(ImportPath);

                string repositoryRoot = FindRepositoryRoot();
                scriptPath = Path.Combine(root, "PostProcess.cmd");
                File.Copy(Path.Combine(repositoryRoot, "sqlnexus", "PostProcess.cmd"), scriptPath);
            }

            public string ImportPath { get; }

            public ProcessResult Run(bool includeImportPath)
            {
                string arguments = includeImportPath
                    ? string.Format("/d /c \"\"{0}\" \"test-server\" \"test-database\" \"{1}{2}\" \"false\" \"false\"\"", scriptPath, ImportPath, Path.DirectorySeparatorChar)
                    : string.Format("/d /c \"\"{0}\" \"test-server\" \"test-database\"\"", scriptPath);

                var startInfo = new ProcessStartInfo
                {
                    FileName = Environment.GetEnvironmentVariable("ComSpec"),
                    Arguments = arguments,
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                startInfo.EnvironmentVariables["PATH"] = root;

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return new ProcessResult(process.ExitCode, output, error);
                }
            }

            public void Dispose()
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }

            private static string FindRepositoryRoot()
            {
                var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "sqlnexus.sln")))
                {
                    directory = directory.Parent;
                }

                if (directory == null)
                {
                    throw new DirectoryNotFoundException("Could not locate the SqlNexus repository root.");
                }

                return directory.FullName;
            }
        }

        private sealed class ProcessResult
        {
            public ProcessResult(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output;
                Error = error;
            }

            public int ExitCode { get; }
            public string Output { get; }
            public string Error { get; }
        }
    }
}
