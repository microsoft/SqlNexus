using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Resolver = global::SqlNexus.McpServer.SqlCredentialResolver;
using Store = global::SqlNexus.McpServer.CredentialStore;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    [TestClass]
    public class SqlCredentialResolverTests
    {
        private const string Server = "localhost";
        private const string User = "sqlnexus_reader";
        private const string Password = "Zx9!unique-Passw0rd#42";

        private string root;
        private string directory;

        [TestInitialize]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "SqlNexusResolverTests_" + Guid.NewGuid().ToString("N"));
            directory = Path.Combine(root, "McpCredentials");
            Directory.CreateDirectory(root);
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private IConfigurationRoot BuildJsonConfiguration(params string[] jsonDocuments)
        {
            var builder = new ConfigurationBuilder().SetBasePath(root);
            for (int i = 0; i < jsonDocuments.Length; i++)
            {
                string name = "settings" + i + ".json";
                File.WriteAllText(Path.Combine(root, name), jsonDocuments[i]);
                builder.AddJsonFile(name, optional: false);
            }

            return builder.Build();
        }

        [TestMethod]
        public void RejectPlaintext_PasswordInJsonFile_ThrowsWithoutEchoingPassword()
        {
            IConfigurationRoot configuration = BuildJsonConfiguration("{\"SqlNexus\":{\"Password\":\"" + Password + "\"}}");

            var ex = Assert.ThrowsException<InvalidOperationException>(() => Resolver.RejectPlaintextPasswordInJsonConfiguration(configuration));

            StringAssert.Contains(ex.Message, "--set-credential");
            StringAssert.Contains(ex.Message, "SqlNexus:Password");
            Assert.IsFalse(ex.Message.Contains(Password));
        }

        [TestMethod]
        public void RejectPlaintext_PasswordInAnyLoadedJsonFile_Throws()
        {
            IConfigurationRoot configuration = BuildJsonConfiguration(
                "{\"SqlNexus\":{\"Server\":\"localhost\"}}",
                "{\"sqlnexus\":{\"password\":\"" + Password + "\"}}");

            Assert.ThrowsException<InvalidOperationException>(() => Resolver.RejectPlaintextPasswordInJsonConfiguration(configuration));
        }

        [TestMethod]
        public void RejectPlaintext_NoPasswordOrEmptyPassword_DoesNotThrow()
        {
            Resolver.RejectPlaintextPasswordInJsonConfiguration(BuildJsonConfiguration("{\"SqlNexus\":{\"Server\":\"localhost\"}}"));
            Resolver.RejectPlaintextPasswordInJsonConfiguration(BuildJsonConfiguration("{\"SqlNexus\":{\"Password\":\"\"}}"));
        }

        [TestMethod]
        public void RejectPlaintext_PasswordFromNonJsonSource_IsNotRejected()
        {
            // The environment-variable provider is a compatibility path, not a configuration file.
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { { "SqlNexus:Password", Password } })
                .Build();

            Resolver.RejectPlaintextPasswordInJsonConfiguration(configuration);
        }

        [TestMethod]
        public void RejectPlaintext_ShippedAppSettings_ContainsNoPassword()
        {
            string appSettings = Path.Combine(FindRepositoryRoot(), "SqlNexus.McpServer", "appsettings.json");
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(appSettings))
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            Resolver.RejectPlaintextPasswordInJsonConfiguration(configuration);
            Assert.IsTrue(string.IsNullOrEmpty(configuration["SqlNexus:Password"]));
        }

        [TestMethod]
        public void ResolvePassword_EnvironmentPassword_IsUsedAndWarns()
        {
            var warnings = new List<string>();

            string password = Resolver.ResolvePassword(Server, User, Password, warnings, directory);

            Assert.AreEqual(Password, password);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(warnings[0], "SqlNexus__Password");
            Assert.IsFalse(warnings[0].Contains(Password));
        }

        [TestMethod]
        public void ResolvePassword_EnvironmentPassword_TakesPrecedenceOverStoredCredential()
        {
            Store.Save(Server, User, "stored-password".ToCharArray(), directory);

            string password = Resolver.ResolvePassword(Server, User, Password, new List<string>(), directory);

            Assert.AreEqual(Password, password);
        }

        [TestMethod]
        public void ResolvePassword_StoredCredential_IsUsedWithoutWarning()
        {
            Store.Save(Server, User, Password.ToCharArray(), directory);
            var warnings = new List<string>();

            string password = Resolver.ResolvePassword(Server, User, null, warnings, directory);

            Assert.AreEqual(Password, password);
            Assert.AreEqual(0, warnings.Count);
        }

        [TestMethod]
        public void ResolvePassword_NoSecretAvailable_FailsClosedWithInstruction()
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => Resolver.ResolvePassword(Server, User, "", new List<string>(), directory));

            StringAssert.Contains(ex.Message, "--set-credential");
        }

        [TestMethod]
        public void ResolvePassword_MissingUserId_FailsClosed()
        {
            Assert.ThrowsException<InvalidOperationException>(() => Resolver.ResolvePassword(Server, null, Password, new List<string>(), directory));
            Assert.ThrowsException<InvalidOperationException>(() => Resolver.ResolvePassword(Server, "  ", Password, new List<string>(), directory));
        }

        [TestMethod]
        public void ResolvePassword_StoredCredentialForDifferentUser_IsNotUsed()
        {
            Store.Save(Server, "someone_else", Password.ToCharArray(), directory);

            Assert.ThrowsException<InvalidOperationException>(
                () => Resolver.ResolvePassword(Server, User, null, new List<string>(), directory));
        }

        private static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "SqlNexus.McpServer", "appsettings.json")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the SQL Nexus repository root.");
        }
    }
}
