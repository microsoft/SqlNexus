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

The tests reference real production code via `ProjectReference` entries in
`SqlNexus.UnitTests.csproj` (currently `RowsetImportEngine` and `sqlnexus`). To test
additional product code:

1. Add a `ProjectReference` to the product project in `SqlNexus.UnitTests.csproj`.
2. If you need to test `internal` members, add to the product project (e.g. in
   `AssemblyInfo.cs` or the csproj):
   `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SqlNexus.UnitTests")]`
   (the `sqlnexus` project already does this.)
3. Call directly into the product type from the test.

## Running tests

- **Visual Studio:** open Test Explorer and run.
- **Command line:** `dotnet test TestingInfrastructure/UnitTests/SqlNexus.UnitTests/SqlNexus.UnitTests.csproj`

> Note: the legacy WinForms `sqlnexus` project may not build under the `dotnet` SDK
> toolchain on all machines (non-string WinForms resources). If `dotnet test` fails to
> build it, build the solution in Visual Studio and run the tests from Test Explorer.

## Solution membership

The test project is included in `sqlnexus.sln`. The product build is unaffected because
the test project is separate and only references product assemblies.

## Guiding principles

All tests and the code they cover must honor the security, accessibility, and privacy
requirements described in [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md).
