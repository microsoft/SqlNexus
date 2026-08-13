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

        // Wrapper procedures whose first string argument is an inner SQL query.
        // Used to anchor extraction to a real proc-call prefix rather than guessing
        // from the position of the first quote (which would misfire on raw inner SQL
        // that merely contains a string literal, e.g. UPDATE t SET c = 'x').
        private static readonly System.Collections.Generic.HashSet<string> InnerSqlWrapperProcs =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "sp_executesql",
                "sp_prepare",
                "sp_prepexec",
                "sp_cursoropen",
                "sp_cursorprepare",
                "sp_cursorprepexec",
            };

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
        ///   sp_executesql N'SELECT ...'
        ///   master.dbo.sp_executesql N'SELECT ...'
        ///   [sp_executesql] N'SELECT ...'
        ///
        /// If the text is already the raw inner SQL (no wrapper proc prefix), null is
        /// returned so the caller normalizes the whole text.
        /// </summary>
        public static string TryExtractInnerSql(string textData)
        {
            if (string.IsNullOrEmpty(textData))
                return null;

            int len = textData.Length;
            int i = 0;

            // Skip leading whitespace.
            while (i < len && char.IsWhiteSpace(textData[i])) i++;

            // Skip an optional EXEC / EXECUTE keyword (must be followed by whitespace
            // so we don't swallow the start of an identifier such as "executesql").
            i = SkipOptionalKeyword(textData, i, "EXECUTE");
            i = SkipOptionalKeyword(textData, i, "EXEC");

            // Skip whitespace before the procedure name.
            while (i < len && char.IsWhiteSpace(textData[i])) i++;

            // Read the (possibly qualified / bracketed) procedure name token and confirm
            // it is one of the wrapper procs that carry inner SQL. If it isn't, the text
            // is not an sp_executesql-style wrapper (it may already be the inner SQL), so
            // return null and let the caller normalize the whole text.
            string procName = ReadProcNameToken(textData, ref i);
            if (procName == null || !InnerSqlWrapperProcs.Contains(procName))
                return null;

            // Skip whitespace between the proc name and its first argument.
            while (i < len && char.IsWhiteSpace(textData[i])) i++;

            // The first argument must be the SQL text string literal: N'...' or '...'.
            if (i < len && (textData[i] == 'N' || textData[i] == 'n')
                && i + 1 < len && textData[i + 1] == '\'')
            {
                return ExtractQuotedString(textData, i + 2);
            }
            if (i < len && textData[i] == '\'')
            {
                return ExtractQuotedString(textData, i + 1);
            }

            return null;
        }

        /// <summary>
        /// If <paramref name="text"/> at <paramref name="pos"/> begins with
        /// <paramref name="keyword"/> (case-insensitive) followed by whitespace, returns
        /// the position just after the keyword; otherwise returns <paramref name="pos"/>
        /// unchanged.
        /// </summary>
        private static int SkipOptionalKeyword(string text, int pos, string keyword)
        {
            int len = text.Length;
            if (pos + keyword.Length > len)
                return pos;

            if (string.Compare(text, pos, keyword, 0, keyword.Length,
                    StringComparison.OrdinalIgnoreCase) != 0)
                return pos;

            int after = pos + keyword.Length;
            // Must be at end or followed by whitespace to count as a standalone keyword.
            if (after < len && !char.IsWhiteSpace(text[after]))
                return pos;

            return after;
        }

        /// <summary>
        /// Reads a procedure-name token starting at <paramref name="i"/> (advancing it),
        /// stopping at whitespace or a quote. Returns the final, unbracketed part of a
        /// multi-part name (e.g. "master.dbo.[sp_executesql]" -> "sp_executesql"), or
        /// null if no token is present.
        /// </summary>
        private static string ReadProcNameToken(string text, ref int i)
        {
            int len = text.Length;
            int start = i;
            while (i < len)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c) || c == '\'')
                    break;
                i++;
            }

            if (i == start)
                return null;

            string token = text.Substring(start, i - start);

            // Take the last part of a qualified name (schema/db qualifiers dropped).
            int lastDot = token.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < token.Length - 1)
                token = token.Substring(lastDot + 1);

            // Strip surrounding brackets / quoted-identifier characters.
            token = token.Trim('[', ']', '"');

            return token.Length > 0 ? token : null;
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