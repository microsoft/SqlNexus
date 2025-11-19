using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using NexusInterfaces;

namespace sqlnexus
{
	/// <summary>
	/// Summary description for frmImportSummary.
	/// </summary>
	public class frmImportSummary : System.Windows.Forms.Form
	{
		public System.Windows.Forms.ListView listImportSummary;
		private System.Windows.Forms.ColumnHeader columnHeaderRowset;
		private System.Windows.Forms.ColumnHeader columnHeaderRowcount;
		private System.Windows.Forms.Button cmdOK;
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;
		private INexusImporter ri; 

		private frmImportSummary()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
            g_theme.fRec_setControlColors(this);
        }

		public frmImportSummary(INexusImporter ri)
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			this.ri = ri;
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmImportSummary));
            this.listImportSummary = new System.Windows.Forms.ListView();
            this.columnHeaderRowset = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderRowcount = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.cmdOK = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // listImportSummary
            // 
            this.listImportSummary.AllowColumnReorder = true;
            this.listImportSummary.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listImportSummary.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderRowset,
            this.columnHeaderRowcount});
            this.listImportSummary.FullRowSelect = true;
            this.listImportSummary.GridLines = true;
            this.listImportSummary.HideSelection = false;
            this.listImportSummary.LabelWrap = false;
            this.listImportSummary.Location = new System.Drawing.Point(13, 11);
            this.listImportSummary.Name = "listImportSummary";
            this.listImportSummary.Size = new System.Drawing.Size(391, 193);
            this.listImportSummary.Sorting = System.Windows.Forms.SortOrder.Descending;
            this.listImportSummary.TabIndex = 1;
            this.listImportSummary.UseCompatibleStateImageBehavior = false;
            this.listImportSummary.View = System.Windows.Forms.View.Details;
            // 
            // columnHeaderRowset
            // 
            this.columnHeaderRowset.Text = "Rowset";
            this.columnHeaderRowset.Width = 279;
            // 
            // columnHeaderRowcount
            // 
            this.columnHeaderRowcount.Text = "RowsImported";
            this.columnHeaderRowcount.Width = 110;
            // 
            // cmdOK
            // 
            this.cmdOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.cmdOK.Location = new System.Drawing.Point(296, 215);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(108, 33);
            this.cmdOK.TabIndex = 0;
            this.cmdOK.Text = "&OK";
            this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
            // 
            // frmImportSummary
            // 
            this.AcceptButton = this.cmdOK;
            this.AutoScaleBaseSize = new System.Drawing.Size(8, 19);
            this.CancelButton = this.cmdOK;
            this.ClientSize = new System.Drawing.Size(416, 266);
            this.Controls.Add(this.cmdOK);
            this.Controls.Add(this.listImportSummary);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimizeBox = false;
            this.Name = "frmImportSummary";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Import Summary";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.frmImportSummary_Load);
            this.ResumeLayout(false);

		}
		#endregion

		private void cmdOK_Click(object sender, System.EventArgs e)
		{
			this.Close();
		}

		private void frmImportSummary_Load(object sender, System.EventArgs e)
		{
			// For each of the rowsets in the KnownRowsets collection, add the rowset name 
			// and number of rows we just imported. 
			this.listImportSummary.Items.Clear();
			foreach (INexusImportedRowset r in ri.KnownRowsets)
			{
				string[] itemstrings = new string[2] {r.Name, r.RowsInserted.ToString()};
				this.listImportSummary.Items.Add(new ListViewItem(itemstrings)); 
			}
			this.listImportSummary.Sorting = SortOrder.None;
			this.listImportSummary.View = View.Details;
			this.listImportSummary.ListViewItemSorter = new ListViewIntComparer (1);
			this.listImportSummary.Sort();
		}
	}

	// Implements the manual sorting of items by columns (listview default is to sort 
	// as strings, but we need to sort as int).
	class ListViewIntComparer : IComparer 
	{
		private int col;
		public ListViewIntComparer() 
		{
			col=0;
		}
		public ListViewIntComparer(int column) 
		{
			col=column;
		}
		public int Compare(object x, object y) 
		{
			try
			{
				// First try to compare the columns as integers
				int ix = Convert.ToInt32(((ListViewItem)x).SubItems[col].Text);
				int iy = Convert.ToInt32(((ListViewItem)y).SubItems[col].Text);
				if (ix < iy) return 1;
				else if (ix == iy) return 0;
				else return -1; // (ix > iy) 
			}
			catch
			{
				// Fall back on doing a string compare
				return String.Compare(((ListViewItem)x).SubItems[col].Text, ((ListViewItem)y).SubItems[col].Text);
			}
		}
	}
}
