// USE pubs
// DROP TABLE BCPTarget 
// CREATE TABLE BCPTarget (cint int, cvarchar varchar (1000), cnvarchar nvarchar (1000), cdate datetime, cfloat float, cdecimal decimal)

using System;
using System.Runtime.InteropServices;
using System.Data;

namespace sqlnexus
{
	/// <summary>
	/// Managed wrapper for BulkLoad.DLL, which is a simplified wrapper around unmanaged ODBC BCP API.
	/// </summary>
	public class BulkLoad
	{
		private enum SQLTypes 
		{
			// From ODBCSS.H
			SQLIMAGE		= 0x22,		// image
			SQLTEXT			= 0x23,		// text
			SQLINT4			= 0x38,		// int
			SQLDATETIME 	= 0x3d,		// datetime
			SQLFLT8 		= 0x3e,		// float/real
			SQLDECIMAL		= 0x6a,		// decimal/numeric
			SQLNTEXT		= 0x63,		// ntext
			SQLINT8			= 0x7f,		// bigint
			SQLVARBINARY	= 0xa5,		// varbinary (actually SQLBIGVARBINARY, SQLVARBINARY is 0x25)
			SQLVARCHAR		= 0xa7,		// varchar (actually SQLBIGVARCHAR, SQLVARCHAR is 0x27)
			SQLNVARCHAR 	= 0xe7		// nvarchar
		}
		// BulkLoad.DLL functions exposed as public
		[DllImport("BulkLoad.dll", EntryPoint="AllocateConnectionHandle", CharSet=CharSet.Unicode)]		public static extern uint AllocateConnectionHandle	();
		[DllImport("BulkLoad.dll", EntryPoint="Connect", CharSet=CharSet.Unicode)]						public static extern bool Connect					(uint hConn, string szConnStr, string szServer, string szDatabase, bool bTrustedConn, string szUser, string szPassword);
		[DllImport("BulkLoad.dll", EntryPoint="SetTargetTable", CharSet=CharSet.Unicode)]				public static extern bool SetTargetTable			(uint hConn, string TableName);
		[DllImport("BulkLoad.dll", EntryPoint="SendRow", CharSet=CharSet.Unicode)]						public static extern bool SendRow					(uint hConn);
		[DllImport("BulkLoad.dll", EntryPoint="CommitBatch", CharSet=CharSet.Unicode)]					public static extern int CommitBatch				(uint hConn);
		[DllImport("BulkLoad.dll", EntryPoint="EndRowset", CharSet=CharSet.Unicode)]					public static extern int EndRowset					(uint hConn);
		[DllImport("BulkLoad.dll", EntryPoint="StartRowset", CharSet=CharSet.Unicode)]					public static extern bool StartRowset				(uint hConn);
		[DllImport("BulkLoad.dll", EntryPoint="GetErrorMessage", CharSet=CharSet.Unicode)]				public static extern string GetErrorMessage			(uint hConn);
		[DllImport("BulkLoad.dll", EntryPoint="Disconnect", CharSet=CharSet.Unicode)]					public static extern bool Disconnect				(uint hConn);

