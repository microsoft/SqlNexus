using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("sqlnexus")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Microsoft")]
[assembly: AssemblyProduct("sqlnexus")]
[assembly: AssemblyCopyright("Copyright © 2022 Microsoft")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// Expose internal members (e.g. Program.TryParseImporterSelection) to the unit test project.
// The test project is strong-named with SqlNexus.snk, so the grant must include its PublicKey
// (a strong-named friend cannot be referenced by simple name alone).
[assembly: InternalsVisibleTo("SqlNexus.UnitTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010035e2369b74530a07a7910ea41b965b4daa57b4cd92f20d3e27ab470cdbf7c68c9c314d400448071dc7312e65e3281b089bfac9fc2b258048c406d81fa0bdb01a1bc0cd5b1430fc0bd990eb14efbe684400b2bb096bd678b1b8fabe27b9daebbdbb74b44b679258622773731d2a14e15da281d83bd23c02d5d1baa8f38da86fdb")]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("da26622b-cd7a-47f6-8962-459747425941")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
[assembly: AssemblyVersion("7.26.08.31")]
[assembly: AssemblyFileVersion("7.26.08.31")]
