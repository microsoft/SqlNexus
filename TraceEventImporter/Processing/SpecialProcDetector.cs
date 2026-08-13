using System;
using System.Collections.Generic;

namespace TraceEventImporter.Processing
{
    /// <summary>
    /// Detects the 17 special stored procedures that get special handling during
    /// normalization and hash computation. The SpecialProcID is used as a seed
    /// component in the StringHash function.
    ///
    /// IMPORTANT: These SpecialProcID values are IMPORTER-LOCAL and intentionally
    /// differ from the canonical SpecialProcs enum used by native ReadTrace.exe
    /// (RMLSupp\CommonFiles\CommonDefines.h — e.g. there sp_executesql = 13,
    /// sp_prepare = 10, sp_prepexec = 14). This importer never needs to match those
    /// numeric values: the same ID is used consistently as both the StringHash seed
    /// and the tblUniqueBatches.SpecialProcID stored value (joined to
    /// tblProcedureNames.SpecialProcID), so the data is internally consistent.
    /// The resulting HashIDs therefore will NOT match those produced by ReadTrace.exe,
    /// which is acceptable because the two importers are never mixed within one import.
    /// If byte-for-byte parity with ReadTrace.exe is ever required, remap the values
    /// below to the espXxx enum numbering.
    /// </summary>
    public static class SpecialProcDetector
    {
        private static readonly Dictionary<string, byte> SpecialProcs =
            new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            { "sp_prepare",            1 },
            { "sp_execute",            2 },
            { "sp_executesql",         3 },
            { "sp_prepexec",           4 },
            { "sp_cursoropen",         5 },
            { "sp_cursorclose",        6 },
            { "sp_cursorfetch",        7 },
            { "sp_cursorexecute",      8 },
            { "sp_cursorprepare",      9 },
            { "sp_cursorprepexec",    10 },
            { "sp_cursorunprepare",   11 },
            { "sp_cursor",            12 },
            { "sp_unprepare",         13 },
            { "sp_getschemalock",     14 },
            { "sp_releaseschemalock", 15 },
            { "sp_reset_connection",  16 },
            { "sp_refreshview",       17 },
        };

        /// <summary>
        /// Returns the SpecialProcID for a stored procedure name, or 0 if not special.
        /// Handles fully qualified names (e.g., "master.dbo.sp_executesql").
        /// </summary>
        public static byte GetSpecialProcId(string procName)
        {
            if (string.IsNullOrEmpty(procName))
                return 0;

            // Handle fully qualified names: extract the last part
            string simpleName = procName;
            int lastDot = procName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < procName.Length - 1)
                simpleName = procName.Substring(lastDot + 1);

            if (SpecialProcs.TryGetValue(simpleName, out byte id))
                return id;

            return 0;
        }

        /// <summary>
        /// Checks if an RPC event's object name is a special procedure.
        /// </summary>
        public static bool IsSpecialProc(string procName)
        {
            return GetSpecialProcId(procName) != 0;
        }
    }
}
