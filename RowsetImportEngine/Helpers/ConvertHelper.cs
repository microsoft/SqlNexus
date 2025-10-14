﻿using System.ComponentModel;


namespace RowsetImportEngine.Helpers
{
    public static class ConvertHelper
    {
        public static object ValidateData<T>(object columndata, out object data)
        {
            //IsValid() and ConvertFrom() only work if you do valid converts from one data type to another.
            //Those FAIL if you attempt to convert from one type to the same type (e.g. converting Int64 to Int64 fails)

            try
            {
                data = null;
                var converter = TypeDescriptor.GetConverter(typeof(T));

                if (converter != null && converter.IsValid(columndata))
                {
                    try { data = (T)converter.ConvertFrom(columndata); }
                    catch {
                        if (columndata != null)
                            data = (T)converter.ConvertFromInvariantString(columndata.ToString());
                        else
                            data = null;
                    }// try invariant if direct fails ,happens on decimals on non-US locales                
                }
                else if (converter != null && columndata != null)
                {
                    if (converter.IsValid(columndata.ToString()))
                    {
                        data = (T)converter.ConvertFrom(columndata.ToString());
                    }
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


