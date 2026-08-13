---
applyTo: "**/*.sql"
---

# SQL / T-SQL Instructions — SqlNexus

These rules apply to all `.sql` files. They complement the repository-wide
`.github/copilot-instructions.md`.

## General
- Target SQL Server T-SQL. Scripts are executed against the SqlNexus import database.
- Verify scripts with `ScriptIntegrityChecker.VerifyScript()` before execution (handled by the
  host) — do not add code paths that bypass verification.

## Security
- Never build dynamic SQL by concatenating untrusted input. Use parameters (`sp_executesql` with
  typed parameters) when dynamic SQL is unavoidable.
- Bracket-escape all dynamic object names (`[dbname]`, `[schema].[table]`).
- Validate dynamic identifiers against a strict allowlist/regex before use.
- Do not embed credentials, connection strings, or secrets in scripts.

## Conventions
- Prefix table names created by importers with `tbl_`.
- Match existing formatting and casing conventions of neighbouring scripts.
- Prefer set-based operations over cursors where practical.
- Keep changes minimal and focused; do not reformat unrelated statements.
