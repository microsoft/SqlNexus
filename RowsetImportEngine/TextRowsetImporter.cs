using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Data.SqlTypes;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using System.Xml;
using System.Threading;
using System.Diagnostics;
using NexusInterfaces;
using BulkLoadEx;
using System.Text;
namespace RowsetImportEngine
{

	/// <summary>
	/// Main class used to drive importing of rowsets from a text file.  Use DoImport() to 
	/// start an import. Use .State, .FileSize, and .CurrentPosition to check current status. 
	/// Use Cancel() to abort the import. 
	/// </summary>
	public class TextRowsetImporter : INexusFileImporter
	{
        private const string DROP_EXISTING = "Drop existing tables";
        private const string POST_LOAD_SQL_SCRIPT = null;//"PerfStatsAnalysis_doNOTRUN.sql";
        const string OPTION_ENABLED = "Enabled";

        /// <summary>
        /// Reference to the rowset that corresponds to the current position in the input file
        /// </summary>
		public RowsetImportEngine.TextRowset CurrentRowset;

        /// <summary>
        /// TextRowsetImporter ctor
        /// </summary>
		public TextRowsetImporter()
		{
            Options.Add(DROP_EXISTING, false); //don't drop existing
            Options.Add(OPTION_ENABLED, true);
            
		}

        /// <summary>
        /// Main params passed to DoImport
        /// </summary>
        private string connStr = "";
        private string server = "";
        private string databasename = "";
        private bool usewindowsauth = true;
        private string sqllogin = "";
        private string sqlpassword = "";
        private bool dropTables = true;
        private string filename = "";
        private ILogger logger;

        /// <summary>
        /// Called by the hosting framework just before a load begins. 
        /// </summary>
        /// <param name="Filename">File mask to import (pssdiag_output_path\*.OUT)</param>
        /// <param name="connString">Target database connection string</param>
        /// <param name="Server">Target SQL Server name</param>
        /// <param name="UseWindowsAuth">True if Windows authentication should be used to connect to the SQL Server</param>
        /// <param name="SQLLogin">SQL login name (if UseWindowsAuth=false)</param>
        /// <param name="SQLPassword">SQL login password (if UseWindowsAuth=false)</param>
        /// <param name="DatabaseName">Target database name</param>
        /// <param name="Logger">ILogger instance</param>
        public void Initialize(string Filemask, string connString, string Server, bool UseWindowsAuth, string SQLLogin, string SQLPassword, string DatabaseName, ILogger Logger)
        {
            if (Server == null || DatabaseName == null || Server.Trim().Length == 0 || DatabaseName.Trim().Length == 0)
                throw new ArgumentNullException("you have passed an invalid server or database name");

            this.server = Server;
            this.databasename = DatabaseName;
            this.usewindowsauth = UseWindowsAuth;
            this.sqllogin = SQLLogin;
            this.sqlpassword = SQLPassword;
            this.connStr = connString;
            this.filename = Filemask;
            this.logger = Logger;
        }
      

        /// <summary>
        /// Tell the hosting framework the type of files we know how to import
        /// </summary>
        public string[] SupportedMasks
        {
            get
            {
                return new string[] { "*.OUT", "*.TXT" };
            }
        }

        /// <summary>
        /// We don't need to run any pre-import scripts
        /// </summary>
        public string[] PreScripts
        {
            get
            {
                return new string[0];
            }
        }

        /// <summary>
        /// We need to run a script following the import for efficient analysis. The hosting framework will do this for us. 
        /// </summary>
        public string[] PostScripts
        {
            get
            {
                return new string[] { POST_LOAD_SQL_SCRIPT };
            }
        }

		private long fileSize = 0;

        /// <summary> Size of input file </summary>
        public long FileSize
        {
            get
            {
                return fileSize;
            }
            set
            {
                fileSize = value;
            }
        }

        private long currentPosition = 0;
        /// <summary> Byte position in input file </summary>
        public long CurrentPosition
        {
            get
            {
                return currentPosition;
            }
            set
            {
                currentPosition = value;
                OnProgressChanged(new EventArgs());
            }
        }

        private ImportState state = ImportState.Idle;
        /// <summary> Host can check this property to see current state </summary>
        public ImportState State
        {
            get
            {
                return state;
            }
            set
            {
                state = value;
                OnStatusChanged(new EventArgs());
            }
        }

        private bool cancelled = false;
        /// <summary> Will be set to true if the current import has been canceled </summary>
        public bool Cancelled
        {
            get
            {
                return cancelled;
            }
            set
            {
                cancelled = value;
            }
        }

        private ArrayList knownRowsets = new ArrayList();
        /// <summary> List of the rowsets we know how to interpret </summary>
        public ArrayList KnownRowsets
        {
            get
            {
                return knownRowsets;
            }
            set
            {
                knownRowsets = value;
            }
        }

        /// <summary> 
        /// List of the "non-tabular" rowsets we know how to interpret. Maintained separately 
        /// for perf reasons. 
        /// </summary>
        public ArrayList KnownNonTabularRowsets = new ArrayList();

        private long totalRowsInserted = 0;

        /// <summary>
        /// Number of rows inserted so far
        /// </summary>
        public long TotalRowsInserted
        {
            get
            {
                return totalRowsInserted;
            }
            set
            {
                totalRowsInserted = value;
            }
        }

        private long totalLinesProcessed = 0;
        /// <summary>
        /// Number of lines in the input file processed so far
        /// </summary>
        public long TotalLinesProcessed
        {
            get
            {
                return totalLinesProcessed;
            }
            set
            {
                totalLinesProcessed = value;
            }
        }

        /// <summary>
        /// Number of rows inserted when we last reset inactive rowsets
        /// </summary>
		private long TotalRowsInsertedAtLastFlushCheck = 0;
		private DateTime ImportStartDate = DateTime.Now;
		
