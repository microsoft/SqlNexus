#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace SqlNexus.McpServer
{
    /// <summary>
    /// Verifies the integrity of the AI guidance files (SQL Nexus skill files and the diagnostic
    /// agent definition) that drive the MCP server's Responsible AI behaviour.
    ///
    /// Modelled on <c>sqlnexus.ScriptIntegrityChecker</c>: each protected file is listed explicitly
    /// by name with an expected SHA-256 hash. If any file is missing, unreadable, not in the allow
    /// list, or its hash does not match, the check fails. The MCP server calls
    /// <see cref="VerifyAll"/> at startup and refuses to run if tampering is detected, surfacing an
    /// explicit error to the user.
    /// </summary>
    public static class FileIntegrityChecker
    {
        // Expected SHA-256 hashes keyed by the file's REPOSITORY-RELATIVE PATH (case-insensitive).
        // Using a relative path (rather than a bare file name) lets any protected file live in any
        // folder, so new files can be added in the future regardless of location. The absolute path
        // is resolved at runtime relative to the repository root, so the check works regardless of
        // where the built server executable lives. Use forward slashes here; they are normalised to
        // the platform separator at verification time.
        private static readonly Dictionary<string, string> ProtectedFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // NOTE: The diagnostic agent definition (.github/agents/sql-nexus-diagnostic.agent.md) is
            // intentionally NOT hash-protected. VS Code (and other MCP hosts) REWRITE that file's
            // frontmatter 'tools:' list whenever the user toggles a tool's enable/disable checkbox, so
            // a fixed-hash check on it is unworkable — every checkbox change would fail startup. The
            // repository root is still located via the .github/agents *directory* (see ResolveRepositoryRoot),
            // so removing the file hash does not affect root resolution. The AI skill files below are
            // never edited by the client and remain protected.

            // AI skill files (under AI/Skills).
            { "AI/Skills/analysis-summary-queries.md",              "C0891753FDB44D207BCAAD88F42522D7C0AE04ECF1AEA6EFA11EC8B2E82E2C97" },
            { "AI/Skills/blocking-queries.md",                      "B5DBD3E2DFFFF1DD527C0061DD2E2C5A1DB2A53A950EADA93C3BB2FD22DA90E3" },
            { "AI/Skills/bottleneck-analysis-queries.md",           "A3260DEEF551D98433A25ED6CE842543E11ADAF570461EAE78F2886EC9E848F7" },
            { "AI/Skills/report-map.md",                            "3EE8B67BC24684A724FB8F9D93C4607F2066BE70F371AD49FFEDA62918743631" },
            { "AI/Skills/scenario-application-analysis.md",         "38C12E100BEB5B857857D54DFED8726618EB0821890DA0571C86F2C0B37B042A" },
            { "AI/Skills/scenario-blocking.md",                     "642D27A3BEDFD8A3FC23F090D5ABD7E694BB69B4E8E0F27E486770D0FB2F9BFC" },
            { "AI/Skills/scenario-comparative-analysis.md",         "CC94F27A38681249B305DD95A85FF0A9392CE1FC6CC8E2A6323FC46EEBD23D56" },
            { "AI/Skills/scenario-database-comparison.md",          "4B9333EF92F4ED157219FEA70870150654B93049016AC5510E24E25391B76321" },
            { "AI/Skills/scenario-cpu.md",                          "159FE9EB4C7E8707598A4283D45E349C82831144AE36988971D8851D720D53B8" },
            { "AI/Skills/scenario-hadr.md",                         "883C905E611ACD804C60E835D8EB544B88A1A4DA7EE82C7CED2430BBC44ED4D5" },
            { "AI/Skills/scenario-index-optimization.md",           "2713DD9918950FA3CDD7D495687DDE8C652E029F2FA692C7BCA93988B693101A" },
            { "AI/Skills/scenario-io.md",                           "3E2C1FCFD86E6FEF9DDA31628F7A00C286FE00A5AB87369A4D8FF74414F443D0" },
            { "AI/Skills/scenario-memory.md",                       "253CB40AE8A6EB06480782EB99E41D287976C54D6D7CD29952B65F8DB6099C10" },
            { "AI/Skills/scenario-performance.md",                  "C8D2718D54C3313C2B33C90E88D3278027C0B2CB7A2BD4CE061CF420B3F6FA52" },
            { "AI/Skills/scenario-query-deepdive-wait-analysis.md", "E3F8CF314B7913FCC567A78E870EEA95A7EB517A6CED3372291549B8C91D37D3" },
            { "AI/Skills/scenario-setup.md",                        "D62A1D76376ADDEA78C2947DD57228194190E431396B58E99061968027C45111" },
            { "AI/Skills/scenario-utility-diagnostics.md",          "A184BBF7CD6F21190ACFBAEAB93065CF0A4B1CC325EB4BB879908761F5E8ABE4" },
            { "AI/Skills/symptom-quick-reference.md",               "DB22D0D15DA9230467C458685D0AED64621100151E99CAE286B2E4BA057C5E05" },
            { "AI/Skills/wait-analysis-queries.md",                 "483AC72DCC582C49A03DEA06B9405967E65F70E57C6F2744F7A8571AE053B254" },
        };

        // Repository-relative marker directories used to locate the repository root.
        private static readonly string SkillsRelativeDir = Path.Combine("AI", "Skills");
        private static readonly string AgentRelativeDir = Path.Combine(".github", "agents");

        private static void Log(string msg) => Logger.Info("[FileIntegrity] " + msg);

        /// <summary>
        /// Verifies every protected AI guidance file. Returns true only if the repository root can be
        /// located and all listed files are present with a matching hash. On failure, <paramref name="error"/>
        /// contains an explicit, user-facing message describing which file failed and why.
        /// </summary>
        public static bool VerifyAll(out string error)
        {
            string? root = ResolveRepositoryRoot();
            if (root == null)
            {
                error = "SQL Nexus MCP Server cannot start: unable to locate the AI guidance folder "
                      + $"('{SkillsRelativeDir}'). The installation appears incomplete or has been moved. "
                      + "Restore the original SQL Nexus files and try again.";
                Log(error);
                return false;
            }

            Log($"Verifying AI guidance file integrity under: {root}");

            var failures = new List<string>();

            foreach (var kvp in ProtectedFileHashes)
            {
                // Normalise forward slashes in the relative key to the platform separator.
                string relative = kvp.Key.Replace('/', Path.DirectorySeparatorChar);
                string path = Path.Combine(root, relative);
                VerifyOne(path, kvp.Value, failures);
            }

            if (failures.Count > 0)
            {
                error = "SQL Nexus MCP Server cannot start: one or more AI guidance files have been "
                      + "tampered with, are missing, or are unreadable. To protect the integrity of the "
                      + "diagnostic analysis, the server will not run until the original files are restored.\r\n"
                      + "Affected file(s):\r\n  - " + string.Join("\r\n  - ", failures);
                Log(error);
                return false;
            }

            Log("All AI guidance files passed the integrity check.");
            error = string.Empty;
            return true;
        }

        // Verifies a single file, appending a human-readable reason to <paramref name="failures"/> on any problem.
        private static void VerifyOne(string filePath, string expectedHash, List<string> failures)
        {
            if (!File.Exists(filePath))
            {
                failures.Add($"{filePath} (missing)");
                Log($"Missing file: {filePath}");
                return;
            }

            string? actualHash = ComputeFileHash(filePath);
            if (actualHash == null)
            {
                failures.Add($"{filePath} (unreadable)");
                return;
            }

            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{filePath} (hash mismatch \u2013 file modified)");
                Log($"Hash mismatch for '{filePath}'. Expected={expectedHash} Actual={actualHash}");
                return;
            }

            Log($"OK: {filePath}");
        }

        /// <summary>
        /// Locates the repository root by walking up from the executable location and the current
        /// working directory, looking for the folder that contains the protected AI guidance layout
        /// (both <c>AI\Skills</c> and <c>.github\agents</c>).
        /// </summary>
        private static string? ResolveRepositoryRoot()
        {
            var startPoints = new List<string?>
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Directory.GetCurrentDirectory()
            };

            foreach (var start in startPoints)
            {
                if (string.IsNullOrEmpty(start))
                    continue;

                var dir = new DirectoryInfo(start!);
                while (dir != null)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, SkillsRelativeDir))
                        && Directory.Exists(Path.Combine(dir.FullName, AgentRelativeDir)))
                    {
                        return dir.FullName;
                    }
                    dir = dir.Parent;
                }
            }

            return null;
        }

        private static string? ComputeFileHash(string filePath)
        {
            try
            {
                using (var stream = File.OpenRead(filePath))
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "");
                }
            }
            catch (Exception ex)
            {
                Log($"Error computing hash for '{filePath}': {ex.Message}");
                return null;
            }
        }
    }
}
