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

        [TestMethod]
        public void GetImportSearchPaths_PrimaryContainsDotDotSegment_NormalizesAndResolvesSibling()
        {
            // Path.GetFullPath collapses ".." before resolution, so an instance path expressed with
            // a traversal segment still resolves to the same real folder and its sibling shared
            // folder. This documents the post-normalization behavior after removing the (dead)
            // direct-sibling guard.
            CreateDir("output");
            string instance = CreateDir("output", "SERVER_SQL2019");
            string shared = CreateDir("output", SharedOutputFolder.SharedFolderName);

            string traversalInput = Path.Combine(instance, "sub", "..");

            List<string> result = SharedOutputFolder.GetImportSearchPaths(traversalInput);

            Assert.AreEqual(2, result.Count, "Normalized traversal path should still find the sibling.");
            Assert.AreEqual(Path.GetFullPath(instance).TrimEnd(Path.DirectorySeparatorChar), result[0]);
            Assert.AreEqual(Path.GetFullPath(shared).TrimEnd(Path.DirectorySeparatorChar), result[1]);
        }

        [TestMethod]
        public void GetImportSearchPaths_DriveRootInput_PreservesRootedPath()
        {
            string driveRoot = Path.GetPathRoot(Path.GetFullPath(_root));

            List<string> result = SharedOutputFolder.GetImportSearchPaths(driveRoot);

            Assert.AreEqual(1, result.Count, "Drive root should resolve to only the primary path.");
            Assert.AreEqual(driveRoot, result[0], "Drive root must remain rooted (e.g. 'C:\\'), not 'C:'.");
            Assert.IsTrue(Path.IsPathRooted(result[0]), "Resolved path must be rooted.");
        }

        // ---- FilterDuplicateSiblingFiles (size-aware duplicate-name guard) ----

        [TestMethod]
        public void FilterDuplicateSiblingFiles_RunningDriversTxt_CopiedIntoBothFolders_IsDeduped()
        {
            // Regression for the reported case: a *.TXT (Rowset importer, which IS an
            // INexusFileImporter) manually copied into the primary instance folder while it also
            // exists in SharedOutputFiles must NOT import twice.
            const string leaf = "_not_instance_specific_20260908T1925577918_RunningDrivers.txt";
            var primary = new Dictionary<string, long>
            {
                { leaf, 20480 }, // selected from the instance folder
            };
            var sibling = Sib(
                (@"D:\SQLLogScout\output_AllInstances1\SharedOutputFiles\" + leaf, 20480));

            List<string> sameSize, diffSize;
            List<string> accepted = SharedOutputFolder.FilterDuplicateSiblingFiles(
                primary, sibling, out sameSize, out diffSize);

            Assert.AreEqual(0, accepted.Count, "The SharedOutputFiles copy must be skipped (no re-import).");
            Assert.AreEqual(1, sameSize.Count);
            Assert.AreEqual(0, diffSize.Count);
        }

        private static List<KeyValuePair<string, long>> Sib(params (string path, long size)[] items)
        {
            var list = new List<KeyValuePair<string, long>>();
            foreach (var i in items)
                list.Add(new KeyValuePair<string, long>(i.path, i.size));
            return list;
        }

        [TestMethod]
        public void FilterDuplicateSiblingFiles_NoOverlap_ReturnsAllSiblingFiles()
        {
            var primary = new Dictionary<string, long> { { "SERVER_SQLDIAG.OUT", 100 } };
            var sibling = Sib(
                (@"C:\out\SharedOutputFiles\HOST_OS.OUT", 10),
                (@"C:\out\SharedOutputFiles\HOST_NET.OUT", 20));

            List<string> sameSize, diffSize;
            List<string> accepted = SharedOutputFolder.FilterDuplicateSiblingFiles(
                primary, sibling, out sameSize, out diffSize);

            Assert.AreEqual(2, accepted.Count, "No name overlap => all sibling files accepted.");
            Assert.AreEqual(0, sameSize.Count);
            Assert.AreEqual(0, diffSize.Count);
        }

        [TestMethod]
        public void FilterDuplicateSiblingFiles_NameAndSizeMatch_SkippedAsSameSizeCaseInsensitive()
        {
            var primary = new Dictionary<string, long> { { "SQLDIAG.OUT", 5000 } };
            var sibling = Sib(
                (@"C:\out\SharedOutputFiles\sqldiag.out", 5000), // same name (diff case) + same size
                (@"C:\out\SharedOutputFiles\unique.out", 10));

            List<string> sameSize, diffSize;
            List<string> accepted = SharedOutputFolder.FilterDuplicateSiblingFiles(
                primary, sibling, out sameSize, out diffSize);

            CollectionAssert.AreEquivalent(new[] { @"C:\out\SharedOutputFiles\unique.out" }, accepted);
            CollectionAssert.AreEquivalent(new[] { @"C:\out\SharedOutputFiles\sqldiag.out" }, sameSize);
            Assert.AreEqual(0, diffSize.Count);
        }

        [TestMethod]
        public void FilterDuplicateSiblingFiles_NameMatchesButSizeDiffers_SkippedAsDifferentSize()
        {
            var primary = new Dictionary<string, long> { { "ERRORLOG.OUT", 4096 } };
            var sibling = Sib((@"C:\out\SharedOutputFiles\ERRORLOG.OUT", 8192)); // grew -> ambiguous

            List<string> sameSize, diffSize;
            List<string> accepted = SharedOutputFolder.FilterDuplicateSiblingFiles(
                primary, sibling, out sameSize, out diffSize);

            Assert.AreEqual(0, accepted.Count, "Colliding name is skipped either way (primary wins).");
            Assert.AreEqual(0, sameSize.Count);
            CollectionAssert.AreEquivalent(new[] { @"C:\out\SharedOutputFiles\ERRORLOG.OUT" }, diffSize);
        }

        [TestMethod]
        public void FilterDuplicateSiblingFiles_UnknownSize_TreatedAsDifferentSize()
        {
            // -1 = size could not be read; must be surfaced (different-size), not assumed identical.
            var primary = new Dictionary<string, long> { { "a.out", -1 } };
            var sibling = Sib((@"C:\s\a.out", 100));

            List<string> sameSize, diffSize;
            List<string> accepted = SharedOutputFolder.FilterDuplicateSiblingFiles(
                primary, sibling, out sameSize, out diffSize);

            Assert.AreEqual(0, accepted.Count);
            Assert.AreEqual(0, sameSize.Count);
            CollectionAssert.AreEquivalent(new[] { @"C:\s\a.out" }, diffSize);
        }

        [TestMethod]
        public void FilterDuplicateSiblingFiles_NullSiblingFiles_ReturnsEmpty()
        {
            List<string> sameSize, diffSize;
            List<string> accepted = SharedOutputFolder.FilterDuplicateSiblingFiles(
                new Dictionary<string, long> { { "a.out", 1 } }, null, out sameSize, out diffSize);

            Assert.AreEqual(0, accepted.Count);
            Assert.AreEqual(0, sameSize.Count);
            Assert.AreEqual(0, diffSize.Count);
        }

        [TestMethod]
        public void FilterDuplicateSiblingFiles_NullPrimaryMap_AcceptsAll()
        {
            var sibling = Sib((@"C:\s\a.out", 1), (@"C:\s\b.out", 2));

            List<string> sameSize, diffSize;
            List<string> accepted = SharedOutputFolder.FilterDuplicateSiblingFiles(
                null, sibling, out sameSize, out diffSize);

            Assert.AreEqual(2, accepted.Count, "With no primary map, nothing is a duplicate.");
            Assert.AreEqual(0, sameSize.Count);
            Assert.AreEqual(0, diffSize.Count);
        }

        [TestMethod]
        public void FilterDuplicateSiblingFiles_IgnoresNullAndEmptyEntries()
        {
            var primary = new Dictionary<string, long> { { "keep.out", 42 } };
            var sibling = Sib(
                (@"C:\s\keep.out", 42),
                ("", 0),
                (null, 0),
                (@"C:\s\other.out", 7));

            List<string> sameSize, diffSize;
            List<string> accepted = SharedOutputFolder.FilterDuplicateSiblingFiles(
                primary, sibling, out sameSize, out diffSize);

            CollectionAssert.AreEquivalent(new[] { @"C:\s\other.out" }, accepted);
            CollectionAssert.AreEquivalent(new[] { @"C:\s\keep.out" }, sameSize);
            Assert.AreEqual(0, diffSize.Count);
        }

        // ---- GetSiblingOnlyFiles (Custom XEL sibling-only gap detection) ------

        [TestMethod]
        public void GetSiblingOnlyFiles_FileInBothFolders_NotReturned()
        {
            // Regression: system_health XEL copied into BOTH folders. The primary copy is imported,
            // so the sibling copy must NOT be flagged as an unimported gap.
            const string leaf = "DESKTOP_SQL2019_system_health_0_134291004454640000.xel";
            var primaryNames = new HashSet<string> { leaf };
            var siblingFiles = new[] { @"D:\out\SharedOutputFiles\" + leaf };

            List<string> siblingOnly = SharedOutputFolder.GetSiblingOnlyFiles(primaryNames, siblingFiles);

            Assert.AreEqual(0, siblingOnly.Count, "A file present in both folders is not a gap.");
        }

        [TestMethod]
        public void GetSiblingOnlyFiles_FileOnlyInSibling_Returned()
        {
            var primaryNames = new HashSet<string> { "server_SQLDIAG_0_100.xel" };
            var siblingFiles = new[]
            {
                @"D:\out\SharedOutputFiles\host_system_health_0_200.xel", // sibling only -> gap
                @"D:\out\SharedOutputFiles\server_SQLDIAG_0_100.xel",     // also in primary -> not a gap
            };

            List<string> siblingOnly = SharedOutputFolder.GetSiblingOnlyFiles(primaryNames, siblingFiles);

            CollectionAssert.AreEquivalent(
                new[] { @"D:\out\SharedOutputFiles\host_system_health_0_200.xel" }, siblingOnly);
        }

        [TestMethod]
        public void GetSiblingOnlyFiles_CaseInsensitiveNameMatch_NotReturned()
        {
            var primaryNames = new HashSet<string> { "SYSTEM_HEALTH.XEL" };
            var siblingFiles = new[] { @"D:\s\system_health.xel" };

            List<string> siblingOnly = SharedOutputFolder.GetSiblingOnlyFiles(primaryNames, siblingFiles);

            Assert.AreEqual(0, siblingOnly.Count, "Name comparison must be case-insensitive.");
        }

        [TestMethod]
        public void GetSiblingOnlyFiles_NullSiblingFiles_ReturnsEmpty()
        {
            List<string> siblingOnly = SharedOutputFolder.GetSiblingOnlyFiles(
                new HashSet<string> { "a.xel" }, null);

            Assert.AreEqual(0, siblingOnly.Count);
        }

        [TestMethod]
        public void GetSiblingOnlyFiles_NullOrEmptyPrimary_ReturnsAllSibling()
        {
            var siblingFiles = new[] { @"D:\s\a.xel", @"D:\s\b.xel" };

            List<string> siblingOnly = SharedOutputFolder.GetSiblingOnlyFiles(null, siblingFiles);

            CollectionAssert.AreEquivalent(siblingFiles, siblingOnly);
        }

        [TestMethod]
        public void GetSiblingOnlyFiles_IgnoresNullAndEmptyEntries()
        {
            var primaryNames = new HashSet<string> { "keep.xel", "", null };
            var siblingFiles = new[] { @"D:\s\keep.xel", "", null, @"D:\s\only.xel" };

            List<string> siblingOnly = SharedOutputFolder.GetSiblingOnlyFiles(primaryNames, siblingFiles);

            CollectionAssert.AreEquivalent(new[] { @"D:\s\only.xel" }, siblingOnly);
        }
    }
}
