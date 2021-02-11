using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
//using Microsoft.SqlServer.MessageBox;
using System.Diagnostics;
using System.Windows.Forms;

namespace NexusInterfaces
{
    [Flags]
    public enum MessageOptions
    { 
        
        StatusBar=2,
        Silent=4,
        Dialog=8,
        Both=StatusBar|Silent,
        All = StatusBar|Silent|Dialog,
        NoLog = StatusBar
    }

    public interface ILogger
    {
        TraceSource TraceLogger
        {
            get;
        }
        void InitializeLog(string logfilename);
        void LogMessage(string msg);
        void LogMessage(string msg, MessageOptions options);

        void LogMessage(string msg, MessageOptions options, TraceEventType eventtype);
        void LogMessage(string msg, string[] args);
        void LogMessage(string msg, string[] args, MessageOptions options, TraceEventType eventtype);
        void ClearMessage();
        DialogResult LogMessage(string msg, string title, MessageBoxButtons buttons);
    }

    public interface INexusImportedRowset
    {
        string Name
        {
            get;
            set;
        }
        long RowsInserted
        {
            get;
            set;
        }
    }

    public enum ImportState
    {
        Idle = 0,
        NoFiles,
        OpeningFile,
        OpeningDatabaseConnection,
        CreatingDatabase,
        Canceling,
        Importing,
        ClosingFile
    }

    public interface INexusFileSizeReporter
    {
        long FileSize							// Size of input file
        {
            get;
        }
    }
    public interface INexusProgressReporter
    {
        long CurrentPosition					// Byte position in input file
        {
            get;
        }
        event EventHandler ProgressChanged;
        void OnProgressChanged(EventArgs e);
    }

    public interface INexusImporter
    {
        Guid ID
        {
            get;
        }
        void Initialize(string Filemask, string connString, string Server, bool UseWindowsAuth, string SQLLogin, string SQLPassword, string DatabaseName, ILogger Logger);
        Dictionary<string, object> Options
        {
            get;
        }
        Form OptionsDialog
        {
            get;
        }
        string[] SupportedMasks
        {
            get;
        }
        string[] PreScripts
        {
            get;
        }
        string[] PostScripts
        {
            get;
        }
        ImportState State  // Host can check this to see current state
        {
            get;
        }
        bool Cancelled 			 // Will be set to true if the current import has been cancelled
        {
            get;
        }
        ArrayList KnownRowsets   // List of the rowsets we know how to interpret
        {
            get;
        }
        long TotalRowsInserted	 // Number of rows inserted since import began
        {
            get;
        }
        long TotalLinesProcessed // Number of lines in the input file processed
        {
            get;
        }
        string Name
        {
            get;
        }

        bool DoImport();
        void Cancel();
        event EventHandler StatusChanged;
        void OnStatusChanged(EventArgs e);
    }
    public interface INexusFileImporter : INexusImporter, INexusProgressReporter, INexusFileSizeReporter
    {
    }

    public interface INexusAnalysis
    {
        string[] Reports
        {
            get;
        }
        void Initialize(string connString);
        TabPage LoadPage(string analysisName);
        void ShowPage(string analysisName);
        ToolStrip[] Toolbars
        {
            get;
        }
        MenuStrip[] Menus
        {
            get;
        }

    }
}
