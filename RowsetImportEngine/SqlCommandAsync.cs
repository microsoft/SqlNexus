using System;
using System.Data;
using System.Data.SqlTypes;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using System.Threading;

namespace RowsetImportEngine
{
	// Prior to ADO.NET 2.0 (not released as of now, May 2004), managed SqlClient doesn't provide 
	// a built-in way to execute a query asynchronously (!).  SqlCommandAsync just wraps 
	// SqlCommand.ExecuteNonQuery and executes the query on a background thread. 
	public class SqlCommandAsync
	{
		public SqlConnection Connection;
		public int CommandTimeout = 0;
		public string CommandText = "";
		public bool IsExecuting = false;
		public SqlCommandAsync() {}
		protected SqlCommand cmd = new SqlCommand();
		// So far, haven't had a need to implement an async SqlDataReader. Only supports async version of 
		// ExecuteNonQuery(). 
		public void ExecuteNonQueryAsync()
		{
			cmd.CommandTimeout = this.CommandTimeout; 
			cmd.CommandText = this.CommandText;
			cmd.Connection = this.Connection;
			this.IsExecuting = true;
			Thread t = new Thread(new ThreadStart(ExecuteNonQueryAsyncThreadProc));
			t.Start();
			Thread.Sleep(0);
		}
		protected void ExecuteNonQueryAsyncThreadProc () 
		{
			try 
			{
				cmd.ExecuteNonQuery();
			}
			catch 
			{}
			finally
			{
				this.IsExecuting = false;
			}
		}
	}
}
