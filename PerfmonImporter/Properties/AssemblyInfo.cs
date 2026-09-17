using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
 
// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("PerfmonImporter")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Henderson Family")]
[assembly: AssemblyProduct("PerfmonImporter")]
[assembly: AssemblyCopyright("Copyright © Henderson Family 2006")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// Expose internal members (e.g. BLGImporter.ReadEncryptionSettings) to the unit test project
// so parsing logic can be tested without making members public solely for testing. The public
// key is required because this assembly is strong-named (it can only grant friend access to a
// strong-named assembly by full public key).
[assembly: InternalsVisibleTo("SqlNexus.UnitTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010035e2369b74530a07a7910ea41b965b4daa57b4cd92f20d3e27ab470cdbf7c68c9c314d400448071dc7312e65e3281b089bfac9fc2b258048c406d81fa0bdb01a1bc0cd5b1430fc0bd990eb14efbe684400b2bb096bd678b1b8fabe27b9daebbdbb74b44b679258622773731d2a14e15da281d83bd23c02d5d1baa8f38da86fdb")]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("aadbb342-4624-4f1d-8945-f27cf47a6a09")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Revision and Build Numbers 
// by using the '*' as shown below:
[assembly: AssemblyVersion("3.0.0.0")]
[assembly: AssemblyFileVersion("3.0.0.0")]