		// DefineColumn is used to pass column metadata to BULKLOAD.DLL.  This is private because 
		// we need to map some base data types to string due limitations in .NET datatype precision. 
		[DllImport("BulkLoad.dll", EntryPoint="DefineColumn", CharSet=CharSet.Unicode)]					private static extern bool DllDefineColumn			(uint hConn, int iSQLType, int cbMaxDataLen);
		// SetColumnData is used to pass row data to BULKLOAD.DLL. These are private because 
		// we need to map some base data types to string due to marshalling issues and limitations in 
		// .NET datatype precision. 
		[DllImport("BulkLoad.dll", EntryPoint="SetColumnDataInt", CharSet=CharSet.Unicode)]				public static extern bool SetColumnDataInt			(uint hConn, int ColNum, int ColData, bool bColIsNull);
		[DllImport("BulkLoad.dll", EntryPoint="SetColumnDataBigInt", CharSet=CharSet.Unicode)]			public static extern bool SetColumnDataBigInt		(uint hConn, int ColNum, long ColData, bool bColIsNull);
		[DllImport("BulkLoad.dll", EntryPoint="SetColumnDataVarChar", CharSet=CharSet.Ansi)]			public static extern bool SetColumnDataVarChar		(uint hConn, int ColNum, string ColData, bool bColIsNull);	
		[DllImport("BulkLoad.dll", EntryPoint="SetColumnDataNVarChar", CharSet=CharSet.Unicode)]		public static extern bool SetColumnDataNVarChar		(uint hConn, int ColNum, string ColData, bool bColIsNull);
		[DllImport("BulkLoad.dll", EntryPoint="SetColumnDataVarBinary", CharSet=CharSet.Unicode)]		public static extern bool SetColumnDataVarBinary	(uint hConn, int ColNum, byte[] ColData, int cbDataLen, bool bColIsNull);
		[DllImport("BulkLoad.dll", EntryPoint="SetColumnDataFloat", CharSet=CharSet.Unicode)]			public static extern bool SetColumnDataFloat		(uint hConn, int ColNum, double ColData, bool bColIsNull);
		[DllImport("BulkLoad.dll", EntryPoint="SetColumnDataDecimal", CharSet=CharSet.Unicode)]			public static extern bool SetColumnDataDecimal		(uint hConn, int ColNum, decimal ColData, bool bColIsNull);

		public static bool SetColumnDataDateTime (uint hConn, int ColNum, string ColData, bool bColIsNull) {
			// .NET datetime datatype has even worse precision than SQL's datetime, so keep 
			// datetimes in string format to avoid loss of precision. 
			// ODBC canonical datetime string format (YYYY-MM-DD HH:NN:SS.MMM) is required.
			if (ColData == null) 
				return SetColumnDataVarChar (hConn, ColNum, "", true);
			else
			{
				if (ColData.Length > 23) ColData = ColData.Substring(1, 23);
				return SetColumnDataVarChar (hConn, ColNum, ColData, bColIsNull);
			}
		}

		// Public interfaces to DefineColumn. 
		public static bool DefineColumnInt (uint hConn) {
			return DllDefineColumn (hConn, (int)BulkLoad.SQLTypes.SQLINT4, 4);
		}
		public static bool DefineColumnBigInt (uint hConn) {
			return DllDefineColumn (hConn, (int)BulkLoad.SQLTypes.SQLINT8, 8);
		}
		public static bool DefineColumnVarChar (uint hConn, int MaxStrLen) {
			return DllDefineColumn (hConn, (int)BulkLoad.SQLTypes.SQLVARCHAR, MaxStrLen);
		}
		public static bool DefineColumnNVarChar (uint hConn, int MaxStrLen) {
			return DllDefineColumn (hConn, (int)BulkLoad.SQLTypes.SQLNVARCHAR, MaxStrLen * 2);
		}
		public static bool DefineColumnVarBinary (uint hConn, int cbMaxDataLen) {
			return DllDefineColumn (hConn, (int)BulkLoad.SQLTypes.SQLVARBINARY, cbMaxDataLen);
		}
		public static bool DefineColumnDateTime (uint hConn) {
			// .NET datetime datatype has even worse precision than SQL's datetime, so keep 
			// datetimes in string format to avoid loss of precision. 
			// ODBC canonical datetime string format (YYYY-MM-DD HH:NN:SS.MMM) is required.
			return DllDefineColumn (hConn, (int)BulkLoad.SQLTypes.SQLVARCHAR, 23);
		}
		public static bool DefineColumnFloat (uint hConn) {
			return DllDefineColumn (hConn, (int)BulkLoad.SQLTypes.SQLFLT8, 8);
		}
		public static bool DefineColumnDecimal (uint hConn) {
			return DllDefineColumn (hConn, (int)BulkLoad.SQLTypes.SQLDECIMAL, 17);
		}
		public BulkLoad() {}
	}
}
