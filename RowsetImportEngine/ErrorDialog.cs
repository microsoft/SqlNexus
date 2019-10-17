#define TRACE

using System;
using System.IO;
using System.Windows.Forms;
using NexusInterfaces;

namespace RowsetImportEngine
{
	/// <summary>
	/// Summary description for ErrorDialog.
	/// </summary>
	public class ErrorDialog
	{
		private Exception exception;
		private string error;
		private bool isIgnorable;
        private ILogger logger;

		public ErrorDialog (Exception e, bool IsIgnorable, ILogger Logger)
		{
			this.exception = e;
			this.isIgnorable = IsIgnorable;
			this.error = e.ToString();
            this.logger = Logger;
		}
		public ErrorDialog (string Error, bool IsIgnorable, ILogger Logger)
		{
			this.exception = null;
			this.isIgnorable = IsIgnorable;
			this.error = Error;
            this.logger = Logger;
        }
        /// <summary>
        /// If error is ignorable, Handle() will ask the user if they want to continue.  If error is not ignorable or 
        /// if user chooses to fail, the exception is rethrown.  ErrorDialog ensures that the user is not notified 
        /// more than once of the same exception, even if the rethrown exception is caught and passed back to a new 
        /// ErrorDialog instance. 
        /// </summary>
		public virtual void Handle ()
		{
			DialogResult result;
			string caption; 
			string msg; 
			MessageBoxButtons buttons;

			// Check if we're handling an exception that we've already shown the user the error for.  
			// If so, simply re-throw. 
			if (this.exception != null)
				if (this.exception.GetType().ToString().Equals ("RowsetImportEngine.RowsetImportException"))
					if ((this.exception as RowsetImportException).DialogShown)
						throw (this.exception);

			caption = Path.GetFileName(System.Windows.Forms.Application.ExecutablePath) + " Error";
			
			msg = "An unexpected error has occurred: \n\n" + this.error;
			if (this.isIgnorable)
				msg += "\n\nDo you want to try to continue anyway?";
            else
                msg += "\n\nImport canceled.";
			
			buttons = 
				isIgnorable ? 
				System.Windows.Forms.MessageBoxButtons.YesNo : 
				System.Windows.Forms.MessageBoxButtons.OK;

            System.Diagnostics.Trace.WriteLine (@"TraceBuster Error: " + msg);
            // TODO: mod NexusInterfaces to allow ignorable errors and provide a "Yes (Continue) for All" option
			//result = System.Windows.Forms.MessageBox.Show(msg, caption, buttons);
            if (!(logger == null))
                logger.LogMessage(string.Format("RowsetImportEngine Error: {0}", msg));
            result = (isIgnorable ? DialogResult.Yes : DialogResult.OK);

			if (result == DialogResult.Yes)
				return;	// User asked to continue anyway. 
			else
			{
				RowsetImportException newex = new RowsetImportException("RowsetImport failed.", this.exception);
				newex.DialogShown = true;
				throw (newex);
			}
		}
	}

    /// <summary>
    /// Custom app exception class, needed primarily to prevent repeated notification of the same exception
    /// </summary>
	public class RowsetImportException : ApplicationException
	{
		public bool DialogShown = false;
		public RowsetImportException ()	{}
		public RowsetImportException (string message) : base(message) {}
		public RowsetImportException (string message, Exception inner) : base(message, inner) {}
	}
}
