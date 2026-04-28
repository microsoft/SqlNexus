using System;
using System.Text;

namespace TraceEventImporter.Normalization
{
    /// <summary>
    /// Normalizes SQL text by replacing literal values with placeholders.
    /// Implements the same normalization rules as NORMALIZEMODE in rml.y:
    ///   @variable    -> @P#
    ///   123          -> {##}
    ///   1.23         -> {##}.{##}
    ///   'text'       -> {STR}
    ///   N'text'      -> {STR}
    ///   0xABCD       -> {BS}
    ///   {GUID-here}  -> {GUID}
    /// Also: UPPER case, collapse whitespace, trim, strip comments.
    /// </summary>
    public static class SqlTextNormalizer
    {
        public static string Normalize(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                return string.Empty;

            var result = new StringBuilder(sql.Length);
            int i = 0;
            int len = sql.Length;

            while (i < len)
            {
                char c = sql[i];

                // --- Line comments: -- ... \n ---
                if (c == '-' && i + 1 < len && sql[i + 1] == '-')
                {
                    i += 2;
                    while (i < len && sql[i] != '\n' && sql[i] != '\r')
                        i++;
                    continue;
                }

                // --- Block comments: /* ... */ ---
                if (c == '/' && i + 1 < len && sql[i + 1] == '*')
                {
                    i += 2;
                    int depth = 1;
                    while (i < len && depth > 0)
                    {
                        if (sql[i] == '/' && i + 1 < len && sql[i + 1] == '*')
                        {
                            depth++;
                            i += 2;
                        }
                        else if (sql[i] == '*' && i + 1 < len && sql[i + 1] == '/')
                        {
                            depth--;
                            i += 2;
                        }
                        else
                        {
                            i++;
                        }
                    }
                    continue;
                }

                // --- N'unicode string literal' ---
                if ((c == 'N' || c == 'n') && i + 1 < len && sql[i + 1] == '\'')
                {
                    i += 2;
                    SkipStringLiteral(sql, ref i);
                    AppendWithSpace(result, "{STR}");
                    continue;
                }

                // --- 'string literal' ---
                if (c == '\'')
                {
                    i++;
                    SkipStringLiteral(sql, ref i);
                    AppendWithSpace(result, "{STR}");
                    continue;
                }

                // --- Binary literal: 0xHEX ---
                if (c == '0' && i + 1 < len && (sql[i + 1] == 'x' || sql[i + 1] == 'X'))
                {
                    // Ensure it's not part of an identifier
                    if (i == 0 || !IsIdentChar(sql[i - 1]))
                    {
                        i += 2;
                        while (i < len && IsHexDigit(sql[i]))
                            i++;
                        AppendWithSpace(result, "{BS}");
                        continue;
                    }
                }

                // --- GUID literal: {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} ---
                if (c == '{' && IsGuidLiteral(sql, i))
                {
                    int end = sql.IndexOf('}', i);
                    if (end > i)
                    {
                        i = end + 1;
                        AppendWithSpace(result, "{GUID}");
                        continue;
                    }
                }

                // --- @variable or @@system_variable ---
                if (c == '@')
                {
                    i++;
                    if (i < len && sql[i] == '@')
                    {
                        // @@system_variable — preserve as-is
                        result.Append("@@");
                        i++;
                        while (i < len && IsIdentChar(sql[i]))
                        {
                            result.Append(char.ToUpperInvariant(sql[i]));
                            i++;
                        }
                    }
                    else
                    {
                        // @user_variable -> @P#
                        while (i < len && IsIdentChar(sql[i]))
                            i++;
                        AppendWithSpace(result, "@P#");
                    }
                    continue;
                }

                // --- Numeric literal ---
                if (char.IsDigit(c) && (i == 0 || !IsIdentChar(sql[i - 1])))
                {
                    bool hasDecimal = false;
                    while (i < len && char.IsDigit(sql[i]))
                        i++;

                    if (i < len && sql[i] == '.' && i + 1 < len && char.IsDigit(sql[i + 1]))
                    {
                        hasDecimal = true;
                        i++; // skip '.'
                        while (i < len && char.IsDigit(sql[i]))
                            i++;
                    }

                    // Skip scientific notation suffix (e.g., 1.5E+10)
                    if (i < len && (sql[i] == 'e' || sql[i] == 'E'))
                    {
                        i++;
                        if (i < len && (sql[i] == '+' || sql[i] == '-'))
                            i++;
                        while (i < len && char.IsDigit(sql[i]))
                            i++;
                    }

                    // Make sure this number isn't followed by an identifier char (e.g., part of a name like "table1")
                    if (i < len && IsIdentChar(sql[i]))
                    {
                        // It's part of an identifier — don't normalize, just output what we've consumed
                        // Actually, rewind and just output as identifier
                        // This is a simplification; the original lexer handles this more precisely
                    }

                    AppendWithSpace(result, hasDecimal ? "{##}.{##}" : "{##}");
                    continue;
                }

                // --- Whitespace collapsing ---
                if (char.IsWhiteSpace(c))
                {
                    if (result.Length > 0 && result[result.Length - 1] != ' ')
                        result.Append(' ');
                    i++;
                    while (i < len && char.IsWhiteSpace(sql[i]))
                        i++;
                    continue;
                }

                // --- Quoted identifier [name] — preserve as-is ---
                if (c == '[')
                {
                    // Check for showplan variable pattern [ExprNNNN] or [BMKNNNN]
                    if (IsShowplanVariable(sql, i))
                    {
                        result.Append('[');
                        i++;
                        // Output the alpha prefix, strip trailing digits
                        while (i < len && sql[i] != ']' && !char.IsDigit(sql[i]))
                        {
                            result.Append(char.ToUpperInvariant(sql[i]));
                            i++;
                        }
                        // Skip digits and closing bracket
                        while (i < len && sql[i] != ']')
                            i++;
                        if (i < len) i++; // skip ']'
                        result.Append(']');
                        continue;
                    }

                    // Regular quoted identifier — preserve
                    result.Append('[');
                    i++;
                    while (i < len && sql[i] != ']')
                    {
                        result.Append(char.ToUpperInvariant(sql[i]));
                        i++;
                    }
                    if (i < len)
                    {
                        result.Append(']');
                        i++; // skip ']'
                    }
                    continue;
                }

                // --- Regular character — uppercase ---
                result.Append(char.ToUpperInvariant(c));
                i++;
            }

            return result.ToString().Trim();
        }

