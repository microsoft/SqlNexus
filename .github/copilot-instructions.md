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

## Project overview (projects and responsibilities)

SqlNexus is a Windows Forms desktop application for importing and analyzing SQL Server
diagnostic data (PSSdiag, SQL LogScout, XEL, TRC, Perfmon, Linux perf). Key projects:

- **sqlnexus** — Main WinForms host (UI, report viewer, theme engine, import orchestration)
- **NexusInterfaces** — Shared importer interfaces (`INexusImporter`, `INexusFileImporter`, etc.)
- **RowsetImportEngine** — Core T-SQL rowset importer
- **ReadTraceNexusImporter** — XEL/TRC trace importer (legacy)
- **PerfmonImporter** — Windows Performance Monitor (.blg/.csv) importer
- **LinuxPerfImporter** — Linux performance data importer
- **ErrorLogImporter** — SQL Server error log importer
- **BulkLoadEx** — Native bulk-load helper
- **TraceEventImporter** — New importer for XEL files using SQL Server's TraceEvent API XeLite
  (future replacement for ReadTraceNexusImporter)

Framework specifics:
- **Windows Forms** for all UI
- **Microsoft.Reporting.WinForms** (ReportViewer) for RDLC report rendering
- **Microsoft.Data.SqlClient** for all SQL Server connectivity (prefer over `System.Data.SqlClient`)
- Use only APIs available in .NET Framework 4.8 — do **not** suggest .NET Core / .NET 5+ APIs

## Architecture conventions

- All importers implement `INexusImporter` (and optionally `INexusFileImporter`,
  `INexusProgressReporter`) from `NexusInterfaces`
- Importer ordering is controlled in `fmImport.OrderedImporterFiles()` — TraceEventImporter (150)
  must run after RowsetImportEngine (100)
- ReadTrace and TraceEventImporter are mutually exclusive — never enable both simultaneously
- The `CustomXELImporter` handles SQLDiag, AlwaysOn, and System Health XEL files independently of
  the plugin importers
- Report parameters are set via `ReportParameter` objects; `ContrastTheme` must be propagated to
  every report that declares it
- `ThemeManager` owns all color definitions for the three themes: **Default**, **Aquatic**
  (`#202020` background), and **Desert** (`#FFFAEF` background)
- The `TopToolStripPanel` layout is order-sensitive:
  `menuBarMain ? toolbarService ? toolbarReport ? toolbarMain` (last added = topmost row)

## Security — CodeQL specifics

In addition to the SDL requirements above:

- Database names passed to SQL commands must be bracket-escaped (`[dbname]`) before use — see the
  `CodeQL [SM03934]` annotation pattern in `fmImport.cs`
- Use `ScriptIntegrityChecker.VerifyScript()` before executing any `.sql` or `.cmd` file on disk
- Validate all file paths before use; reject paths containing directory traversal sequences
- Do not use `Assembly.LoadFile` on untrusted paths without verification
- Do not log passwords or connection strings with credentials via `LogMessage`

## Accessibility — WinForms/WCAG specifics

In addition to the accessibility requirements above:

- Use `AccessibleTextBox` (the project's custom subclass) instead of plain `TextBox` for all
  user-editable fields — it implements the UIA Text pattern required for screen readers
- Disabled `Label` controls must not rely on WinForms' default disabled rendering (which ignores
  `ForeColor`) — keep labels `Enabled = true` and set a muted `ForeColor` via
  `ThemeManager.CurrentThemeName` to simulate the disabled appearance
- `ToolStripLabel` and `ToolStripComboBox` items must have `AccessibleName` set
- Submenus must remain open when the user toggles a checkbox option
  (`ToolStripDropDownCloseReason.ItemClicked` should be cancelled)
- Link labels must always use `LinkBehavior.AlwaysUnderline` for WCAG 1.4.1 compliance
- When Windows High Contrast mode is active (`SystemInformation.HighContrast`), defer to
  `SystemColors` rather than theme colors — see `ThemeManager.ApplyHighContrastTheme()`

## RDLC report theming

- Every report must declare a `ContrastTheme` parameter with valid values `None`, `Aquatic`, `Desert`
- Define exactly 11 theme variables: `ReportTextColor`, `BodyBackgroundColor`, `TitleColor`,
  `TableHeadingColor`, `TableHeadingFontColor`, `ChartColor`, `ChartSecondaryColor`,
  `ChartGradientStyle`, `TableShowCell`, `TableHidcell`, `URILinkFontColor`
- Replace all hardcoded colors in `<Style>` blocks with the appropriate variable reference:
  - Header `<BackgroundColor>` ? `=Variables!TableHeadingColor.Value`
  - Body `<BackgroundColor>` ? `=Variables!BodyBackgroundColor.Value`
  - Drillthrough/hyperlink `<Color>` ? `=Variables!URILinkFontColor.Value`
  - Chart area and chart background ? `=Variables!ChartColor.Value`
  - All `<TextRun>` font-styled text without an explicit color ? `=Variables!ReportTextColor.Value`
- All chart axes, titles, and legend text must have an explicit `<Color>` set to
  `=Variables!ReportTextColor.Value` so they remain readable in all contrast themes
