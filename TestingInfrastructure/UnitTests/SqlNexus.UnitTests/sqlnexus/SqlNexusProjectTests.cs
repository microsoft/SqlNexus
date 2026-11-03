using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.sqlnexus
{
    [TestClass]
    public class SqlNexusProjectTests
    {
        [TestMethod]
        public void ProjectContent_ExcludesObsoleteChmFiles()
        {
            string repositoryRoot = FindRepositoryRoot();
            XDocument project = XDocument.Load(Path.Combine(repositoryRoot, "sqlnexus", "sqlnexus.csproj"));
            XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";

            string[] chmFiles = project.Descendants(msbuild + "Content")
                .Select(item => (string)item.Attribute("Include"))
                .Where(path => path != null && path.EndsWith(".chm", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.AreEqual(0, chmFiles.Length, "Obsolete CHM help must not be copied to the build output.");
        }

        [DataTestMethod]
        [DataRow(@"sqlnexus\sqlnexus.chm")]
        [DataRow(@"sqlnexus\Help\sqlnexus.chm")]
        [DataRow(@"sqlnexus\Help\sqlnexus2.chm")]
        public void ObsoleteChmFile_IsRemoved(string relativePath)
        {
            string repositoryRoot = FindRepositoryRoot();
            string helpFile = Path.Combine(repositoryRoot, relativePath);

            bool exists = File.Exists(helpFile);

            Assert.IsFalse(exists, "Obsolete CHM help file must be removed: " + relativePath);
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
}
