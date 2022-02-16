using System;
using System.Data;
using System.Text.RegularExpressions;
using RowsetImportEngine.Helpers;

// TODO: Need both DateTimeColumn and FloatColumn (for waitstats). 
// TODO: Add error log text file showing the first 100 rows that a problem occurred on (flag in ). 

/*
 * To define a new column type: 
 *  - Derive from either the abstract Column class or (ideally) one of its descendents. 
 *  - Add an instance of your new type to the KnownColumnTypes array in the ColumnTypes class below. 
 *  - If you must derive directly from Column (for example to add a new base data type) instead of 
 *    from one of its descendents like VarCharColumn, you must implement the shallow Copy() method 
 *    and the read-only DataType property. If you derive from one of the existing base data type 
 *    classes like VarCharColumn, IntColumn, etc, you can skip this third step. 
 * 
 */

namespace RowsetImportEngine
{
	public class ColumnTypes
	{
		public Column [] KnownColumnTypes = new Column[]
		{
			new VarCharColumn(),
			new NVarCharColumn(),
			new IntColumn(),
			new BigIntColumn(),
			new DateTimeColumn(), 
			new FloatColumn(), 
			new DecimalColumn(),
			new VarBinaryColumn(),
            new DateTimeOffsetColumn(),
		};
	}

	/// <summary>
	/// Base Column class.  Abstract class, cannot be instantiated.  Derive from this class to create columns 
	/// with specific base data types or custom data validation. 
	/// </summary>
	public abstract class Column
	{
        public static  Int32 SQL_MAX_LENGTH = -1;
		public string Name;					// Column name (e.g. "spid")
		public int Length;					// Width of the column in the text input rowset
		public int SqlColumnLength;			// Width of the column in the SQL table
		public string DefineToken = "";		// Name of the token that should take on the value of the most recently encountered value of this column
		public string ValueToken = "";		// Name of the token that should be used to provide the value of this column
		public bool RowsetLevel = false;	// True if the data is set once at the rowset level (e.g. implied "runtime" column), false if it is actual row data
		protected object data;	// Private storage for user data (may be any type)
		public abstract SqlDbType DataType	// Column's datatype (abstract -- must be implemented by derived class).  Read only.	
		{ 
			get;
		}
		public virtual object Data	// public property to set/get data. Can be overridden by descendants to do custom validation.
		{
			get
			{
				return data;
			}
			set
			{
				data = ValidateData(value);
			}
		}
		// The base class does not provide any data validation. If data fails validation, 
		// this function should return null.
		public virtual object ValidateData(object columndata)
		{
			return columndata;
		}
		// The base class' implementation of implicit and explicit ToString conversion assumes that the data 
		// stored is of a type that natively supports explicit conversion to string. If this is not the case, 
		// the two functions below will need to be overridden. 
		// Note: In current implementation, ToString() is not used. 
		public override string ToString()					// Explicit convert-to-string operator
		{
			if (data == null) return "NULL";
			else return (string)Convert.ChangeType(data, typeof(string));
		}
		public static implicit operator string (Column c)	// Implicit convert-to-string operator
		{
			if (c.data == null) return "NULL";
			return (string)Convert.ChangeType(c.data, typeof(string));
		}
		public Column() {}
		public Column(string Name, int Length, int SqlColumnLength, bool RowsetLevel) 
		{
			this.Name = Name;
			this.Length = Length;
			this.SqlColumnLength = SqlColumnLength;
			this.RowsetLevel = RowsetLevel;
		}
		// Every class derived directly from Column should implement a shallow (memberwise) Copy(). 
		// Unfortunately can't implement here in the abstract ancestor because it requires instantiating 
		// a new object. See VarCharColumn for suggested implementation. Used in 
		// TextRowsetImporter.ReadRowsetPropertiesFromXml(). 
		public abstract Column Copy ();
	}


