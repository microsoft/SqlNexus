using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using System.Collections;
using System.Xml;
using System.Data;
using System.Diagnostics;
using Microsoft.Reporting.WinForms;
using System.Data.SqlClient;
using System.Globalization;

namespace sqlnexus
{
	/// <summary>
	/// Summary description for fmParameters.
	/// </summary>
	public class fmParameters : System.Windows.Forms.Form
    {
		private System.Windows.Forms.Panel paBottom;
		private System.Windows.Forms.Button btOK;
		private System.Windows.Forms.Button btCancel;
        private DataGridView dvParams;
        private IContainer components;

		public fmParameters()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

			//
			// TODO: Add any constructor code after InitializeComponent call
			//
		}

        public static bool HasReportParameters(LocalReport report)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(report.ReportPath);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2005/01/reportdefinition");

            XmlNodeList nodes = doc.DocumentElement.SelectNodes("//rds:Report//rds:ReportParameters/rds:ReportParameter[rds:Hidden='false' or not (rds:Hidden)]", nsmgr);

            return ((null != nodes) && (0 != nodes.Count));

        }

        public static bool GetReportParameters(LocalReport report, ILogger logger)
        {

            fmParameters fmP = new fmParameters();

            XmlDocument doc = new XmlDocument();
            doc.Load(report.ReportPath);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2005/01/reportdefinition");

            XmlNodeList nodes = doc.DocumentElement.SelectNodes("//rds:Report//rds:ReportParameters/rds:ReportParameter[rds:Hidden='false' or not (rds:Hidden)]", nsmgr);

            //If no params, bail
            if ((null == nodes) || (0 == nodes.Count))
            {
                logger.LogMessage(sqlnexus.Properties.Resources.Msg_NoParams, MessageOptions.Dialog);
                return false;
            }

            object[] row = new object[nodes.Count];
            int i = 0;
            foreach (XmlNode node in nodes)
            {
                DataGridViewColumn col;

                XmlNode valnode = node.SelectSingleNode("rds:ValidValues", nsmgr);

                col = null;
                if (null != valnode) //value list
                {
                    col = new DataGridViewComboBoxColumn();

                    XmlNode dsnode = valnode.SelectSingleNode("rds:DataSetReference/rds:DataSetName", nsmgr);
                    if (null != dsnode)  //value list from dataset
                    {
                        XmlNode vfnode = valnode.SelectSingleNode("rds:DataSetReference/rds:ValueField", nsmgr);
                        System.Diagnostics.Debug.Assert(null != vfnode);

                        DataTable dt = new DataTable();
                        SqlDataAdapter da = new SqlDataAdapter(dsnode.InnerText, Globals.ConnectionString);
                        da.Fill(dt);
                        foreach (DataRow r in dt.Rows)
                        {
                            ((DataGridViewComboBoxColumn)col).Items.Add(r[vfnode.InnerText].ToString());
                        }
                    }
                    else
                    {
                        XmlNodeList valnodes = valnode.SelectNodes("rds:ParameterValues//rds:Value", nsmgr);
                        if ((null != valnodes) && (0 != valnodes.Count))
                        {
                            foreach (XmlNode vnode in valnodes)
                            {
                                ((DataGridViewComboBoxColumn)col).Items.Add(vnode.InnerText);
                            }
                        }
                    }

                }

                //Get the default value if there is one
                string defval=null;

                //Check for dataset first
                XmlNode dsetnode = node.SelectSingleNode("rds:DefaultValue/rds:DataSetReference/rds:DataSetName", nsmgr);
                if (null != dsetnode)  //value from dataset
                {
                    XmlNode vfnode = node.SelectSingleNode("rds:DefaultValue/rds:DataSetReference/rds:ValueField", nsmgr);
                    System.Diagnostics.Debug.Assert(null != vfnode);

                    DataTable dt = new DataTable();
                    SqlDataAdapter da = new SqlDataAdapter(dsetnode.InnerText, Globals.ConnectionString);
                    da.Fill(dt);
                    defval = dt.Rows[0][vfnode.InnerText].ToString();
                }
                else
                {
                    XmlNode defvalue = node.SelectSingleNode("rds:DefaultValue//rds:Value", nsmgr);
                    if (null != defvalue)
                        defval = defvalue.InnerText;
                }

                object def;
                if (0 == string.Compare(node["DataType"].InnerText, "datetime", true, CultureInfo.InvariantCulture))
                {
                    def = DateTime.Now;
                    if (null == col)
                        col = new DataGridViewCalendarColumn();

                    col.DefaultCellStyle.Format = "s";

//                    ((DataGridViewCalendarColumn)col).

                    if (null == defval)
                        row[i] = def;
                    else
                        row[i] = Convert.ToDateTime(defval);
                }
                else if (0 == string.Compare(node["DataType"].InnerText, "boolean", true, CultureInfo.InvariantCulture))
                {
                    def = false;
                    if (null == col)
                        col = new DataGridViewCheckBoxColumn();

                    if (null == defval)
                        row[i] = def;
                    else
                        row[i] = Convert.ToBoolean(defval);
                }
                else if (0 == string.Compare(node["DataType"].InnerText, "integer", true, CultureInfo.InvariantCulture))
                {
                    def = "0";  //Masked text box requires strings
                    if (null == col)
                    {
                        col = new MaskedTextBoxColumn();
                        ((MaskedTextBoxColumn)col).Mask = "##########";
                        ((MaskedTextBoxColumn)col).TextAlign = HorizontalAlignment.Right;
                    }

                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    col.DefaultCellStyle.Format = "d";

                    if (null == defval)
                        row[i] = def;
                    else
                        row[i] = defval;
                }
                else if (0 == string.Compare(node["DataType"].InnerText, "float", true, CultureInfo.InvariantCulture))
                {
                    def = "0.00";  //Masked text box requires strings
                    if (null == col)
                    {
                        col = new MaskedTextBoxColumn();
                        ((MaskedTextBoxColumn)col).Mask = "##########.00";
                        ((MaskedTextBoxColumn)col).TextAlign = HorizontalAlignment.Right;
                    }
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                    col.DefaultCellStyle.Format = "f";

                    if (null == defval)
                        row[i] = def;
                    else
                        row[i] = defval;
                }
                else  //string
                {
                    def = "";
                    if (null == col)
                        col = new DataGridViewTextBoxColumn();

                    if (null == defval)
                        row[i] = def;
                    else
                        row[i] = defval;
                }

                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                col.DefaultCellStyle.NullValue = def;
                col.ValueType = def.GetType();
                col.Name = node.Attributes["Name"].Value;
                try
                {
                    col.ToolTipText = node["Prompt"].InnerText;
                }
                catch (Exception ex)  //May not have a prompt
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
                fmP.dvParams.Columns.Add(col);

                i++;
            }
            fmP.dvParams.Rows.Add(row);
            
            if (DialogResult.OK == fmP.ShowDialog())
            {
                ReportParameter[] parameters = new ReportParameter[report.GetParameters().Count];
                int j = 0;
                foreach (DataGridViewColumn c in fmP.dvParams.Columns)
                {
                    parameters[j++] = new ReportParameter((string)c.Name, (string)fmP.dvParams.Rows[0].Cells[c.Name].Value.ToString());
                }
                report.SetParameters(parameters);
                return true;
            }
            else
                return false;
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(fmParameters));
            this.paBottom = new System.Windows.Forms.Panel();
            this.btCancel = new System.Windows.Forms.Button();
            this.btOK = new System.Windows.Forms.Button();
            this.dvParams = new System.Windows.Forms.DataGridView();
            this.paBottom.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dvParams)).BeginInit();
            this.SuspendLayout();
            // 
            // paBottom
            // 
            this.paBottom.Controls.Add(this.btCancel);
            this.paBottom.Controls.Add(this.btOK);
            resources.ApplyResources(this.paBottom, "paBottom");
            this.paBottom.Name = "paBottom";
            // 
            // btCancel
            // 
            resources.ApplyResources(this.btCancel, "btCancel");
            this.btCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btCancel.Name = "btCancel";
            // 
            // btOK
            // 
            resources.ApplyResources(this.btOK, "btOK");
            this.btOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btOK.Name = "btOK";
            // 
            // dvParams
            // 
            this.dvParams.AllowUserToAddRows = false;
            this.dvParams.AllowUserToDeleteRows = false;
            this.dvParams.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dvParams.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.dvParams.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            resources.ApplyResources(this.dvParams, "dvParams");
            this.dvParams.Name = "dvParams";
            this.dvParams.RowHeadersVisible = false;
            // 
            // fmParameters
            // 
            this.AcceptButton = this.btOK;
            resources.ApplyResources(this, "$this");
            this.CancelButton = this.btCancel;
            this.Controls.Add(this.dvParams);
            this.Controls.Add(this.paBottom);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "fmParameters";
            this.Load += new System.EventHandler(this.fmParameters_Load);
            this.paBottom.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dvParams)).EndInit();
            this.ResumeLayout(false);

		}
		#endregion

        private void fmParameters_Load(object sender, EventArgs e)
        {
//            this.dgParams.CurrentCell = new DataGridCell(0, 3);
//            bsParams.DataSource = dsParams.Tables[0];
        }

    }

    #region calendarcolumn
    public class DataGridViewCalendarColumn : DataGridViewColumn
    {
        public DataGridViewCalendarColumn()
            : base(new CalendarCell())
        {
        }

        public override DataGridViewCell CellTemplate
        {
            get
            {
                return base.CellTemplate;
            }
            set
            {
                // Ensure that the cell used for the template is a CalendarCell.
                if (value != null &&
                    !value.GetType().IsAssignableFrom(typeof(CalendarCell)))
                {
                    throw new InvalidCastException("Must be a CalendarCell");
                }
                base.CellTemplate = value;
            }
        }
    }

    public class CalendarCell : DataGridViewTextBoxCell
    {

        public CalendarCell()
            : base()
        {
            // Use the short date format.
            this.Style.Format = "d";
        }

        public override void InitializeEditingControl(int rowIndex, object
            initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
        {
            // Set the value of the editing control to the current cell value.
            base.InitializeEditingControl(rowIndex, initialFormattedValue,
                dataGridViewCellStyle);
            CalendarEditingControl ctl =
                DataGridView.EditingControl as CalendarEditingControl;
            ctl.Value = (DateTime)this.Value;
        }

        public override Type EditType
        {
            get
            {
                // Return the type of the editing contol that CalendarCell uses.
                return typeof(CalendarEditingControl);
            }
        }

        public override Type ValueType
        {
            get
            {
                // Return the type of the value that CalendarCell contains.
                return typeof(DateTime);
            }
        }

        public override object DefaultNewRowValue
        {
            get
            {
                // Use the current date and time as the default value.
                return DateTime.Now;
            }
        }
    }

    class CalendarEditingControl : DateTimePicker, IDataGridViewEditingControl
    {
        DataGridView dataGridView;
        private bool valueChanged = false;
        int rowIndex;

        public CalendarEditingControl()
        {
            this.Format = DateTimePickerFormat.Short;
        }


        // Implements the IDataGridViewEditingControl.EditingControlFormattedValue 
        // property.
        public object EditingControlFormattedValue
        {
            get
            {
                return this.Value.ToShortDateString();
            }
            set
            {
                String newValue = value as String;
                if (newValue != null)
                {
                    this.Value = DateTime.Parse(newValue);
                }
            }
        }

        // Implements the 
        // IDataGridViewEditingControl.GetEditingControlFormattedValue method.
        public object GetEditingControlFormattedValue(
            DataGridViewDataErrorContexts context)
        {
            return EditingControlFormattedValue;
        }

        // Implements the 
        // IDataGridViewEditingControl.ApplyCellStyleToEditingControl method.
        public void ApplyCellStyleToEditingControl(
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            this.Font = dataGridViewCellStyle.Font;
            this.CalendarForeColor = dataGridViewCellStyle.ForeColor;
            this.CalendarMonthBackground = dataGridViewCellStyle.BackColor;
        }

        // Implements the IDataGridViewEditingControl.EditingControlRowIndex 
        // property.
        public int EditingControlRowIndex
        {
            get
            {
                return rowIndex;
            }
            set
            {
                rowIndex = value;
            }
        }

        // Implements the IDataGridViewEditingControl.EditingControlWantsInputKey 
        // method.
        public bool EditingControlWantsInputKey(
            Keys key, bool dataGridViewWantsInputKey)
        {
            // Let the DateTimePicker handle the keys listed.
            switch (key & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Up:
                case Keys.Down:
                case Keys.Right:
                case Keys.Home:
                case Keys.End:
                case Keys.PageDown:
                case Keys.PageUp:
                    return true;
                default:
                    return false;
            }
        }

        // Implements the IDataGridViewEditingControl.PrepareEditingControlForEdit 
        // method.
        public void PrepareEditingControlForEdit(bool selectAll)
        {
            // No preparation needs to be done.
        }

        // Implements the IDataGridViewEditingControl
        // .RepositionEditingControlOnValueChange property.
        public bool RepositionEditingControlOnValueChange
        {
            get
            {
                return false;
            }
        }

        // Implements the IDataGridViewEditingControl
        // .EditingControlDataGridView property.
        public DataGridView EditingControlDataGridView
        {
            get
            {
                return dataGridView;
            }
            set
            {
                dataGridView = value;
            }
        }

        // Implements the IDataGridViewEditingControl
        // .EditingControlValueChanged property.
        public bool EditingControlValueChanged
        {
            get
            {
                return valueChanged;
            }
            set
            {
                valueChanged = value;
            }
        }

        // Implements the IDataGridViewEditingControl
        // .EditingPanelCursor property.
        public Cursor EditingPanelCursor
        {
            get
            {
                return base.Cursor;
            }
        }

        protected override void OnValueChanged(EventArgs eventargs)
        {
            // Notify the DataGridView that the contents of the cell
            // have changed.
            valueChanged = true;
            this.EditingControlDataGridView.NotifyCurrentCellDirty(true);
            base.OnValueChanged(eventargs);
        }
    }
    #endregion calendarcolumn

    #region maskededitcolumn
    public class MaskedTextBoxColumn : DataGridViewColumn
    {
        private string mask;
        private char promptChar;
        private bool includePrompt;
        private bool includeLiterals;
        private HorizontalAlignment textalign;
        private Type validatingType;

        //  Initializes a new instance of this class, making sure to pass
        //  to its base constructor an instance of a MaskedTextBoxCell 
        //  class to use as the basic template.
        public MaskedTextBoxColumn()
            : base(new MaskedTextBoxCell())
        {
        }

        //  Routine to convert from boolean to DataGridViewTriState.
        private static DataGridViewTriState TriBool(bool value)
        {
            return value ? DataGridViewTriState.True
                         : DataGridViewTriState.False;
        }


        //  The template cell that will be used for this column by default,
        //  unless a specific cell is set for a particular row.
        //
        //  A MaskedTextBoxCell cell which will serve as the template cell
        //  for this column.
        public override DataGridViewCell CellTemplate
        {
            get
            {
                return base.CellTemplate;
            }

            set
            {
                //  Only cell types that derive from MaskedTextBoxCell are supported as the cell template.
                if (value != null && !value.GetType().IsAssignableFrom(typeof(MaskedTextBoxCell)))
                {
                    string s = "Cell type is not based upon the MaskedTextBoxCell.";//CustomColumnMain.GetResourceManager().GetString("excNotMaskedTextBox");
                    throw new InvalidCastException(s);
                }

                base.CellTemplate = value;
            }
        }

        //  Indicates the Mask property that is used on the MaskedTextBox
        //  for entering new data into cells of this type.
        // 
        //  See the MaskedTextBox control documentation for more details.
        public virtual string Mask
        {
            get
            {
                return this.mask;
            }
            set
            {
                MaskedTextBoxCell mtbc;
                DataGridViewCell dgvc;
                int rowCount;

                if (this.mask != value)
                {
                    this.mask = value;

                    //
                    // first, update the value on the template cell.
                    //
                    mtbc = (MaskedTextBoxCell)this.CellTemplate;
                    mtbc.Mask = value;

                    //
                    // now set it on all cells in other rows as well.
                    //
                    if (this.DataGridView != null && this.DataGridView.Rows != null)
                    {
                        rowCount = this.DataGridView.Rows.Count;
                        for (int x = 0; x < rowCount; x++)
                        {
                            dgvc = this.DataGridView.Rows.SharedRow(x).Cells[x];
                            if (dgvc is MaskedTextBoxCell)
                            {
                                mtbc = (MaskedTextBoxCell)dgvc;
                                mtbc.Mask = value;
                            }
                        }
                    }
                }
            }
        }

        public virtual HorizontalAlignment TextAlign
        {
            get
            {
                return this.textalign;
            }
            set
            {
                MaskedTextBoxCell mtbc;
                DataGridViewCell dgvc;
                int rowCount;

                if (this.textalign != value)
                {
                    this.textalign = value;

                    //
                    // first, update the value on the template cell.
                    //
                    mtbc = (MaskedTextBoxCell)this.CellTemplate;
                    mtbc.TextAlign = value;

                    //
                    // now set it on all cells in other rows as well.
                    //
                    if (this.DataGridView != null && this.DataGridView.Rows != null)
                    {
                        rowCount = this.DataGridView.Rows.Count;
                        for (int x = 0; x < rowCount; x++)
                        {
                            dgvc = this.DataGridView.Rows.SharedRow(x).Cells[x];
                            if (dgvc is MaskedTextBoxCell)
                            {
                                mtbc = (MaskedTextBoxCell)dgvc;
                                mtbc.TextAlign = value;
                            }
                        }
                    }
                }
            }
        }

        //  By default, the MaskedTextBox uses the underscore (_) character
        //  to prompt for required characters.  This propertly lets you 
        //  choose a different one.
        // 
        //  See the MaskedTextBox control documentation for more details.
        public virtual char PromptChar
        {
            get
            {
                return this.promptChar;
            }
            set
            {
                MaskedTextBoxCell mtbc;
                DataGridViewCell dgvc;
                int rowCount;

                if (this.promptChar != value)
                {
                    this.promptChar = value;

                    //
                    // first, update the value on the template cell.
                    //
                    mtbc = (MaskedTextBoxCell)this.CellTemplate;
                    mtbc.PromptChar = value;

                    //
                    // now set it on all cells in other rows as well.
                    //
                    if (this.DataGridView != null && this.DataGridView.Rows != null)
                    {
                        rowCount = this.DataGridView.Rows.Count;
                        for (int x = 0; x < rowCount; x++)
                        {
                            dgvc = this.DataGridView.Rows.SharedRow(x).Cells[x];
                            if (dgvc is MaskedTextBoxCell)
                            {
                                mtbc = (MaskedTextBoxCell)dgvc;
                                mtbc.PromptChar = value;
                            }
                        }
                    }
                }
            }
        }

        //   Indicates whether any unfilled characters in the mask should be
        //   be included as prompt characters when somebody asks for the text
        //   of the MaskedTextBox for a particular cell programmatically.
        // 
        //   See the MaskedTextBox control documentation for more details.
        public virtual bool IncludePrompt
        {
            get
            {
                return this.includePrompt;
            }
            set
            {
                MaskedTextBoxCell mtbc;
                DataGridViewCell dgvc;
                int rowCount;

                if (this.includePrompt != value)
                {
                    this.includePrompt = value;

                    //
                    // first, update the value on the template cell.
                    //
                    mtbc = (MaskedTextBoxCell)this.CellTemplate;
                    mtbc.IncludePrompt = TriBool(value);

                    //
                    // now set it on all cells in other rows as well.
                    //
                    if (this.DataGridView != null && this.DataGridView.Rows != null)
                    {
                        rowCount = this.DataGridView.Rows.Count;
                        for (int x = 0; x < rowCount; x++)
                        {
                            dgvc = this.DataGridView.Rows.SharedRow(x).Cells[x];
                            if (dgvc is MaskedTextBoxCell)
                            {
                                mtbc = (MaskedTextBoxCell)dgvc;
                                mtbc.IncludePrompt = TriBool(value);
                            }
                        }
                    }
                }
            }
        }

        //  Controls whether or not literal (non-prompt) characters should
        //  be included in the output of the Text property for newly entered
        //  data in a cell of this type.
        // 
        //  See the MaskedTextBox control documentation for more details.
        public virtual bool IncludeLiterals
        {
            get
            {
                return this.includeLiterals;
            }
            set
            {
                MaskedTextBoxCell mtbc;
                DataGridViewCell dgvc;
                int rowCount;

                if (this.includeLiterals != value)
                {
                    this.includeLiterals = value;

                    //
                    // first, update the value on the template cell.
                    //
                    mtbc = (MaskedTextBoxCell)this.CellTemplate;
                    mtbc.IncludeLiterals = TriBool(value);

                    //
                    // now set it on all cells in other rows as well.
                    //
                    if (this.DataGridView != null && this.DataGridView.Rows != null)
                    {

                        rowCount = this.DataGridView.Rows.Count;
                        for (int x = 0; x < rowCount; x++)
                        {
                            dgvc = this.DataGridView.Rows.SharedRow(x).Cells[x];
                            if (dgvc is MaskedTextBoxCell)
                            {
                                mtbc = (MaskedTextBoxCell)dgvc;
                                mtbc.IncludeLiterals = TriBool(value);
                            }
                        }
                    }
                }
            }
        }

        //  Indicates the type against any data entered in the MaskedTextBox
        //  should be validated.  The MaskedTextBox control will attempt to
        //  instantiate this type and assign the value from the contents of
        //  the text box.  An error will occur if it fails to assign to this
        //  type.
        //
        //  See the MaskedTextBox control documentation for more details.
        public virtual Type ValidatingType
        {
            get
            {
                return this.validatingType;
            }
            set
            {
                MaskedTextBoxCell mtbc;
                DataGridViewCell dgvc;
                int rowCount;

                if (this.validatingType != value)
                {
                    this.validatingType = value;

                    //
                    // first, update the value on the template cell.
                    //
                    mtbc = (MaskedTextBoxCell)this.CellTemplate;
                    mtbc.ValidatingType = value;

                    //
                    // now set it on all cells in other rows as well.
                    //
                    if (this.DataGridView != null && this.DataGridView.Rows != null)
                    {
                        rowCount = this.DataGridView.Rows.Count;
                        for (int x = 0; x < rowCount; x++)
                        {
                            dgvc = this.DataGridView.Rows.SharedRow(x).Cells[x];
                            if (dgvc is MaskedTextBoxCell)
                            {
                                mtbc = (MaskedTextBoxCell)dgvc;
                                mtbc.ValidatingType = value;
                            }
                        }
                    }
                }
            }
        }
    }
    #endregion maskededitcolumn

    #region maskededitcell
    class MaskedTextBoxCell : DataGridViewTextBoxCell
    {
        private string mask;
        private char promptChar;
        private DataGridViewTriState includePrompt;
        private DataGridViewTriState includeLiterals;
        private HorizontalAlignment textalign;
        private Type validatingType;

        //=------------------------------------------------------------------=
        // MaskedTextBoxCell
        //=------------------------------------------------------------------=
        /// <summary>
        ///   Initializes a new instance of this class.  Fortunately, there's
        ///   not much to do here except make sure that our base class is 
        ///   also initialized properly.
        /// </summary>
        /// 
        public MaskedTextBoxCell()
            : base()
        {
            this.mask = "";
            this.promptChar = '_';
            this.includePrompt = DataGridViewTriState.NotSet;
            this.includeLiterals = DataGridViewTriState.NotSet;
            this.validatingType = typeof(string);
        }

        ///   Whenever the user is to begin editing a cell of this type, the editing
        ///   control must be created, which in this column type's
        ///   case is a subclass of the MaskedTextBox control.
        /// 
        ///   This routine sets up all the properties and values
        ///   on this control before the editing begins.
        public override void InitializeEditingControl(int rowIndex,
                                                      object initialFormattedValue,
                                                      DataGridViewCellStyle dataGridViewCellStyle)
        {
            MaskedTextBoxEditingControl mtbec;
            MaskedTextBoxColumn mtbcol;
            DataGridViewColumn dgvc;

            base.InitializeEditingControl(rowIndex, initialFormattedValue,
                                          dataGridViewCellStyle);

            mtbec = DataGridView.EditingControl as MaskedTextBoxEditingControl;

            //
            // set up props that are specific to the MaskedTextBox
            //

            dgvc = this.OwningColumn;   // this.DataGridView.Columns[this.ColumnIndex];
            if (dgvc is MaskedTextBoxColumn)
            {
                mtbcol = dgvc as MaskedTextBoxColumn;

                //
                // get the mask from this instance or the parent column.
                //
                if (string.IsNullOrEmpty(this.mask))
                {
                    mtbec.Mask = mtbcol.Mask;
                }
                else
                {
                    mtbec.Mask = this.mask;
                }

                //
                // prompt char.
                //
                mtbec.PromptChar = this.PromptChar;

                //
                // textalign.
                //
                mtbec.TextAlign = this.TextAlign;

                //
                // IncludePrompt
                //
                if (this.includePrompt == DataGridViewTriState.NotSet)
                {
                    //mtbec.IncludePrompt = mtbcol.IncludePrompt;
                }
                else
                {
                    //mtbec.IncludePrompt = BoolFromTri(this.includePrompt);
                }

                //
                // IncludeLiterals
                //
                if (this.includeLiterals == DataGridViewTriState.NotSet)
                {
                    //mtbec.IncludeLiterals = mtbcol.IncludeLiterals;
                }
                else
                {
                    //mtbec.IncludeLiterals = BoolFromTri(this.includeLiterals);
                }

                //
                // Finally, the validating type ...
                //
                if (this.ValidatingType == null)
                {
                    mtbec.ValidatingType = mtbcol.ValidatingType;
                }
                else
                {
                    mtbec.ValidatingType = this.ValidatingType;
                }

                mtbec.Text = (string)this.Value;
            }
        }

        //  Returns the type of the control that will be used for editing
        //  cells of this type.  This control must be a valid Windows Forms
        //  control and must implement IDataGridViewEditingControl.
        public override Type EditType
        {
            get
            {
                return typeof(MaskedTextBoxEditingControl);
            }
        }

        //   A string value containing the Mask against input for cells of
        //   this type will be verified.
        public virtual string Mask
        {
            get
            {
                return this.mask;
            }
            set
            {
                this.mask = value;
            }
        }

        public virtual HorizontalAlignment TextAlign
        {
            get
            {
                return this.textalign;
            }
            set
            {
                this.textalign = value;
            }
        }


        //  The character to use for prompting for new input.
        // 
        public virtual char PromptChar
        {
            get
            {
                return this.promptChar;
            }
            set
            {
                this.promptChar = value;
            }
        }


        //  A boolean indicating whether to include prompt characters in
        //  the Text property's value.
        public virtual DataGridViewTriState IncludePrompt
        {
            get
            {
                return this.includePrompt;
            }
            set
            {
                this.includePrompt = value;
            }
        }

        //  A boolean value indicating whether to include literal characters
        //  in the Text property's output value.
        public virtual DataGridViewTriState IncludeLiterals
        {
            get
            {
                return this.includeLiterals;
            }
            set
            {
                this.includeLiterals = value;
            }
        }

        //  A Type object for the validating type.
        public virtual Type ValidatingType
        {
            get
            {
                return this.validatingType;
            }
            set
            {
                this.validatingType = value;
            }
        }

        //   Quick routine to convert from DataGridViewTriState to boolean.
        //   True goes to true while False and NotSet go to false.
        protected static bool BoolFromTri(DataGridViewTriState tri)
        {
            return (tri == DataGridViewTriState.True) ? true : false;
        }
    }
    #endregion maskededitcell

    #region maskededitcontrol
    public class MaskedTextBoxEditingControl : MaskedTextBox, IDataGridViewEditingControl
    {
        protected int rowIndex;
        protected DataGridView dataGridView;
        protected bool valueChanged = false;

        public MaskedTextBoxEditingControl()
        {

        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            // Let the DataGridView know about the value change
            NotifyDataGridViewOfValueChange();
        }

        //  Notify DataGridView that the value has changed.
        protected virtual void NotifyDataGridViewOfValueChange()
        {
            this.valueChanged = true;
            if (this.dataGridView != null)
            {
                this.dataGridView.NotifyCurrentCellDirty(true);
            }
        }

        #region IDataGridViewEditingControl Members

        //  Indicates the cursor that should be shown when the user hovers their
        //  mouse over this cell when the editing control is shown.
        public Cursor EditingPanelCursor
        {
            get
            {
                return Cursors.IBeam;
            }
        }


        //  Returns or sets the parent DataGridView.
        public DataGridView EditingControlDataGridView
        {
            get
            {
                return this.dataGridView;
            }

            set
            {
                this.dataGridView = value;
            }
        }


        //  Sets/Gets the formatted value contents of this cell.
        public object EditingControlFormattedValue
        {
            set
            {
                this.Text = value.ToString();
                NotifyDataGridViewOfValueChange();
            }
            get
            {
                return this.Text;
            }

        }

        //   Get the value of the editing control for formatting.
        public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context)
        {
            return this.Text;
        }

        //  Process input key and determine if the key should be used for the editing control
        //  or allowed to be processed by the grid. Handle cursor movement keys for the MaskedTextBox
        //  control; otherwise if the DataGridView doesn't want the input key then let the editing control handle it.
        public bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Right:
                    //
                    // If the end of the selection is at the end of the string
                    // let the DataGridView treat the key message
                    //
                    if (!(this.SelectionLength == 0
                          && this.SelectionStart == this.ToString().Length))
                    {
                        return true;
                    }
                    break;

                case Keys.Left:
                    //
                    // If the end of the selection is at the begining of the
                    // string or if the entire text is selected send this character 
                    // to the dataGridView; else process the key event.
                    //
                    if (!(this.SelectionLength == 0
                          && this.SelectionStart == 0))
                    {
                        return true;
                    }
                    break;

                case Keys.Home:
                case Keys.End:
                    if (this.SelectionLength != this.ToString().Length)
                    {
                        return true;
                    }
                    break;

                case Keys.Prior:
                case Keys.Next:
                    if (this.valueChanged)
                    {
                        return true;
                    }
                    break;

                case Keys.Delete:
                    if (this.SelectionLength > 0 || this.SelectionStart < this.ToString().Length)
                    {
                        return true;
                    }
                    break;
            }

            //
            // defer to the DataGridView and see if it wants it.
            //
            return !dataGridViewWantsInputKey;
        }


        //  Prepare the editing control for edit.
        public void PrepareEditingControlForEdit(bool selectAll)
        {
            if (selectAll)
            {
                SelectAll();
            }
            else
            {
                //
                // Do not select all the text, but position the caret at the 
                // end of the text.
                //
                this.SelectionStart = this.ToString().Length;
            }
        }

        //  Indicates whether or not the parent DataGridView control should
        //  reposition the editing control every time value change is indicated.
        //  There is no need to do this for the MaskedTextBox.
        public bool RepositionEditingControlOnValueChange
        {
            get
            {
                return false;
            }
        }


        //  Indicates the row index of this cell.  This is often -1 for the
        //  template cell, but for other cells, might actually have a value
        //  greater than or equal to zero.
        public int EditingControlRowIndex
        {
            get
            {
                return this.rowIndex;
            }

            set
            {
                this.rowIndex = value;
            }
        }



        //  Make the MaskedTextBox control match the style and colors of
        //  the host DataGridView control and other editing controls 
        //  before showing the editing control.
        public void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
        {
            this.Font = dataGridViewCellStyle.Font;
            this.ForeColor = dataGridViewCellStyle.ForeColor;
            this.BackColor = dataGridViewCellStyle.BackColor;
            this.TextAlign = translateAlignment(dataGridViewCellStyle.Alignment);
        }


        //  Gets or sets our flag indicating whether the value has changed.
        public bool EditingControlValueChanged
        {
            get
            {
                return valueChanged;
            }

            set
            {
                this.valueChanged = value;
            }
        }

        #endregion // IDataGridViewEditingControl.

        ///   Routine to translate between DataGridView
        ///   content alignments and text box horizontal alignments.
        private static HorizontalAlignment translateAlignment(DataGridViewContentAlignment align)
        {
            switch (align)
            {
                case DataGridViewContentAlignment.TopLeft:
                case DataGridViewContentAlignment.MiddleLeft:
                case DataGridViewContentAlignment.BottomLeft:
                    return HorizontalAlignment.Left;

                case DataGridViewContentAlignment.TopCenter:
                case DataGridViewContentAlignment.MiddleCenter:
                case DataGridViewContentAlignment.BottomCenter:
                    return HorizontalAlignment.Center;

                case DataGridViewContentAlignment.TopRight:
                case DataGridViewContentAlignment.MiddleRight:
                case DataGridViewContentAlignment.BottomRight:
                    return HorizontalAlignment.Right;
            }

            throw new ArgumentException("Error: Invalid Content Alignment!");
        }


    }
    #endregion maskededitcontrol

}
