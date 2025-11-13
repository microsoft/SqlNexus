using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace sqlnexus
{
    public static class ScriptIntegrityChecker
    {
        // Store expected hashes for each allowed script

        private static readonly Dictionary<string, string> ScriptHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
          { "PerfStatsAnalysis.sql", "37111E6F2052A2A7B15E26329475738B4E543FE66AFB7BA426639C062D9E81A1" },
          { "SQLNexus_PostProcessing.sql", "BA659CE90DD602AD16C5A8F131D95C1A7D86AA00D764C68C3DE176C5AD0A4139" },
          { "SQLNexus_PreProcessing.sql", "81465871D11C26E93329C5F60CBACED1311E97205B29CD8E5526273018168FF6" },
          { "PostBuild.cmd", "741ABE8E8750EE4F010268B29C08B645EAB3EAE4E805D46CD5CA100926E00A48" },
          { "PostProcess.cmd", "814ABCA4622978440172394F24AD6C81535D0C78E53D4356642704A95CD38C7F" },
          { "PreBuild.cmd", "9C706DD338C5A3743C176E43F2C35FE765CF4719FBF33AF6FDAA811418B01187" },
      };
        // Returns true only if the file is listed and the hash matches
        public static bool VerifyScript(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            if (!ScriptHashes.ContainsKey(fileName))
                return false;

            string expectedHash = ScriptHashes[fileName];
            string actualHash = ComputeFileHash(filePath);

            return string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase);
        }

        // Returns true only if the file is listed in the dictionary
        public static bool IsScriptAllowed(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            return ScriptHashes.ContainsKey(fileName);
        }

        private static string ComputeFileHash(string filePath)
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
            catch (Exception)
            {
                return null;
            }
        }
    }
}