        /// <summary>
        /// # of rows between BCP batch flushes (commit)
        /// </summary>
		public const long BATCH_COMMIT_ROWCOUNT = 10000;
        /// <summary>
        /// col width for non-tabular columns without explicitly specified width
        /// </summary>
		public const int DEFAULT_NONTAB_COLUMN_LEN = 256;

        
		
        /// <summary>
        /// Used to read the input file
        /// </summary>
		private StreamReader sr;
        /// <summary>
        /// Connection to SQL
        /// </summary>
		private SqlConnection cn = new SqlConnection();
        /// <summary>
        /// Tokens (e.g. RUNTIME=="Start time: xyz") 
        /// </summary>
		private Hashtable DefinedTokens = new Hashtable();
		
        /// <summary>
        /// Used to communicate progress back to hosting framework
        /// </summary>
		public delegate void ImportProgressDelegate (int PercentComplete);
		private ImportProgressDelegate m_ProgressUpdateFunction;

		/// <summary>
        /// Constructor optionally provides a delegate so that we can keep our parent appraised of load progress. 
		/// </summary>
		/// <param name="ProgressUpdateFunction"></param>
		public TextRowsetImporter (ImportProgressDelegate ProgressUpdateFunction)
		{
			m_ProgressUpdateFunction = ProgressUpdateFunction;
		}

		/// <summary>
        /// Main entrypoint into importer class. Invoked by host framework to begin a load. 
		/// </summary>
		/// <returns></returns>
		public bool DoImport()
		{
			bool result;
            try
            {
                Reset();
                this.dropTables = (bool)Options[DROP_EXISTING];
                // The list of rowsets we know how to interpret and their properties are stored in an XML file 
                // named TextRowsets.XML. Read this file and use the info to populate the KnownRowsets collection. 
                if (result = GetRowsetProperties())
                {
                    // Do import
                    this.State = ImportState.OpeningFile;
                    if (result = OpenFile())
                    {
                        this.State = ImportState.OpeningDatabaseConnection;
                        if (result = OpenSQLConnection())
                        {
                            this.State = ImportState.Importing;
                            try
                            {
                                ProcessFile();
                                result = true;
                            }
                            catch (Exception ex)
                            {
                                Util.Logger.LogMessage("Rowset Importer failed for file: " + this.filename + "exception: " + ex.ToString());
                                result = false;
                            }

                            //CloseSQLConnection();
                        }
                        CloseFile();

                    }
                }
                this.State = ImportState.Idle;
                return result;
            }
            catch (Exception ex)
            {
                logger.LogMessage("TextRowsetImperter: Error failed with exception " + ex.ToString());
                return false;
            }
            catch
            {
                logger.LogMessage("TextRowsetImperter: Error failed with unknown exception " );
                return false;
            }
		}

		/// <summary>
        /// Host can call this method to tell us to stop in the middle of a load.	
		/// </summary>
		public void Cancel() 
		{
			this.Cancelled = true;
		}
		/// <summary>
        /// Set initial importer state
		/// </summary>
		private void Reset() 
		{
			TotalRowsInserted = 0;
			TotalLinesProcessed = 0;
			TotalRowsInsertedAtLastFlushCheck = 0;
			Cancelled = false;
			KnownRowsets.Clear();
			KnownNonTabularRowsets.Clear();
			DefinedTokens.Clear();
		}


        /// <summary>
        /// Based on column metadata in the current rowset, format and execute CREATE TABLE command. 
        /// </summary>
        private void CreateTable()
        {
            string SqlStmt = string.Empty;
            SqlCommand cmd;
            int len;
            SqlConnection conn = new SqlConnection(this.connStr);

            // Validate table name
            if (!IsSafeSqlIdentifier(CurrentRowset.Name))
            {
                logger.LogMessage($"CreateTable: Unsafe table name '{CurrentRowset.Name}'", MessageOptions.Silent);
                throw new ArgumentException("Unsafe table name.");
            }

            try
            {
                // Create the CREATE TABLE command. 
                cmd = new SqlCommand();
                var sb = new StringBuilder();


                // Use QUOTENAME for table name to ensure it's properly escaped and validated
                sb.Append($"IF OBJECT_ID(N'{CurrentRowset.Name}', 'U') IS NULL CREATE TABLE {QUOTENAME(CurrentRowset.Name)} (");


                foreach (RowsetImportEngine.Column c in CurrentRowset.Columns)
                {
                    len = c.SqlColumnLength;
                    string ColumnName = c.Name.Trim();

                    // Validate column name
                    if (!IsSafeSqlIdentifier(ColumnName))
                    {
                        logger.LogMessage($"CreateTable: Unsafe column name '{ColumnName}'", MessageOptions.Silent);
                        throw new ArgumentException("Unsafe column name.");
                    }

                    // Append column definition safely by validation type and adding appropriate length/precision. Make all columns NULLable.
                    sb.Append($"{QUOTENAME(ColumnName)} {GetSafeSqlType(c.DataType, c.SqlColumnLength)} NULL, ");


                    // Remove trailing comma and close parenthesis
                    SqlStmt = sb.ToString().TrimEnd(',', ' ') + ")";
                }

                // Use the SqlCommand to run the CREATE TABLE. 

                conn.Open();
                cmd.Connection = conn;
                cmd.CommandText = SqlStmt;  // CodeQL [SM03934] multiple levels of object and column name validation performed above
                cmd.ExecuteNonQuery();
                conn.Close();

            }
            catch (SqlException e)
            {
                ErrorDialog ed = new ErrorDialog(e, false, this.logger);
                Util.Logger.LogMessage("Createtable command " + SqlStmt);
                ed.Handle();
            }
            catch (InvalidOperationException e)
            {
                ErrorDialog ed = new ErrorDialog(e, false, this.logger);
                Util.Logger.LogMessage("Createtable command " + SqlStmt);
                ed.Handle();
            }
            catch (Exception e)
            {
                ErrorDialog ed = new ErrorDialog(e, false, this.logger);
                Util.Logger.LogMessage("Createtable command " + SqlStmt);
                ed.Handle();
            }
            finally
            {
                conn.Close();
            }
            return;
        }
        /// <summary>
        /// Run a DROP TABLE for the current rowset's SQL table.  Optionally executed the first time we 
        /// encounter a new rowset.
        /// </summary>
        private void DropCurrentTable ()
		{
			DropTable (CurrentRowset.Name);
			return;
		}