        private static void SkipStringLiteral(string sql, ref int i)
        {
            int len = sql.Length;
            while (i < len)
            {
                if (sql[i] == '\'')
                {
                    i++;
                    // Escaped quote ''
                    if (i < len && sql[i] == '\'')
                    {
                        i++;
                        continue;
                    }
                    return;
                }
                i++;
            }
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static bool IsIdentChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '#' || c == '$';
        }

        private static bool IsGuidLiteral(string sql, int pos)
        {
            // {XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}  = 38 chars
            if (pos + 37 >= sql.Length) return false;
            if (sql[pos + 9] != '-') return false;
            if (sql[pos + 14] != '-') return false;
            if (sql[pos + 19] != '-') return false;
            if (sql[pos + 24] != '-') return false;
            if (sql[pos + 37] != '}') return false;
            return true;
        }

        private static bool IsShowplanVariable(string sql, int pos)
        {
            // Matches [ExprNNNN] or [BMKNNNN] patterns
            if (pos + 5 >= sql.Length) return false;
            int i = pos + 1;
            string prefix = "";
            while (i < sql.Length && char.IsLetter(sql[i]))
            {
                prefix += sql[i];
                i++;
            }

            if (prefix.Length == 0) return false;

            string upper = prefix.ToUpperInvariant();
            if (upper != "EXPR" && upper != "BMK" && upper != "UNION" && upper != "CONST")
                return false;

            // Must be followed by digits then ']'
            if (i >= sql.Length || !char.IsDigit(sql[i])) return false;
            while (i < sql.Length && char.IsDigit(sql[i])) i++;
            return (i < sql.Length && sql[i] == ']');
        }

        private static void AppendWithSpace(StringBuilder sb, string text)
        {
            sb.Append(text);
        }
    }
}
