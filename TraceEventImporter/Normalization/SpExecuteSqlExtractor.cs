using System;

namespace TraceEventImporter.Normalization
{
    /// <summary>
    /// Extracts the inner SQL query text from sp_executesql, sp_prepexec, and sp_prepare
    /// RPC calls. ReadTrace.exe does this via GetRPCParamText() to normalize/hash the
    /// actual query rather than the wrapper call.
    /// </summary>
    public static class SpExecuteSqlExtractor
    {
        // SpecialProcIDs that carry an inner SQL query as the first parameter
        private const byte SP_PREPARE_ID = 1;
        private const byte SP_EXECUTESQL_ID = 3;
        private const byte SP_PREPEXEC_ID = 4;
        private const byte SP_CURSOROPEN_ID = 5;
        private const byte SP_CURSORPREPARE_ID = 9;
        private const byte SP_CURSORPREPEXEC_ID = 10;

        /// <summary>
        /// Returns true if this SpecialProcID should have its inner SQL extracted.
        /// </summary>
        public static bool ShouldExtractInnerSql(byte specialProcId)
        {
            return specialProcId == SP_EXECUTESQL_ID
                || specialProcId == SP_PREPEXEC_ID
                || specialProcId == SP_PREPARE_ID
                || specialProcId == SP_CURSOROPEN_ID
                || specialProcId == SP_CURSORPREPARE_ID
                || specialProcId == SP_CURSORPREPEXEC_ID;
        }

        /// <summary>
        /// Extracts the inner SQL query from a sp_executesql/sp_prepexec/sp_prepare
        /// TextData string. Returns the inner SQL if found, otherwise returns null.
        /// 
        /// Expected formats:
        ///   exec sp_executesql N'SELECT ...', N'@p1 int', @p1=42
        ///   exec sp_executesql N'SELECT ...'
        ///   SELECT ...   (already extracted by trace infrastructure)
        /// </summary>
        public static string TryExtractInnerSql(string textData)
        {
            if (string.IsNullOrEmpty(textData))
                return null;

            // Find the first N' or ' which starts the SQL parameter
            int i = 0;
            int len = textData.Length;

            // Skip past the proc name to find the first string parameter
            // Look for N'...' or '...' pattern
            while (i < len)
            {
                // N'...' unicode string literal
                if (i < len - 1 && (textData[i] == 'N' || textData[i] == 'n') && textData[i + 1] == '\'')
                {
                    // Check if this is after the proc name (not part of it)
                    // by verifying we've passed at least one space/comma
                    if (i > 0 && IsAfterProcName(textData, i))
                    {
                        return ExtractQuotedString(textData, i + 2);
                    }
                }

                // '...' regular string literal
                if (textData[i] == '\'' && i > 0 && IsAfterProcName(textData, i))
                {
                    return ExtractQuotedString(textData, i + 1);
                }

                i++;
            }

            return null;
        }

        private static bool IsAfterProcName(string text, int pos)
        {
            // Walk backwards to check we're past whitespace/comma after the proc name
            int j = pos - 1;
            while (j >= 0 && (text[j] == ' ' || text[j] == '\t' || text[j] == ','))
                j--;
            // We should be past at least a few chars (the proc name)
            return j > 3;
        }

        private static string ExtractQuotedString(string text, int startAfterQuote)
        {
            var result = new System.Text.StringBuilder();
            int i = startAfterQuote;
            int len = text.Length;

            while (i < len)
            {
                if (text[i] == '\'')
                {
                    // Check for escaped quote ''
                    if (i + 1 < len && text[i + 1] == '\'')
                    {
                        result.Append('\'');
                        i += 2;
                        continue;
                    }
                    // End of string
                    break;
                }
                result.Append(text[i]);
                i++;
            }

            string inner = result.ToString().Trim();
            return inner.Length > 0 ? inner : null;
        }
    }
}