	// Column classes for base datatypes. 
	public class VarCharColumn : Column
	{
		public override SqlDbType DataType 
		{
			get 
			{
				return SqlDbType.VarChar;
			}
		}
		public override object Data	
		{
			set
			{
				data = ValidateData(value);
			}
		}
		// The base class does not provide any data validation. 
		public override object ValidateData(object columndata)
		{
			try
			{
				// For Varchar we only need to validate length.  If string is too long, truncate. 
				data = (string)Convert.ToString(columndata);
                 
                //also handle varchar(max) or nvarchar(max) 
                if (((string)data).Length > this.SqlColumnLength && (this.SqlColumnLength != Column.SQL_MAX_LENGTH))
                {
                    data = ((string)data).Substring(1, this.SqlColumnLength);
                }
				return data;
			}
			catch
			{
				data = null;
				return null;
			}
		}
		public override Column Copy()
		{
			VarCharColumn newcol = new VarCharColumn();
			newcol = (VarCharColumn)this.MemberwiseClone();
			return newcol;
		}
		public VarCharColumn() : base() {}
		public VarCharColumn(string Name, int Length, int SqlColumnLength, bool RowsetLevel) : base(Name, Length, SqlColumnLength, RowsetLevel) {}
	}
	public class NVarCharColumn : VarCharColumn
	{
		public override SqlDbType DataType 
		{ 
			get 
			{
				return SqlDbType.NVarChar;
			}
		}
		public NVarCharColumn() : base() {}
		public NVarCharColumn(string Name, int Length, int SqlColumnLength, bool RowsetLevel) : base(Name, Length, SqlColumnLength, RowsetLevel) {}
	}
	public class DateTimeColumn : Column
	{
		public override SqlDbType DataType 
		{
			get 
			{
				return SqlDbType.DateTime;
			}
		}
		public override object Data	
		{
			set
			{
				data = ValidateData(value);
			}
		}
		// The base class does not provide any data validation. 
		public override object ValidateData(object columndata)
		{
            return ConvertHelper.ValidateData<DateTime>(columndata, out data);
   //         try
			//{
			//	// Query Analyzer and osql format dates like this: 
			//	//		2004-07-12 23:26:18.850
			//	// The specific formatting is left up to the SQL ODBC driver. 
			//	// TODO: Verify whether SQLODBC date formatting depends on the system locale. Do we need a way to specify formatting or LCID? 

			//	// Unfortunately, the framework's DateTime value only has second-level precision. Use 
			//	// Convert.ToDateTime just to validate that the format of the datetime string is valid, 
			//	// but actually store the string value so we can pass the milliseconds portion intact 
			//	// to SQL. 
			//	Convert.ToDateTime(columndata);
			//	data = columndata.ToString();
			//	return data;
			//}
			//catch
			//{
			//	data = null;
			//	return null;
			//}
		}
		public override Column Copy()
		{
			DateTimeColumn newcol = new DateTimeColumn();
			newcol = (DateTimeColumn)this.MemberwiseClone();
			return newcol;
		}
		public DateTimeColumn() : base() {}
		public DateTimeColumn(string Name, int Length, int SqlColumnLength, bool RowsetLevel) : base(Name, Length, SqlColumnLength, RowsetLevel) {}
	}


    public class DateTimeOffsetColumn : Column
    {
        public override SqlDbType DataType
        {
            get
            {
                return SqlDbType.DateTimeOffset;
            }
        }
        public override object Data
        {
            set
            {
                data = ValidateData(value);
            }
        }
        // The base class does not provide any data validation. 
        //we have to convert the data to DateTimeOffset from sql output
        //here is an example of sql output 2016-02-02 21:07:05.1230000 +00:00
        public override object ValidateData(object columndata)
        {
            try
            {

                
                String pattern = @" ";
                String[] elements = Regex.Split(columndata.ToString(), pattern);

                DateTime dt = DateTime.ParseExact(string.Format("{0} {1}", elements[0], elements[1]), "yyyy-MM-dd HH:mm:ss.fffffff", null);

                string pattern2 = @"[:]";
                string [] elements2 = Regex.Split (elements[2], pattern2);
                int hour = Int32.Parse(elements2[0]);
                int minute = Int32.Parse(elements2[1]);
               if (hour<0)
                {
                    
                    minute = -minute;
                }

                TimeSpan span = new TimeSpan(hour, minute, 0);
                DateTimeOffset myDt = new DateTimeOffset(dt, span); ;

                return myDt;
            }
            catch
            {
                data = null;
                return null;
            }
        }
        public override Column Copy()
        {
            DateTimeOffsetColumn newcol = new DateTimeOffsetColumn();
            newcol = (DateTimeOffsetColumn )this.MemberwiseClone();
            return newcol;
        }

