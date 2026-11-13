using System;

namespace SqlNexus.UnitTests.SqlNexus.McpServer
{
    /// <summary>
    /// Test passwords are generated at run time so that no password literal is committed to source
    /// control. The value contains characters ('!' and '#') that need care in command lines and
    /// scripts, so the quoting and stdin paths are still exercised.
    /// </summary>
    internal static class TestSecrets
    {
        internal static string NewPassword()
        {
            return "Pw!" + Guid.NewGuid().ToString("N") + "#";
        }
    }
}
