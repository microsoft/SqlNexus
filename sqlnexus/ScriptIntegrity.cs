using NexusInterfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace sqlnexus
{
    public static class ScriptIntegrityChecker
    {

        public static ILogger Logger { get; set; }

        // Central logging helper – falls back to Trace / Console if no logger yet
        private static void Log(string msg, bool verbose = false)
        {
            // Use local Logger if set, else Util.Logger
            var lg = Logger ?? Util.Logger;
            if (lg != null)
            {
                // Use Silent so it goes only to file (not status bar / dialogs)
                lg.LogMessage("[ScriptIntegrity] " + msg, verbose ? MessageOptions.Silent : MessageOptions.Silent);
            }
            else
            {
                System.Diagnostics.Trace.WriteLine(msg);
            }
        }

        // Store expected hashes for each allowed script

        private static readonly Dictionary<string, string> ScriptHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { Application.StartupPath + "\\" + "PerfStatsAnalysis.sql", "21D1F5BA061770206F648A9AE36DFE4C4E69AA5CD7CCBF91F9CBDF6F5C7A29F2" },
            { Application.StartupPath + "\\" + "ReadTracePostProcessing.sql", "9244659FFF95BCA5D5C118DDFDAB2E3D9138CAE913D2F52488A01CD72749E5B1" },
            { Application.StartupPath + "\\" + "ReadTraceReportValidate.sql", "92A575503905D2CABEE18D1804D1DCDCACD12FACD912B16E1040C923AB168E02" },
            { Application.StartupPath + "\\" + "SQLNexus_PostProcessing.sql", "E631283CF30456C26AD3DBE9C345832581C6BB0D91A2B187A98DBBD314ADD73A" },
            { Application.StartupPath + "\\" + "TraceTimestampPostProcessing.sql", "ED1D29CD45FBA42F01D96F99CF9E94695B5677C3DF4CCD9499097A829E01C804" },
            { Application.StartupPath + "\\" + "SQLNexus_PreProcessing.sql", "81465871D11C26E93329C5F60CBACED1311E97205B29CD8E5526273018168FF6" },
            { Application.StartupPath + "\\" + "PostBuild.cmd", "741ABE8E8750EE4F010268B29C08B645EAB3EAE4E805D46CD5CA100926E00A48" },
            { Application.StartupPath + "\\" + "PreBuild.cmd", "9C706DD338C5A3743C176E43F2C35FE765CF4719FBF33AF6FDAA811418B01187" }
        };


        // Returns true only if the file is listed and the hash matches
        public static bool VerifyScript(string filePath)
        {

            Log($"Computing hash for file: {filePath}");

            // Check if file is in the allowed list and get expected hash
            if (!ScriptHashes.TryGetValue(filePath, out string expectedHash))
            {
                Log($"Script '{filePath}' not in the allowed list. Blocked.");
                return false;
            }

            // Compute actual hash
            string actualHash = ComputeFileHash(filePath);


            if (actualHash == null)
            {
                Log($"Failed to compute hash for '{filePath}'.");
                return false;
            }

            if (!IsExpectedHash(filePath, actualHash))
            {
                Log($"Hash mismatch for '{filePath}'. Expected={expectedHash} Actual={actualHash}");
                return false;
            }

            Log($"Script '{filePath}' integrity OK.");
            return true;
        }

        // Returns true only if the file is listed in the dictionary
        public static bool IsScriptAllowed(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            return ScriptHashes.ContainsKey(fileName);
        }

        internal static bool IsExpectedHash(string scriptName, string actualHash)
        {
            if (String.IsNullOrWhiteSpace(scriptName) || String.IsNullOrWhiteSpace(actualHash))
                return false;

            string scriptPath = Path.Combine(Application.StartupPath, Path.GetFileName(scriptName));
            return ScriptHashes.TryGetValue(scriptPath, out string expectedHash)
                && String.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeFileHash(string filePath)
        {
            try
            {
                using (var stream = File.OpenRead(filePath))
                {
                    using (var sha = SHA256.Create())
                    {
                        byte[] hash = sha.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "");
                    }
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