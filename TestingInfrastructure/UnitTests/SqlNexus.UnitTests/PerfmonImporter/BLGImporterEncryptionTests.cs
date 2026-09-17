using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfmonImporter;

namespace SqlNexus.UnitTests.PerfmonImporter
{
    /// <summary>
    /// Tests for <see cref="BLGImporter.ReadEncryptionSettings"/>, which maps the app's
    /// connection-string encryption options onto the boolean flags passed to the relog DSN.
    /// These tests validate that the DSN honors the same transport-security choices as the app
    /// and that malformed/empty input fails closed to "no encryption".
    /// </summary>
    [TestClass]
    public class BLGImporterEncryptionTests
    {
        [TestMethod]
        public void ReadEncryptionSettings_EncryptMandatory_ReturnsEncryptTrue()
        {
            bool encrypt, trust;

            BLGImporter.ReadEncryptionSettings(
                "Server=myserver;Database=mydb;Encrypt=Mandatory;TrustServerCertificate=True;",
                out encrypt, out trust);

            Assert.IsTrue(encrypt);
            Assert.IsTrue(trust);
        }

        [TestMethod]
        public void ReadEncryptionSettings_EncryptStrict_ReturnsEncryptTrue()
        {
            bool encrypt, trust;

            BLGImporter.ReadEncryptionSettings(
                "Server=myserver;Database=mydb;Encrypt=Strict;",
                out encrypt, out trust);

            Assert.IsTrue(encrypt);
            Assert.IsFalse(trust);
        }

        [TestMethod]
        public void ReadEncryptionSettings_EncryptOptional_ReturnsEncryptFalse()
        {
            bool encrypt, trust;

            BLGImporter.ReadEncryptionSettings(
                "Server=myserver;Database=mydb;Encrypt=Optional;",
                out encrypt, out trust);

            Assert.IsFalse(encrypt);
            Assert.IsFalse(trust);
        }

        [TestMethod]
        public void ReadEncryptionSettings_TrustServerCertificateTrue_ReturnsTrustTrue()
        {
            bool encrypt, trust;

            BLGImporter.ReadEncryptionSettings(
                "Server=myserver;Database=mydb;Encrypt=Optional;TrustServerCertificate=True;",
                out encrypt, out trust);

            Assert.IsFalse(encrypt);
            Assert.IsTrue(trust);
        }

        [TestMethod]
        public void ReadEncryptionSettings_MalformedConnectionString_FailsClosedToNoEncryption()
        {
            bool encrypt, trust;

            // An unparseable connection string must fail closed (no encryption, no trust) rather
            // than throw, so the import can continue with a safe, well-defined default.
            BLGImporter.ReadEncryptionSettings(
                "this is not a valid=connection=string;;;=",
                out encrypt, out trust);

            Assert.IsFalse(encrypt);
            Assert.IsFalse(trust);
        }
    }
}
