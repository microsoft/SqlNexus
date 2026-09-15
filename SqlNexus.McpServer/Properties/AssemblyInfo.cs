using System.Runtime.CompilerServices;

// Expose internal members to the unit test project. This assembly is strong-named, so the grant
// must include the (strong-named) test project's PublicKey; a simple-name grant would not match.
[assembly: InternalsVisibleTo("SqlNexus.UnitTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010035e2369b74530a07a7910ea41b965b4daa57b4cd92f20d3e27ab470cdbf7c68c9c314d400448071dc7312e65e3281b089bfac9fc2b258048c406d81fa0bdb01a1bc0cd5b1430fc0bd990eb14efbe684400b2bb096bd678b1b8fabe27b9daebbdbb74b44b679258622773731d2a14e15da281d83bd23c02d5d1baa8f38da86fdb")]
