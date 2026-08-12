---
applyTo: "**/*.cs"
---

# C# Instructions — SqlNexus

These rules apply to all C# source files. They complement the repository-wide
`.github/copilot-instructions.md`.

## Language and framework
- Target **C# 7.3** on **.NET Framework 4.8** — do not use language or BCL features newer than
  what C# 7.3 / .NET Framework 4.8 supports.
- Use only APIs available in .NET Framework 4.8 — do **not** suggest .NET Core / .NET 5+ APIs.
- Prefer `Microsoft.Data.SqlClient` over `System.Data.SqlClient` for all new SQL connectivity code.

## Code style
- Follow existing naming conventions: `PascalCase` for methods and properties, `camelCase` for
  local variables, `m_` prefix for private fields where already established.
- Match the indentation and brace style of the file being edited.
- Add comments only where they match the style of existing comments or explain genuinely
  non-obvious logic.
- Keep changes minimal and focused; do not reformat unrelated code.
- Do not introduce new NuGet packages without explicit instruction.

## Exception handling
- Never leave a `catch` block empty. Every `catch` must log via `MainForm.LogMessage(...)` or
  `Util.Logger.LogMessage(...)`, rethrow, or return a meaningful failure value.
- Prefer `catch (Exception ex)` with a named variable over bare `catch {}`.
- Use `Globals.HandleException(ex, this, MainForm)` for unhandled exceptions in UI event handlers.
- Catch the most specific exception type (`SqlException`, `IOException`,
  `UnauthorizedAccessException`) for expected, recoverable failures.
- Do not use exceptions for control flow.

## Security
- Never construct SQL command text by concatenating user-supplied strings; use parameterized
  queries or stored procedures.
- Bracket-escape database/object names (`[dbname]`) before use.
- Do not log secrets, passwords, or connection strings with credentials.