        /// <summary>
        /// Drop all tables prior to start of a load
        /// </summary>
		public void DropAllKnownTables ()
		{
			foreach (TextRowset r in this.KnownRowsets)
			{
				DropTable (r.Name);
			}
			return;
		}

		private void DropTable (string TableName)
		{

            // Validate table name
            string safeTableName = QUOTENAME(TableName);

            // First drop any dependent objects
            const string DependentObjectCheckSql = 
				"SELECT DISTINCT OBJECT_NAME (syso.[id]) AS dependent_object,  \n"
				+ "  CASE  \n"
				+ "    WHEN SUBSTRING (val.name, 5, 16) = 'user table' THEN 'table' \n"
				+ "    WHEN SUBSTRING (val.name, 5, 16) LIKE '%FUNCTION%' THEN 'function' \n"
				+ "    WHEN SUBSTRING (val.name, 5, 16) LIKE '%FUNCTION%' THEN 'function' \n"
				+ "    WHEN SUBSTRING (val.name, 5, 16) = 'stored procedure' THEN 'procedure' \n"
				+ "    ELSE SUBSTRING (val.name, 5, 16)  \n"
				+ "  END AS obj_type  \n"
				+ "FROM sysdepends dep \n"
				+ "INNER JOIN sysobjects syso ON syso.[id] = dep.[id] \n"
				+ "INNER JOIN master.dbo.spt_values val ON syso.type = SUBSTRING (val.name,1,2) COLLATE database_default AND val.type = 'O9T' \n"
				+ "WHERE [depid] = OBJECT_ID ('{0}') \n"
				+ "  AND OBJECT_NAME (dep.[id]) != '{0}' \n";

            // Format the SQL query to find dependent objects
            string sqlQuery = String.Format(DependentObjectCheckSql, safeTableName);

            using (SqlConnection conn = new SqlConnection(this.connStr))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sqlQuery, conn))
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        DropObject(dr["dependent_object"].ToString(), dr["obj_type"].ToString());
                    }
                }
            }


            // Then drop the table itself
            DropObject(TableName, "table");
			return;
		}

        // CodeQL [sql-injection]: Identifiers validated and escaped via QUOTENAME

        private static string QUOTENAME(string identifier)
        {
            // Wrap in brackets and escape any closing bracket inside the identifier by doubling it to prevent malicious input
            return "[" + identifier.Replace("]", "]]") + "]";
        }

        private static readonly HashSet<string> AllowedObjectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TABLE", "PROCEDURE", "FUNCTION", "VIEW"
        };

        private bool IsSafeSqlIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
                return false;
            // Must start with a letter or underscore, followed by letters, numbers, spaces, underscores, hyphens, #, or $   
            //^ [A-Za-z_][A-Za-z0-9 _\-#\$]*$
            return Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9 _\-#\$]*$");

        }


        private string GetSafeSqlType(SqlDbType dataType, int length)
        {
            switch (dataType)
            {
                case SqlDbType.Decimal:
                    return "DECIMAL(38,10)";
                case SqlDbType.Float:
                    return "FLOAT(53)";
                case SqlDbType.VarChar:
                    return $"VARCHAR({GetLength(length, 8000)})";
                case SqlDbType.VarBinary:
                    return $"VARBINARY({GetLength(length, 8000)})";
                case SqlDbType.NVarChar:
                    return $"NVARCHAR({GetLength(length, 4000)})";
                default:
                    // Only allow known safe types
                    return dataType.ToString().ToUpperInvariant();
            }
        }

        private string GetLength(int len, int maxAllowed)
        {
            if (len > 0 && len <= maxAllowed)
                return len.ToString();
            else
                return "MAX";
        }


        private void DropObject(string ObjectName, string ObjectType)
        {
            // CodeQL [sql-injection]: Identifiers validated before use in SQL statement
            if (!AllowedObjectTypes.Contains(ObjectType))
            {
                logger.LogMessage($"DropObject: Invalid object type '{ObjectType}'", MessageOptions.Silent);
                throw new ArgumentException("Invalid object type.");
            }
            if (!IsSafeSqlIdentifier(ObjectName))
            {
                logger.LogMessage($"DropObject: Unsafe object name '{ObjectName}'", MessageOptions.Silent);
                throw new ArgumentException("Unsafe object name.");
            }

            using (SqlConnection conn = new SqlConnection(this.connStr))
            {
                try
                {
                    // Bracket-quote the object name
                    string SqlStmt = $"IF OBJECT_ID(N'{ObjectName}', N'{ObjectType}') IS NOT NULL DROP {ObjectType} [{ObjectName}]";
                    using (SqlCommand cmd = new SqlCommand(SqlStmt, conn))
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();  // CodeQL [SM03934] multiple levels of object validation performed prior to this call
                    }
                }
                catch (SqlException e)
                {
                    ErrorDialog ed = new ErrorDialog(e, true, this.logger);
                    ed.Handle();
                }
                catch (InvalidOperationException e)
                {
                    ErrorDialog ed = new ErrorDialog(e, true, this.logger);
                    ed.Handle();
                }

                catch (Exception e)
                {
                    ErrorDialog ed = new ErrorDialog(e, true, this.logger);
                    ed.Handle();
                }
            }
        }


        /// <summary>
        /// Read through each line of the input file, recognize any rowsets in the file, parse and insert the 
        /// rows from these rowsets. 
        /// </summary>
        private void ProcessFile()
		{
			bool	InRowset = false;
			string	line = "";			// current line
			string	CurrentRowText = "";// text of current row (usually == line except in case of multi-line rows, a la DBCC INPUTBUFFER)
			string	lineprev1 = null;	// previous line
			string	lineprev2 = null;	// line prior to the previous line
			int		PercentComplete = 0;

			try 
			{
				this.TotalLinesProcessed = 0;
				while ((line != null) && (line=this.sr.ReadLine()) != null)
				{
                    //fixing a bug where indexes script dup rowcnt column
                    line = line.Replace("objid       rowcnt", "objid       rowcnt2");
                    line = line.Replace("client_interface_name            total_elapsed_time", "client_interface_name            total_elapsed_tim2");

                    //apparently, bulkcopy object thinks row_mods is a key word and skip importing the rows if NULL is present
                    line = line.Replace("row_mods", "row_mod2");
                    

					this.CurrentPosition += line.Length;
					this.TotalLinesProcessed++;

					if (!InRowset) 
					{
						// Check for a "non-tabular" rowset (one without traditional column headers)
						InRowset = CheckForRowsetStart(line, lineprev1, lineprev2, true);
					}

					if (InRowset)
					{
						// If we are in the middle of processing a rowset, see whether we've reached the end. 
						if (CurrentRowset.IsEndOfRowset(line, CurrentRowText)) 
						{
							line="";
							CurrentRowText="";
							CurrentRowset.Clear();
							this.CurrentRowset = null;
							InRowset = false;
							if ((TotalRowsInserted - TotalRowsInsertedAtLastFlushCheck) > BATCH_COMMIT_ROWCOUNT)
							{
								// Periodically, flush the BCP batch for all active rowsets so that we don't 
								// suffer from excessive log autogrowth. 
								FlushRowsets();
								TotalRowsInsertedAtLastFlushCheck = TotalRowsInserted;
							}
						} 
						else	// If we're in a rowset and not at the end of the rowset, this must be a row. 
						{
							CurrentRowText = line;
							// Handle multi-line rows by reading in subsequent rows until we come to end-of-row marker.
                            
                            if (!CurrentRowset.IsEndOfRow(line))
                            {

                                //performance optimization 
                                //using string concat will lead to CPU hang when 'losing track'
                                StringBuilder Builder = new StringBuilder();
                                while (((line = this.sr.ReadLine()) != null) && (!CurrentRowset.IsEndOfRow(line)))
                                {
                                    Builder.AppendFormat(" {0}", line);
                                    //CurrentRowText += " " + line;
                                    
                                }
                                CurrentRowText += Builder.ToString();
                            }
							// ParseRow will break out row data on column boundaries and populate the data in the Columns collection.
							this.CurrentRowset.ParseRow(CurrentRowText);
							// Copy token data to/from any columns that are linked to tokens in this rowset
							if (this.CurrentRowset.UsesTokens)
							{
								foreach (Column c in this.CurrentRowset.Columns)
								{
									if (""!=c.DefineToken)
									{
										if (this.DefinedTokens.Contains(c.DefineToken))
											this.DefinedTokens[c.DefineToken] = c.Data;
										else
											this.DefinedTokens.Add (c.DefineToken, c.Data);
									}
									if (""!=c.ValueToken)
									{
										if (this.DefinedTokens.Contains(c.ValueToken))
											c.Data = this.DefinedTokens[c.ValueToken];
										else 
										{
											// Handle built-in application-provided tokens
											switch (c.ValueToken)
											{
												case "INPUTFILENAME":
													c.Data = Path.GetFileName(this.filename);
													break;
												case "USERNAME":
													c.Data = Environment.UserDomainName + @"\" + Environment.UserName;
													break;
												case "IMPORTDATE":
													c.Data = this.ImportStartDate.ToString("yyyy-mm-dd hh:mm:ss");
													break;
												case "IMPORTDATEUTC":
													c.Data = this.ImportStartDate.ToUniversalTime().ToString("yyyy-mm-dd hh:mm:ss");
													break;
												case "CURRENTDATE":
													c.Data = DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss");
													break;
												case "CURRENTDATEUTC":
													c.Data = DateTime.Now.ToUniversalTime().ToString("yyyy-mm-dd hh:mm:ss");
													break;
												case "ROWNUMBER":
													c.Data = this.CurrentRowset.RowsInserted.ToString(); //have to make a string because ValidateData() would fail to convert from long to long
                                                    break;
												default:
													c.Data = null;
													break;
											}
										}
									}
								}
							}
							// Insert current row into SQL. 
							InsertRow();
							// Save the last line read (the inputbuffer special rowset uses the same marker for end-of-row and end-of-rowset)
							// TODO: clean this up (same soln as in SimpleMessageRowset)
							CurrentRowText = line;
							// If we are in the middle of processing a rowset, see whether we've reached the end. 
							if (CurrentRowset.IsEndOfRowset(line, CurrentRowText)) 
							{
								CurrentRowText="";
								CurrentRowset.Clear();
								this.CurrentRowset = null;
								InRowset = false;
							} 
						}
					}

					if (!InRowset) 
					{
						// See if we're reached a possible start of a new rowset.  
						// Check for both "-----" (osql) and "~~~~~" (VMSTAT) as a column header indicator. 
						// HACK: In order to handle the blocker script's non-tabular DBCC OPENTRAN output we 
						// also check for the string "DBCC OPENTRAN FOR".  
						// TODO: Modify blocker script to use WITH TABLE_RESULTS for DBCC OPENTRAN so that 
						// it can be imported like every other rowset without this hack. Remove this once that 
                        // change has been made. 
                        
						if ((line != null) && (line.Trim().Length > 3) && (
							(line.TrimStart().Substring(0, 2).Equals("--"))
                            || (line.TrimStart().Substring(0, 2).Equals("~~"))
							//|| ((line.Length > 17) && (line.Substring(0, 17).Equals("DBCC OPENTRAN FOR")))
							))
							InRowset = CheckForRowsetStart(line, lineprev1, lineprev2, false);
						
						// Keep track of the previous two lines -- we'll need them when we encounter a new rowset. 
						lineprev2=lineprev1;
						lineprev1=line;
					}
					// Make sure the host hasn't asked us to stop. 
					if (this.Cancelled) 
					{
						this.State = ImportState.Canceling;
						break;
					}


                    // If our parent provided us with a progress update delegate, notify him of % complete status
                    bool ShouldUpdate = false;

                    if (this.TotalLinesProcessed > 100000)
                    {
                        if (0 == (this.TotalLinesProcessed % 10000))
                        {
                            ShouldUpdate = true;
                        }

                    }
                    else if (this.TotalLinesProcessed > 30000)
                    {
                        if (0 == (this.TotalLinesProcessed % 5000))
                        {
                            ShouldUpdate = true;
                        }

                    }

                    else if (this.TotalLinesProcessed > 10000)
                    {
                        if (0 == (this.TotalLinesProcessed % 3000))
                        {
                            ShouldUpdate = true;
                        }

                    }
                    else 
                    {
                        if (0 == (this.TotalLinesProcessed % 1000))
                        {
                            ShouldUpdate = true;
                        }
                    }

                    //now update progress of rows processed
                    if (ShouldUpdate == true)
                    {
                        if (0 == this.CurrentPosition)
                            PercentComplete = 0;
                        else if (0 == this.FileSize)
                            PercentComplete = 100;
                        else
                            PercentComplete = Convert.ToInt32(Convert.ToInt64(100) * this.CurrentPosition / this.FileSize);
                        if (!(null == m_ProgressUpdateFunction))
                        {
                            m_ProgressUpdateFunction(PercentComplete);
                        }
                    } //end of if totallinesprocessed

                } //end of while

				return;
			}
			catch (Exception e)
			{
                ErrorDialog ed = new ErrorDialog(e, false, this.logger);
				ed.Handle();
			}
			finally 
			{
                FlushRowsets();
				if (this.CurrentRowset != null) CurrentRowset.Clear();
				this.CurrentRowset = null;
			}
			return;
		}

        /// <summary>
        /// Detect the start of a new rowset.  Expected format is: 
        ///
        ///   ROWSETNAME
        ///   Column1  Column2 Column3  ...
        ///   -------- ------- -------- ...
        /// </summary>
        /// <param name="ColumnLines">In the above example, "-------- ------- ..."</param>
        /// <param name="ColumnNames">In the above example, "Column1  Column2 ..."</param>
        /// <param name="RowsetIdentifier">In the above example, "ROWSETNAME"</param>
        /// <param name="NonTabularOnly">Rowset doesn't fit the pattern of a "normal" fixed-width rowset; check nontablular rowsets only</param>
        /// <returns></returns>
        private bool CheckForRowsetStart (string ColumnLines, string ColumnNames, string RowsetIdentifier, bool NonTabularOnly)
		{
			ArrayList rowsets;

			try
			{
				// See whether any of the known rowsets "claims" this one. 
				if (NonTabularOnly)
					rowsets = this.KnownNonTabularRowsets;
				else
					rowsets = this.KnownRowsets;
				foreach (TextRowset r in rowsets)
				{
					if (r.CheckForRowsetStart(ColumnLines, ColumnNames, RowsetIdentifier))
					{
						this.CurrentRowset = r;
						break;
					}
				}
	
				if (!(null == this.CurrentRowset)) 
				{
					// We've found a rowset object that claims the current rowset. 
					this.CurrentRowset.DefineRowsetColumns(ColumnLines, ColumnNames);
					// Issue a DROP TABLE for this rowset if this is the first time we've encountered it and if 
					// we're not supposed to be appending rows to existing tables. 
					if ((this.dropTables) && (!this.CurrentRowset.HasBeenEncountered))
						DropCurrentTable();
					// Create table in SQL if this is the first time we've run into it. 
					if (!this.CurrentRowset.HasBeenEncountered)
					{
                        logger.LogMessage("First time encoutering row identifier '" + RowsetIdentifier + "'");
						CreateTable();
					}
					// If we don't have an in-progress bulk load for the rowset, start one. 
					if (!this.CurrentRowset.InBCPRowset)
					{
						if (!SetUpBulkLoadRowset(this.CurrentRowset))
						    return false;
					}
					// We're all set. Mark this rowset as "encountered" so we don't drop it again next time.
					this.CurrentRowset.HasBeenEncountered = true;
					return true;
				}
				else 
					return false; 
			}
			catch (Exception e)
			{
                ErrorDialog ed = new ErrorDialog(e, true, this.logger);
				ed.Handle();
			}
			return true;
		}

		private bool SetUpBulkLoadRowset (TextRowset rowset) 
		{

            rowset.Bulkload = new BulkLoadEx.BulkLoadRowset(rowset.Name, connStr);
			rowset.InBCPRowset = true;
			return true;
		}

        private void InsertRow()
        {
            //bool	ret = false;
            int     ColNum = 0;
            object  ColData;

            try
            {
                DataRow row = CurrentRowset.Bulkload.GetNewRow();

                foreach (RowsetImportEngine.Column c in CurrentRowset.Columns)
                {
                    // Auto-number logic
                    if (c.Name.Equals("id", StringComparison.OrdinalIgnoreCase) && c.RowsetLevel)
                        ColData = this.CurrentRowset.RowsInserted + 1;
                    else
                        ColData = c.Data;

                    // Normalize data
                    if (ColData == null || ColData.ToString().Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase))
                    {
                        row[c.Name] = DBNull.Value;
                    }
                    else if (c.DataType == SqlDbType.VarChar || c.DataType == SqlDbType.NVarChar)
                    {
                        row[c.Name] = ColData.ToString().Trim(); // Trim strings
                    }
                    else
                    {
                        row[c.Name] = ColData; // Keep original for numeric/date types
                    }


                    // TODO: If we fail to set row data, log row as failed along with BulkLoad.GetErrorMessage().
                    ColNum++;
                }

                CurrentRowset.Bulkload.InsertRow(row);
                this.CurrentRowset.RowsInserted++;
                TotalRowsInserted++;
                return;
            }

            catch (Exception ex)
            {
                logger.LogMessage($"Inserting a row failed. Error: {ex.Message}\r\n {ex.StackTrace}");
                throw; // rethrow preserves original exception

            }

        }

        // Periodically flush the BCP batch for all active rowsets so that we don't 
        // suffer from excessive tran log autogrow. 
        private void FlushRowsets  ()
		{
			//long ret;
			foreach (TextRowset r in this.KnownRowsets)
			{
                if (r.InBCPRowset && r.Bulkload != null)
                {
                    try
                    {
                        logger.LogMessage("Flushing rowset " + r.Name);
                        r.Bulkload.Flush();
                    }
                    catch (SqlTypeException ex)
                    {
                        logger.LogMessage("Flushing rowset failed for " + r.Name + ex.ToString());

                    }
                    
                    
                }
			}
		}

		// Open the input text file for reading. 
		private bool OpenFile ()
		{
			try 
			{
				string line;
				System.Text.Encoding encoding;

				System.Diagnostics.Debug.WriteLine("Opening file " + this.filename);
                this.logger.LogMessage("Opening file " + this.filename);
				FileInfo fi = new FileInfo(this.filename);
				this.FileSize = fi.Length;
				this.CurrentPosition = 0;
				sr = new StreamReader (this.filename, true);
				// Read a line to get StreamReader to auto-detect the encoding based on BOM
				line = sr.ReadLine();  
				encoding = sr.CurrentEncoding;
				// StreamReader will correctly detect Unicode vs. non-Unicode files 
				// if the file has been written with a proper BOM.  sqlcmd and osql 
				// both include a BOM in their output files.  Some other apps may not.  
				// StreamReader will default to UTF8 encoding for a file without a 
				// BOM, and this won't work for UCS-2 formatted output files w/o a BOM.  
				// If we find that the detected encoding is UTF-8, read the first 
				// (and optionally second) line and check the second byte in each line.  
				// If byte #2 is NULL, assume that the file is little-endian UCS-2 
				// Unicode.  This is an extremely crude auto-detection algorithm but 
				// will suffice in the absence of an IsTextUnicode()-equivalent in .NET. 
				System.Diagnostics.Debug.WriteLine("StreamReader detected encoding: " + encoding.ToString());
				if (sr.CurrentEncoding.ToString() == "System.Text.UTF8Encoding")
				{
					if ((null != line) && (line.Length < 2)) 
						line = sr.ReadLine();	// Read the second line if the first is <= 2 bytes long
					if ((null != line) && (line.Length >= 2) && ('\0' == line[1]))
						encoding = new System.Text.UnicodeEncoding(false, false);
					else
						// If not UCS-2, assume ANSI instead of UTF-8. 
						encoding = System.Text.Encoding.Default;
				}
				// We've done our best to detect the encoding even in the absence of 
				// a BOM. Close and reopen the file with the desired encoding. 
				sr.Close();
				System.Diagnostics.Debug.WriteLine("Auto-detected encoding: " + encoding.ToString());
				sr = new StreamReader (this.filename, encoding);
			}
			catch (Exception e)
			{
                ErrorDialog ed = new ErrorDialog(e, false, this.logger);
				ed.Handle();
				return false;
			}
			return true;
		}

		// Close the input text file
		private void CloseFile ()
		{
			try 
			{
				this.sr.Close();
			}
			catch 
			{
				// Ignore errors here.
			}
		}

		// Open connection to the SQL server
		private bool OpenSQLConnection () 
		{
			// Try to connect. 
			try 
			{
				// Make sure we're not already connected.
				if (0 == (cn.State & System.Data.ConnectionState.Open))
				{
                    if (0 == this.connStr.Length)
                    {
                        cn.ConnectionString = "App=RowsetImport;Packet Size=4096;Persist Security Info=false;Data Source="
                            + this.server + ";";
                        // Connect using either Windows or SQL authentication. 
                        if (this.usewindowsauth)
                            cn.ConnectionString += "Integrated Security=true;";
                        else
                            cn.ConnectionString += "Integrated Security=false;User ID=" + this.sqllogin
                                + ";Password=" + this.sqlpassword;
                    }
                    else
                    {
                        cn.ConnectionString = this.connStr;
                    }
					cn.Open();
				}
			} 
			catch (Exception e)
			{
				// Error: unable to connect
                ErrorDialog ed = new ErrorDialog(e, false, this.logger);
				ed.Handle();
				return false;
			}
			return true;
		}


		
		// Close connection to SQL server
	

		private bool CloseBulkLoadRowset (TextRowset rowset)
		{
            bool ret = true;
            try
            {
                rowset.Bulkload.Close();
            }
            catch (Exception e)
            {
                ret = false;
                logger.LogMessage(e.ToString(), MessageOptions.Silent);
            }

            return ret;   
			
		}

		

		// The list of rowsets we know how to interpret and their properties are stored in XML files 
		// named TextRowsets.XML ("built-in") and TextRowsetsCustom.XML (user-defined). Read these files 
		// and use the info to populate the KnownRowsets collection. 
		private bool GetRowsetProperties ()
		{
			return (ReadRowsetPropertiesFromXml ("TextRowsets.xml", false) 
				&& ReadRowsetPropertiesFromXml ("TextRowsetsCustom.xml", true));
		}


       private  bool IsLegalTableName(String s)
        {
           //will not do anything. this has caused quite some questions on  alias
           /* 
           Regex re = new Regex(@"^[A-Za-z]\w+$");
            if (!re.IsMatch(s))
                return false;
            if (s.Length >= 128)
                return false;
            */
            return true;
        }

        // The list of rowsets we know how to interpret and their properties are stored in XML files. 
        // Read the file and use the info to populate the KnownRowsets collection. 
        private bool ReadRowsetPropertiesFromXml(string sXmlRowsetFile, bool bOptional)
        {
            ColumnTypes ct = new ColumnTypes();
            RowsetTypes rt = new RowsetTypes();
            TextRowset rowset = null;
            Column col = null;
            string RowsetName = "";
            string RowsetType = "";
            string RowsetIdentifier = "";
            string ColumnName = "";
            string ColumnType = "";
            string ColumnDefineToken = "";
            string ColumnValueToken = "";
            string ColumnRowsetLevel = "";
            int ColumnLength = 0;
            string sXmlRowsetFile2 = "";
            bool RowsetEnabled = true;
            System.Xml.XmlTextReader xr = null;

            // First check the .exe dir for TextRowsets.xml.  If not found, check current directory. 
            sXmlRowsetFile2 = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + @"\" + sXmlRowsetFile;
            String sXmlRowsetFile3 = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\sqlnexus\" + sXmlRowsetFile;
            //need some special handling of TextRowsets.xml

            /*
             * do not use resource file for ease of editing
            if (string.Compare (sXmlRowsetFile, "TextRowsets.xml", true) ==0 )
            {
                String RowsetText = Resources.TextRowsets;
                xr = new XmlTextReader(new StringReader(RowsetText));

            }*/

            if (File.Exists(sXmlRowsetFile2))
                xr = new XmlTextReader(sXmlRowsetFile2);
            else if (File.Exists(sXmlRowsetFile))
                xr = new XmlTextReader(sXmlRowsetFile);
            else if (File.Exists(sXmlRowsetFile3))
                xr = new XmlTextReader(sXmlRowsetFile3);
            else
            {
                // If this rowset definition file was optional, we don't want to raise an 
                // error just because it wasn't found. Return true (success) in this case. 
                if (!bOptional)
                {
                    ErrorDialog ed = new ErrorDialog("Could not find rowset definition file (" + sXmlRowsetFile + ").", false, this.logger);
                    ed.Handle();
                }
                return bOptional;
            }

            try
            {
                xr.Read();
                while (!xr.EOF)
                {
                    if (xr.IsStartElement("Rowset"))
                    {
                        // Read rowset attributes
                        while (xr.MoveToNextAttribute())
                        {
                            switch (xr.Name)
                            {
                                case "name": RowsetName = xr.Value; break;

                                case "type": RowsetType = xr.Value; break;
                                case "identifier": RowsetIdentifier = xr.Value; break;
                                // if enabled="false", skip this rowset
                                case "enabled":
                                    if (xr.Value.Trim().ToUpper().Equals("FALSE"))
                                        RowsetEnabled = false;
                                    else
                                        RowsetEnabled = true;
                                    break;
                            }
                            if (!IsLegalTableName(RowsetName))
                            {
                                string dlgTitle = "Incorrect Table Name";
                                this.logger.LogMessage("You provided an illegal table name (likely in your TextRowsetsCustom.xml).\n\r Consider deleting that file from %appdata%\\sqlnexus directory \r\n " + RowsetName, MessageOptions.Silent, TraceEventType.Information, dlgTitle);

                                //just log it and don't fail
                                //return false;
                            }

                        }

                        // If the rowset is defined but not enabled, skip it. 
                        if (!RowsetEnabled)
                        {
                            xr.Skip();
                            // Default to enabled for next rowset
                            RowsetEnabled = true;
                            continue;
                        }

                        // Locate the matching rowset class
                        bool RowsetFound = false;
                        foreach (TextRowset r in rt.KnownRowsetTypes)
                        {
                            if (r.GetType().ToString() == RowsetType)
                            {   // found the rowset type -- make a copy to add to the KnownRowsets collection
                                RowsetFound = true;
                                rowset = (TextRowset)(r.Copy());
                                rowset.Name = RowsetName;
                                rowset.Identifer = RowsetIdentifier;
                                break;
                            }
                        }
                        if (!RowsetFound)
                        {   // We didn't find the rowset type -- skip column processing for the rowset. 
                            xr.Skip();
                            continue;
                        }

                        // Read past the KnownColumns element until we reach the Columns list.  Keep an eye out 
                        // for a new Rowset (it's possible that a rowset could not have any KnownColumns).
                        while (!xr.EOF && !xr.Name.Equals("Column") && !xr.Name.Equals("Rowset") && xr.Read()) { }
                        // Read in each column
                        while (xr.IsStartElement("Column") && !xr.EOF)
                        {
                            ColumnName = "";
                            ColumnLength = 0;
                            ColumnType = "";
                            ColumnDefineToken = "";
                            ColumnValueToken = "";
                            ColumnRowsetLevel = "false";

                            // Read column attributes
                            while (xr.MoveToNextAttribute())
                            {
                                switch (xr.Name)
                                {
                                    case "name": ColumnName = xr.Value; break;
                                    case "type": ColumnType = xr.Value; break;
                                    case "length": ColumnLength = Convert.ToInt32(xr.Value, 10); break;
                                    case "rowsetlevel": ColumnRowsetLevel = xr.Value; break;
                                    case "definetoken": ColumnDefineToken = xr.Value; break;
                                    case "valuetoken": ColumnValueToken = xr.Value; break;
                                }
                                if (("" != ColumnDefineToken) || ("" != ColumnValueToken))
                                    rowset.UsesTokens = true;
                            }
                            // Locate the matching column class
                            bool ColumnFound = false;
                            foreach (Column c in ct.KnownColumnTypes)
                            {
                                if (c.GetType().ToString() == ColumnType)
                                {   // found the column type -- make a copy to add to the KnownColumns collection
                                    ColumnFound = true;
                                    col = (Column)(c.Copy());
                                    col.Name = ColumnName;
                                    col.DefineToken = ColumnDefineToken;
                                    col.ValueToken = ColumnValueToken;
                                    // Some rowsets (e.g. DBCC INPUTBUFFER) have variable-width columns. For these 
                                    // columns, TextRowsets.xml allows specifying a column width wide enough to 
                                    // accommodate all possible widths.  If a col width was specified, we'll use it 
                                    // instead of basing SQL column width on the width of the column in the input file. 

                                    //this is to allow NVARCHAR(MAX)
                                    //if (ColumnLength>0) 
                                    col.SqlColumnLength = ColumnLength;
                                    // We normally determine column width dynamically based on the observed column 
                                    // header width in the input data file. However, non-tabular rowsets don't have column 
                                    // headers, so we always need to set their column width explicitly. 
                                    if (!rowset.TabularRowset)
                                    {
                                        if (ColumnLength > 0)
                                            col.Length = ColumnLength;
                                        else
                                            col.Length = DEFAULT_NONTAB_COLUMN_LEN;
                                    }
                                    col.RowsetLevel = ColumnRowsetLevel.Trim().ToUpper().Equals("TRUE") ? true : false;
                                    rowset.KnownColumns.Add(col);
                                    break;
                                }
                            }
                            if (!ColumnFound)
                            {   // We didn't find the column type -- skip it. 
                                xr.Skip();
                            }
                            xr.Read();

                        }//end while


                        // If we get here we should have a valid TextRowset with a populated KnownColumns collection.
                        // Add the rowset to the KnownRowsets collection. 
                        this.KnownRowsets.Add(rowset);

                        if (!rowset.TabularRowset)
                            this.KnownNonTabularRowsets.Add(rowset);
                    }
                    // Make sure we don't read past a rowset or past the end of the file. 
                    if (!xr.EOF)
                    {
                        if (!xr.EOF && !xr.IsStartElement("Rowset"))
                            xr.Read();
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                ErrorDialog ed = new ErrorDialog(e, false, this.logger);
                ed.Handle();
                return false;
            }
        }

		private void WriteRowsetPropertiesToXml ()
		{
            throw new NotImplementedException("this method is not implemented");
			// Not implemented unless new UI elements are added to allow modifying rowset properties in the GUI. 
		/*
			RowsetTypes rt = new RowsetFinder();
			System.Xml.XmlTextWriter xw = new XmlTextWriter("TextRowsets.xml", System.Text.Encoding.UTF8);
			xw.WriteStartDocument(true);
			xw.WriteStartElement("TextImport");
			xw.WriteStartElement("KnownRowsets");
			foreach (TextRowset r in rf.KnownRowsetTypes)
			{
				xw.WriteStartElement("Rowset");
				xw.WriteAttributeString("name", r.Name);
				xw.WriteAttributeString("type", r.GetType().ToString());
				xw.WriteStartElement("KnownColumns");
				foreach (Column c in r.KnownColumns)
				{
					xw.WriteStartElement("Column");
					xw.WriteAttributeString("name", c.Name);
					xw.WriteAttributeString("type", c.GetType().ToString()); 
					xw.WriteAttributeString("rowsetlevel", c.RowsetLevel.ToString());
					xw.WriteEndElement(); // close Column
				}
				xw.WriteEndElement(); // close KnownColumns
				xw.WriteEndElement(); // close Rowset
			}
			xw.WriteEndElement(); // close KnownRowsets
			xw.WriteEndElement(); // close TextImport (root)
			xw.Close();
		*/
		}


        #region INexusImporter Members


        public event EventHandler StatusChanged;

        public virtual void OnStatusChanged(EventArgs e)
        {
            if (null != StatusChanged)
            {
                StatusChanged(this, e);
            }
        }

        Dictionary<string, object> options = new Dictionary<string, object>();

        public Dictionary<string, object> Options
        {
            get
            {
                return options;
            }
        }

        public System.Windows.Forms.Form OptionsDialog
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        #endregion

        #region INexusProgressReporter Members

        public event EventHandler ProgressChanged;

        public virtual void OnProgressChanged(EventArgs e)
        {
            if (null != ProgressChanged)
            {
                ProgressChanged(this, e);
            }
        }

        #endregion

        #region INexusImporter Members


        public string Name
        {
            get { return "Rowset Importer"; }
        }

        #endregion

        #region INexusImporter Members

        public Guid ID
        {
            get 
            {
                return new Guid("F9533832-8CC5-4a38-9778-F88DB6F5AC89");
            }
        }

        #endregion
    }
}
