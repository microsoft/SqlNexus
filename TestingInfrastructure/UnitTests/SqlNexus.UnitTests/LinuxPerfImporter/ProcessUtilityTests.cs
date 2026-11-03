using System;
using System.IO;
using LinuxPerfImporter.Model;
using LinuxPerfImporter.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.LinuxPerfImporter
{
    [TestClass]
    public class ProcessUtilityTests
    {
        [TestMethod]
        public void RelogExecutablePath_UsesWindowsSystemDirectory()
        {
            // Arrange
            string expectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "relog.exe");

            // Act
            string actualPath = ProcessUtility.RelogExecutablePath;

            // Assert
            Assert.AreEqual(expectedPath, actualPath);
            Assert.IsTrue(Path.IsPathRooted(actualPath));
        }

        [TestMethod]
        public void CreateStartInfo_FileNameContainingRelogOptions_ProducesSingleQuotedArgument()
        {
            // Arrange
            string inputFile = @".\Evil -f CSV -config settings.tsv";
            string outputFile = @".\Evil -f CSV -config settings.blg";

            // Act
            var startInfo = ProcessUtility.CreateStartInfo(
                ProcessUtility.RelogExecutablePath,
                inputFile,
                "-o",
                outputFile,
                "-y");

            // Assert
            Assert.AreEqual(ProcessUtility.RelogExecutablePath, startInfo.FileName);
            Assert.AreEqual("\".\\Evil -f CSV -config settings.tsv\" \"-o\" \".\\Evil -f CSV -config settings.blg\" \"-y\"", startInfo.Arguments);
            Assert.IsFalse(startInfo.UseShellExecute);
        }

        [TestMethod]
        public void QuoteArgument_EmbeddedQuoteAndTrailingBackslash_EscapesForWindowsCommandLine()
        {
            // Arrange
            string argument = "value\\\"quoted\\";
            string expected = "\"value" + new string('\\', 3) + "\"quoted" + new string('\\', 2) + "\"";

            // Act
            string result = ProcessUtility.QuoteArgument(argument);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CreateStartInfo_RelativeExecutablePath_ThrowsArgumentException()
        {
            // Arrange
            string executablePath = "relog.exe";

            // Act and Assert
            Assert.ThrowsException<ArgumentException>(() => ProcessUtility.CreateStartInfo(executablePath, "-y"));
        }

        [TestMethod]
        public void ValidateRelogExitCode_Zero_DoesNotThrow()
        {
            // Act and Assert
            ImportCombine.ValidateRelogExitCode(0);
        }

        [TestMethod]
        public void ValidateRelogExitCode_Nonzero_ThrowsInvalidOperationException()
        {
            // Act and Assert
            Assert.ThrowsException<InvalidOperationException>(() => ImportCombine.ValidateRelogExitCode(11));
        }
    }
}
