# SqlNexus Testing Infrastructure

This folder contains the automated test scaffolding for SqlNexus.

```
TestingInfrastructure/
  UnitTests/
    SqlNexus.UnitTests/          MSTest project (targets net48)
      <MirrorOfSourceProject>/   test folders mirror the code under test
```

## Goals

- Provide a home for unit tests so new code changes can be covered going forward.
- Keep test scaffolding separate from the product source and the installer/release tree.
- Mirror the source layout under the test project so coverage gaps are visible.

## Conventions

- **Framework:** MSTest (`[TestClass]` / `[TestMethod]`). Works in Visual Studio Test Explorer
  and in CI via `dotnet test`.
- **Target:** `net48` (matches the .NET Framework 4.8 product projects).
- **Naming:** `<TypeUnderTest>Tests.cs`; method names read `Scenario_ExpectedResult`.
- **Structure:** Arrange / Act / Assert; one logical assertion focus per test.
- **Folders:** mirror the source project namespace (e.g. tests for `RowsetImportEngine` live under
  `SqlNexus.UnitTests/RowsetImportEngine/`).

## Referencing product code

The example test (`RowsetImportEngine/DateTimeColumnTests.cs`) is currently self-contained
scaffolding. To test real production code:

1. Add a `ProjectReference` to the product project in `SqlNexus.UnitTests.csproj`
   (a commented example is included).
2. If you need to test `internal` members, add to the product project (e.g. in
   `AssemblyInfo.cs` or the csproj):
   `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SqlNexus.UnitTests")]`
3. Replace the local mirror helpers in the example with direct calls into the product type.

## Running tests

- **Visual Studio:** open Test Explorer and run.
- **Command line:** `dotnet test TestingInfrastructure/UnitTests/SqlNexus.UnitTests/SqlNexus.UnitTests.csproj`

## Adding the project to the solution

The test project is intentionally not yet added to `sqlnexus.sln` so the product build is
unaffected. To include it:

`dotnet sln sqlnexus.sln add TestingInfrastructure/UnitTests/SqlNexus.UnitTests/SqlNexus.UnitTests.csproj`

Consider a separate solution filter (`.slnf`) for CI test runs if you do not want the test
project in the main build.

## Guiding principles

All tests and the code they cover must honor the security, accessibility, and privacy
requirements described in [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md).
