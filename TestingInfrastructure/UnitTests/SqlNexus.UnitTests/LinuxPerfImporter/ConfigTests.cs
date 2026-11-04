using System.IO;
using LinuxPerfImporter.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlNexus.UnitTests.LinuxPerfImporter
{
    [TestClass]
    public class ConfigTests
    {
        [TestMethod]
        public void GetMachineNameFromConfigFile_ValidName_ReturnsMachineName()
        {
            // Arrange
            string configFile = @".\sql-host_01_os_machine_config.info";

            // Act
            string machineName = Config.GetMachineNameFromConfigFile(configFile);

            // Assert
            Assert.AreEqual("sql-host_01", machineName);
        }

        [DataTestMethod]
        [DataRow(@".\machine -f CSV -config cfg_os_machine_config.info")]
        [DataRow(@".\.._os_machine_config.info")]
        [DataRow(@".\_os_machine_config.info")]
        public void GetMachineNameFromConfigFile_UnsafeName_ThrowsInvalidDataException(string configFile)
        {
            // Act and Assert
            Assert.ThrowsException<InvalidDataException>(() => Config.GetMachineNameFromConfigFile(configFile));
        }

        [TestMethod]
        public void GetMachineNameFromConfigFile_UnexpectedSuffix_ThrowsInvalidDataException()
        {
            // Arrange
            string configFile = @".\machine.info";

            // Act and Assert
            Assert.ThrowsException<InvalidDataException>(() => Config.GetMachineNameFromConfigFile(configFile));
        }

        [TestMethod]
        public void GetMachineNameFromConfigFile_NullPath_ThrowsInvalidDataException()
        {
            // Act and Assert
            Assert.ThrowsException<InvalidDataException>(() => Config.GetMachineNameFromConfigFile(null));
        }
    }
}