- The "No Data Available" warning banner uses a `Gold` background — its text must always be
  `Black` regardless of theme

## Toolbar and settings persistence

- Toolbar visibility (`ShowStandardToolbar`, `ShowReportToolbar`, `ShowDataCollectionToolbar`) must
  be explicitly saved in `fmNexus_FormClosing` — DataBindings alone are not reliable
- `ShowHideUIElements()` must explicitly set `toolbarMain.Visible` from the persisted setting to
  guard against DataBinding desync during `InitializeComponent`
- `SelectLoadReport()` must read `Properties.Settings.Default.ShowReportToolbar` as the source of
  truth when setting `toolbarReport.Visible` — never blindly set it to `true`
- When iterating `TopToolStripPanel` rows, use `ToolStripPanel.Join(toolStrip, rowIndex)` to
  restore toolbar row position after database changes

## TextRowsets.xml — rowset definition standards

`TextRowsets.xml` (and its custom extension `TextRowsetsCustom.xml`) define how the
`RowsetImportEngine` maps sections of SQL Server diagnostic text output files into SQL tables.

### Structure
- The root element is `<TextImport>` containing a single `<KnownRowsets>` block
- Each rowset is a `<Rowset>` element with mandatory attributes: `name`, `enabled`, `identifier`,
  and `type`
- Column definitions go inside `<KnownColumns>` — columns not listed are imported as
  `VarCharColumn` by default

### Naming
- Table names (`name` attribute) must be prefixed with `tbl_`
- Column names must match exactly the column headers that appear in the source diagnostic text
  output — including spaces if present (e.g. `"Wait Time"`)

### Identifiers
- The `identifier` attribute is the exact string the engine uses to detect the start of this
  rowset in the text file — it must be unique across all rowsets in the file
- Use the actual header line from the diagnostic output script (e.g. `"-- sysperfinfo"`,
  `"-- sys.dm_os_memory_health_history --"`) — do not paraphrase it
- If the identifier could appear in multiple unrelated contexts, make it more specific

### Column types
Use the most appropriate type from `RowsetImportEngine` — do not default everything to
`VarCharColumn`:

| Type | Use for |
|---|---|
| `DateTimeColumn` | Any `runtime`, timestamp, or date/time column |
| `BigIntColumn` | `rownum`, large integer counters |
| `IntColumn` | SPIDs, counts, flags |
| `FloatColumn` | Decimal metrics (wait time, CPU %) |
| `DecimalColumn` | Precise decimal values |
| `VarCharColumn` | Short string identifiers, names |
| `NVarCharColumn` | Unicode text, query text, messages |
| `VarBinaryColumn` | Binary handles (e.g. `query_hash`) |

### Required columns
- In some cases may include `rownum` typed as `BigIntColumn` with `valuetoken="ROWNUMBER"` as the
  first column
- In some cases include `runtime` typed as `DateTimeColumn` with `valuetoken="RUNTIME"` as the
  second column — this is used by most reports to filter by time range

### Value tokens and define tokens
- `valuetoken` columns are populated by the engine from context (e.g. `ROWNUMBER`, `RUNTIME`,
  `SCRIPTNAME`, `USERNAME`, `IMPORTDATE`, `INPUTFILENAME`) — do not parse these from the text file
- `definetoken` columns extract a value from the identifier/header line itself and store it for use
  by `valuetoken` columns later in the same rowset

### Enabled flag
- Set `enabled="true"` for all rowsets that should be active by default
- Set `enabled="false"` only for rowsets that are experimental, deprecated, or conditionally
  loaded — always add a comment explaining why

### Security
- The `name` attribute becomes a SQL table name — it must not be constructed from user input and
  must only contain alphanumeric characters and underscores
- Column `length` attributes are used to size `VARCHAR`/`NVARCHAR` columns — always specify an
  explicit `length` for string columns; omitting it defaults to a short platform-defined length
  that may truncate data

## Exception handling

- Never leave a `catch` block empty — a silent catch hides bugs and makes failures impossible to
  diagnose
- Every `catch` block must do at least one of: log the exception via `MainForm.LogMessage(...)` or
  `Util.Logger.LogMessage(...)`, rethrow, or return a meaningful failure value
- Prefer `catch (Exception ex)` with a named variable over bare `catch {}` — the exception message,
  source, and stack trace should be accessible for logging
- Use `Globals.HandleException(ex, this, MainForm)` for unhandled exceptions in UI event handlers —
  it logs to both the silent log and the dialog, and sets `ExceptionEncountered = true`
- For expected, recoverable failures (e.g. file not found, SQL connection refused) catch the most
  specific exception type (`SqlException`, `IOException`, `UnauthorizedAccessException`) rather than
  the base `Exception`
- Do not swallow `SqlException` silently — always log `sqlex.Message` and, where appropriate,
  surface it to the user via `MessageOptions.Dialog`
- When an inner try/catch exists solely to protect a logging call (as in `Globals.HandleException`),
  fall back to `System.Diagnostics.Debug.WriteLine` so the failure is at minimum visible in the
  debugger output window
- Do not use exceptions for control flow — validate inputs before calling methods rather than
  relying on catching the resulting exception
