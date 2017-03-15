using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace NexusInterfaces
{
    public class DateTimeUtil
    {
        public static String USString(DateTime dt, String format)
        {
            DateTimeFormatInfo myDTFI = new CultureInfo("en-US", false).DateTimeFormat;
            return dt.ToString("yyyy-MM-ddTHH:mm:ss.fff", myDTFI);

        }

    }
}
