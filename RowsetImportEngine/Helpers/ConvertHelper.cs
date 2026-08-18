using System;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Windows.Forms;


namespace RowsetImportEngine.Helpers
{
    public static class ConvertHelper
    {
        /// <summary>
        /// Coerce a value bound for a SQL Server 'datetime'/'smalldatetime' column so that it never
        /// throws "SqlDateTime overflow" during SqlBulkCopy. A .NET DateTime is valid from year 0001,
        /// but SQL 'datetime' only accepts 1753-01-01 .. 9999-12-31. A single out-of-range value
        /// (e.g. a malformed timestamp in the source diagnostic file, or a misaligned fixed-width
        /// column in a first-seen rowset) would otherwise abort the entire rowset bulk-load.
        /// Out-of-range values are treated as NULL so one bad row cannot fail the whole load.
        /// </summary>
        /// <param name="value">The candidate column value (may be a DateTime, string, null, etc.).</param>
        /// <param name="coerced">
        /// The value to store: the original value when it is in range or not a DateTime, or null when
        /// the value is a DateTime outside the SQL 'datetime' range.
        /// </param>
        /// <returns>True if the value was coerced to null because it was out of range; otherwise false.</returns>
        public static bool CoerceToSqlDateTimeRange(object value, out object coerced)
        {
            coerced = value;
            if (value is DateTime)
            {
                DateTime dt = (DateTime)value;
                if (dt < SqlDateTime.MinValue.Value || dt > SqlDateTime.MaxValue.Value)
                {
                    coerced = null;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Normalize string values before assigning to SQL-bound DataRow columns by trimming and,
        /// when a bounded max length is enforced by the destination schema, truncating safely.
        /// </summary>
        /// <param name="value">Input string value.</param>
        /// <param name="maxLength">Destination max length (DataColumn.MaxLength).</param>
        /// <param name="wasTruncated">True when truncation occurred.</param>
        /// <returns>Trimmed/truncated string (or null).</returns>
        public static string NormalizeStringForSql(string value, int maxLength, out bool wasTruncated)
        {
            wasTruncated = false;
            if (value == null)
                return null;

            string normalized = value.Trim();
            if (maxLength > 0 && normalized.Length > maxLength)
            {
                wasTruncated = true;
                normalized = normalized.Substring(0, maxLength);
            }

            return normalized;
        }

        public static object ValidateData<T>(object columndata, out object data)
        {
            //IsValid() and ConvertFrom() only work if you do valid converts from one data type to another.
            //Those FAIL if you attempt to convert from one type to the same type (e.g. converting Int64 to Int64 fails)

            try
            {
                data = null;
                var converter = TypeDescriptor.GetConverter(typeof(T));

                if (converter == null || columndata == null)
                    return data; // if any of these null, return before its too late

                // Handle decimal and double with "comma" AND "dot" as decimal separator, handling this here instead of having the exception later and catching it.
                if ((converter is System.ComponentModel.DecimalConverter || converter is System.ComponentModel.DoubleConverter))
                {
                    if (columndata.ToString().Contains(",")) //is it comma decimal separator?
                    {
                        columndata = columndata.ToString().Replace(",", "."); //replace comma with dot as InvariantCulture uses dot
                    }
                    data = (T)converter.ConvertFromInvariantString(columndata.ToString());
                }

                else if (converter.IsValid(columndata))
                {
                    //our standart convert , path for int , datetime ,etc
                    data = (T)converter.ConvertFrom(columndata);
                }

                else if (converter.IsValid(columndata.ToString()))
                {
                    // which scenario ends here ?
                    data = (T)converter.ConvertFrom(columndata.ToString());                    
                }
                return data;
            }
            catch
            {
                // Some unknown exception.
                // Set to null otherwise.                
                data = null;
                return null;

            }
        }
    }
}


