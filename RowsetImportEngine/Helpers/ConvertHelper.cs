using System.ComponentModel;
using System.Windows.Forms;


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


