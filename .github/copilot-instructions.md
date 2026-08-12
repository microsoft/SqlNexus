# GitHub Copilot Instructions for SqlNexus

These instructions apply to all AI-assisted code changes in this repository. Follow them for
every contribution, now and in the future.

## Project context

- **Language / runtime:** C# 7.3 on **.NET Framework 4.8** (legacy, non-SDK product projects).
- **Solution:** `sqlnexus.sln` with multiple importer/engine assemblies (e.g. `sqlnexus`,
  `RowsetImportEngine`, `NexusInterfaces`, `PerfmonImporter`, `ErrorLogImporter`,
  `BulkLoadEx`, `ReadTraceNexusImporter`, `LinuxPerfImporter`, `TraceEventImporter`).
- **Purpose:** SqlNexus imports SQL Server diagnostic data (PSSDIAG/SQLLogScout output) into a
  SQL Server database and runs reports over it.

## Unit testing requirement (mandatory)

**Every code change that adds or modifies behavior MUST be accompanied by unit tests.**

- Place tests in `TestingInfrastructure/UnitTests/SqlNexus.UnitTests/`, in a subfolder that
  mirrors the source project/namespace being tested.
- Use **MSTest** (`[TestClass]` / `[TestMethod]`), target `net48`.
- Name files `<TypeUnderTest>Tests.cs`; name methods `Scenario_ExpectedResult`.
- Follow **Arrange / Act / Assert**. Keep tests deterministic, isolated, and fast (no live SQL
  Server, network, or file-system dependencies unless explicitly mocked or using temp fixtures).
- Cover, at minimum:
  - The **happy path**.
  - **Boundary/edge cases** (empty, null, min/max, out-of-range, malformed input).
  - **Negative cases** (invalid input is rejected/handled, not silently accepted).
  - Any **bug being fixed** — add a regression test that fails before the fix and passes after.
- If the code under test uses `internal` members, prefer adding
  `[assembly: InternalsVisibleTo("SqlNexus.UnitTests")]` to the product project rather than
  making members public solely for testing.
- When you cannot fully wire a test to production code, still add a clearly-labeled scaffolding
  test and a TODO explaining the required wiring (project reference / InternalsVisibleTo).

## Security requirements (Microsoft SDL — always build in this spirit)

- **No injection.** Never build SQL, shell, or file paths via string concatenation of untrusted
  input. Use parameterized `SqlCommand` parameters. For dynamic object names, validate against a
  strict allowlist/regex (see existing `IsSafeSqlIdentifier`).
- **Least privilege / explicit allowlisting.** Prefer explicit, code-controlled allowlists over
  dynamic discovery for anything that executes code (e.g. importer selection). Do not auto-execute
  drop-in/unknown assemblies.
- **Validate all external input** (command-line args, file contents, config, DB results). Reject
  unknown/empty tokens explicitly; fail closed, not open.
- **Secrets:** never log connection strings, passwords, or credentials. Mask sensitive values.
- **Safe defaults:** new options should default to the most secure behavior.
- **Error handling:** do not swallow exceptions silently in a way that hides failures; surface
  failures in status/results (do not report success when a step failed).
- **Dependencies:** avoid adding new packages unless necessary; prefer maintained, trusted ones.

## Privacy requirements (always build in this spirit)

- **Data minimization.** Only read/store/transmit the diagnostic data needed for the feature.
- **No telemetry of user content.** Do not add logging that emits personal data, server names,
  credentials, query text, or customer data to external sinks. Diagnostic logs should stay local.
- **Be explicit about data handling.** If a change imports or persists new data, document what is
  stored and why in the PR description.
- **Redaction:** when logging for diagnostics, prefer counts, table names, and status over raw row
  content or identifiers.

## Accessibility requirements (Microsoft accessibility standards — always build in this spirit)

For any WinForms UI changes:

- Provide `AccessibleName` and `AccessibleDescription` for meaningful controls.
- Ensure keyboard navigation works (tab order, mnemonics/access keys, Enter/Space activation).
- Do not convey information by color alone; pair color with text/iconography.
- Respect system theme/high-contrast; avoid hard-coded colors that break contrast (use the
  existing `ThemeManager` patterns).
- Ensure labels are associated with their inputs and that status/progress is announced via text,
  not only visual cues.

## Coding conventions

- Match the existing style of the file/project you are editing.
- Keep changes minimal and focused; do not reformat unrelated code.
- Prefer clear names; add comments only where they clarify non-obvious intent (as the codebase does).
- Do not introduce new warnings; keep the build clean (`net48`, C# 7.3 language features only).

## Definition of done for a change

1. Behavior implemented with minimal, focused edits.
2. Unit tests added/updated and passing (including a regression test for any bug fix).
3. Security, privacy, and accessibility considerations addressed (or explicitly noted as N/A).
4. Build is green; no new warnings.
5. PR description explains the change, any data-handling implications, and test coverage.
