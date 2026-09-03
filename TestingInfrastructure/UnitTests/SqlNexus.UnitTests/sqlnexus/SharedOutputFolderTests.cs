using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using sqlnexus;

namespace SqlNexus.UnitTests.sqlnexus
{
    /// <summary>
    /// Tests for <see cref="SharedOutputFolder"/>: resolving the ordered list of directories to
    /// search for importable files. The key contract is: if a sibling "SharedOutputFiles" folder
    /// exists, search both the primary and the sibling; otherwise return only the primary so that
    /// existing single-folder behavior is unchanged. Also verifies the direct-sibling security
    /// guard (no directory traversal / non-sibling matches).
    /// </summary>
    [TestClass]
    public class SharedOutputFolderTests
    {
        private string _root;

        [TestInitialize]
        public void Setup()
        {
            // Unique temp fixture root so tests are isolated and can run in parallel.
            _root = Path.Combine(Path.GetTempPath(), "SqlNexusSharedOutputTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (_root != null && Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; never fail a test because temp deletion failed.
            }
        }

        private string CreateDir(params string[] parts)
        {
            string path = _root;
            foreach (var p in parts)
                path = Path.Combine(path, p);
            Directory.CreateDirectory(path);
            return path;
        }

        // ---- Happy path -------------------------------------------------------

        [TestMethod]
        public void GetImportSearchPaths_SiblingSharedExists_ReturnsBothPaths()
        {
            string output = CreateDir("output");
            string instance = CreateDir("output", "SERVER_SQL2019");
            string shared = CreateDir("output", SharedOutputFolder.SharedFolderName);

            List<string> result = SharedOutputFolder.GetImportSearchPaths(instance);

            Assert.AreEqual(2, result.Count, "Expected primary + sibling shared folder.");
            Assert.AreEqual(Path.GetFullPath(instance).TrimEnd(Path.DirectorySeparatorChar), result[0]);
            Assert.AreEqual(Path.GetFullPath(shared).TrimEnd(Path.DirectorySeparatorChar), result[1]);
        }

        [TestMethod]
        public void GetImportSearchPaths_PrimaryHasTrailingSeparator_StillResolvesSibling()
        {
            CreateDir("output");
            string instance = CreateDir("output", "SERVER_SQL2022");
            string shared = CreateDir("output", SharedOutputFolder.SharedFolderName);

            List<string> result = SharedOutputFolder.GetImportSearchPaths(instance + Path.DirectorySeparatorChar);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(Path.GetFullPath(shared).TrimEnd(Path.DirectorySeparatorChar), result[1]);
        }

        // ---- Sibling absent => unchanged behavior -----------------------------

        [TestMethod]
        public void GetImportSearchPaths_NoSibling_ReturnsOnlyPrimary()
        {
            CreateDir("output");
            string instance = CreateDir("output", "SERVER_SQL2019");
            // Note: no SharedOutputFiles folder created.

            List<string> result = SharedOutputFolder.GetImportSearchPaths(instance);

            Assert.AreEqual(1, result.Count, "With no sibling, only the primary path should be returned.");
            Assert.AreEqual(Path.GetFullPath(instance).TrimEnd(Path.DirectorySeparatorChar), result[0]);
        }

        // ---- Edge / boundary cases -------------------------------------------

        [TestMethod]
        public void GetImportSearchPaths_NullOrEmpty_ReturnsEmpty()
        {
            Assert.AreEqual(0, SharedOutputFolder.GetImportSearchPaths(null).Count);
            Assert.AreEqual(0, SharedOutputFolder.GetImportSearchPaths("").Count);
            Assert.AreEqual(0, SharedOutputFolder.GetImportSearchPaths("   ").Count);
        }

        [TestMethod]
        public void GetImportSearchPaths_PrimaryIsSharedFolderItself_DoesNotAddSelf()
        {
            CreateDir("output");
            string shared = CreateDir("output", SharedOutputFolder.SharedFolderName);

            // Pointing directly at SharedOutputFiles must not add a nested self-sibling, and there
            // is no SharedOutputFiles inside SharedOutputFiles, so only the primary is returned.
            List<string> result = SharedOutputFolder.GetImportSearchPaths(shared);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(Path.GetFullPath(shared).TrimEnd(Path.DirectorySeparatorChar), result[0]);
        }

        [TestMethod]
        public void GetImportSearchPaths_SiblingSharedIsAFileNotDirectory_Ignored()
        {
            CreateDir("output");
            string instance = CreateDir("output", "SERVER_SQL2019");
            // Create a FILE named SharedOutputFiles (not a directory) as a sibling.
            File.WriteAllText(Path.Combine(_root, "output", SharedOutputFolder.SharedFolderName), "not a dir");

            List<string> result = SharedOutputFolder.GetImportSearchPaths(instance);

            Assert.AreEqual(1, result.Count, "A file (not directory) named SharedOutputFiles must be ignored.");
        }

        [TestMethod]
        public void ResolveSharedSibling_NoSibling_ReturnsNull()
        {
            CreateDir("output");
            string instance = CreateDir("output", "SERVER_SQL2019");

            Assert.IsNull(SharedOutputFolder.ResolveSharedSibling(
                Path.GetFullPath(instance).TrimEnd(Path.DirectorySeparatorChar)));
        }
    }
}
