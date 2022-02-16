using System.ComponentModel;

namespace RowsetImportEngine.Helpers
{
    public static class ConvertHelper
    {
        public static object ValidateData<T>(object columndata, out object data)
        {
            try
            {
                data = null;
                var converter = TypeDescriptor.GetConverter(typeof(T));

                if (converter != null && converter.IsValid(columndata))
                {
                    data = (T)converter.ConvertFrom(columndata);
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