        /*
        public static implicit operator DateTimeOffset( DateTimeOffsetColumn myData)	
        {
            
            
            DateTimeOffset myDT = new DateTimeOffset(1900, 1, 1, 1, 0, 0, TimeSpan.Zero);

            try
            { 
            }
            catch (Exception ex)
            {
                //eat all exceptins
            }


            return myDT;
            

        }*/
        public DateTimeOffsetColumn() : base() { }
        public DateTimeOffsetColumn(string Name, int Length, int SqlColumnLength, bool RowsetLevel) : base(Name, Length, SqlColumnLength, RowsetLevel) { }
    }


	public class IntColumn : Column
	{
		public override SqlDbType DataType
		{ 
			get 
			{
				return SqlDbType.Int;
                
			}
		}
		public override object Data 
		{
			set
			{
				data = ValidateData(value);
			}
		}
		public override object ValidateData(object columndata)
		{
            return ConvertHelper.ValidateData<int>(columndata, out data);
            //try 
            //{
            //	data = Convert.ToInt32 (columndata);
            //	return data;
            //}
            //catch
            //{
            //	// Set to null otherwise. 
            //	data = null;
            //	return null;
            //}
        }
		public override Column Copy()
		{
			IntColumn newcol = new IntColumn();
			newcol = (IntColumn)this.MemberwiseClone();
			return newcol;
		}
		public IntColumn() : base() {}
		public IntColumn(string Name, int Length, int SqlColumnLength, bool RowsetLevel) : base(Name, Length, SqlColumnLength, RowsetLevel) {}
	}
	public class BigIntColumn : IntColumn
	{
		public override SqlDbType DataType 
		{ 
			get 
			{
				return SqlDbType.BigInt;
			}
		}
		public override object Data 
		{
			set
			{
				data = ValidateData(value);
			}
		}
		public override object ValidateData(object columndata)
		{
            return ConvertHelper.ValidateData<long>(columndata, out data);
            //try 
            //{
            //	data = Convert.ToInt64 (columndata);
            //             return data;
            //             // TODO: change validation to use tryparse to cut down on frequent "normal" parse exceptions (e.g. "NULL" in an int col)
            //             //if (!Int64.TryParse(columndata, out data))
            //             //    data = null;
            //             //return data;
            //}
            //catch
            //{
            //	// Set to null otherwise. 
            //	data = null;
            //	return null;
            //}
        }
		public BigIntColumn() : base() {}
		public BigIntColumn(string Name, int Length, int SqlColumnLength, bool RowsetLevel) : base(Name, Length, SqlColumnLength, RowsetLevel) {}
	}
	public class FloatColumn : IntColumn
	{
		public override SqlDbType DataType
		{ 
			get 
			{
				return SqlDbType.Float;
			}
		}
		public override object Data 
		{
			set
			{
				data = ValidateData(value);
			}
		}
		public override object ValidateData(object columndata)
		{
            return ConvertHelper.ValidateData<double>(columndata, out data);
            //// QA and osql will format floats in one of the following ways: 
            ////		123.45
            ////		1.2345E-10
            //// Convert.ToDouble(string) will understand both of these. 
            //try 
            //{
            //	data = Convert.ToDouble(columndata);
            //	return data;
            //}
            //catch
            //{
            //	// Set to null otherwise. 
            //	data = null;
            //	return data;
            //}
        }
		public FloatColumn() : base() {}
		public FloatColumn(string Name, int Length, int SqlColumnLength, bool RowsetLevel) : base(Name, Length, SqlColumnLength, RowsetLevel) {}
	}
	public class DecimalColumn : IntColumn
	{
		public override SqlDbType DataType
		{ 
			get 
			{
				return SqlDbType.Decimal;
			}
		}
		public override object Data 
		{
			set
			{
				data = ValidateData(value);
			}
		}
		public override object ValidateData(object columndata)
		{
            return ConvertHelper.ValidateData<decimal>(columndata, out data);
            //try 
            //{
            //	data = Convert.ToDecimal(columndata);
            //	return data;
            //}
            //catch
            //{
            //	// Set to null otherwise. 
            //	data = null;
            //	return data;
            //}
        }
		public DecimalColumn() : base() {}
		public DecimalColumn(string Name, int Length, int SqlColumnLength, bool RowsetLevel) : base(Name, Length, SqlColumnLength, RowsetLevel) {}
	}
	public class VarBinaryColumn : Column
	{
		public override SqlDbType DataType 
		{ 
			get 
			{
				return SqlDbType.VarBinary;
			}
		}
		public override object Data
		{
			set
			{
				data = ValidateData(value);
			}
		}
		public override object ValidateData(object columndata)
		{
            data = null;
            // For now we require that the binary value is being provided in hex string form, e.g. "1a2b3c" or "0x1a2b3c". 
            bool isString = columndata is string;
            if (isString)
			{
				try
				{
                    if (columndata.Equals("NULL"))
                    {
                        return null;
                    }
                    string tmpstr = columndata as string;
                    // Remove "0x" prefix if exists. 
                    if ((columndata as string).Substring (0,2).ToUpper().Equals("0X"))
						tmpstr = tmpstr.Substring(2);
					// Add a leading zero to complete a byte, if necessary. 
					if ((tmpstr.Length % 2) > 0) tmpstr = "0" + tmpstr; 
					// Validate max length, and truncate overflow bytes if necessary;
					if (tmpstr.Length > this.Length) tmpstr = tmpstr.Substring(0, this.Length);
					// Convert the hex string to a byte array. 
					byte[] bytearray = new byte[(tmpstr.Length / 2)];
					for (int i = 0; i < (tmpstr.Length - 1); i += 2)
					{
                        string byteStr = tmpstr.Substring(i, 2);
                        if (!byte.TryParse(byteStr, System.Globalization.NumberStyles.HexNumber, null as IFormatProvider, out byte result))
                        {
                            return null;
                        }
                        bytearray[i / 2] = result;
                    }
                    data = bytearray;
                    return data;
				}
				catch
				{
					// Some unexpected exception during conversion process.
					return null;
				}
			} 
			else	// Caller tried to set the column data to something other than a string.
			{
				return null;
			}
		}
		public override string ToString()					// Explicit convert-to-string operator
		{
			if (data == null) return "NULL";
			else 
			{
				string tmpstr = ""; 
				foreach (byte b in (data as byte[]))
				{
					tmpstr += b.ToString("X2");
				}
				return tmpstr;
			}
		}
		public static implicit operator string (VarBinaryColumn  c)	// Implicit convert-to-string operator
		{
			if (c.data == null) return "NULL";
			else 
			{
				string tmpstr = ""; 
				foreach (byte b in (c.data as byte[]))
				{
					tmpstr += b.ToString("X2");
				}
				return tmpstr;
			}
		}
		public override Column Copy()
		{
			VarBinaryColumn newcol = new VarBinaryColumn();
			newcol = (VarBinaryColumn)this.MemberwiseClone();
			return newcol;
		}
		public VarBinaryColumn () : base() {}
		public VarBinaryColumn (string Name, int Length, int SqlColumnLength, bool RowsetLevel) : base(Name, Length, SqlColumnLength, RowsetLevel) {}
	}
}
