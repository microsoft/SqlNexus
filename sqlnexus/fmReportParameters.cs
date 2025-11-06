using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;
using System.Xml;
using Microsoft.Data.SqlClient;
using System.Globalization;
using NexusInterfaces;

namespace sqlnexus
{
    public partial class fmReportParameters : Form
    {
        public fmReportParameters()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Determines whether a form has parameters.  
        /// </summary>
        /// <remarks>If the <para>nodefaultonly</para> parameter is false, the function returns true if the form has any 
        /// non-hidden parameters.  If <para>nodefaultonly</para> is false, the function returns true only if there are 
        /// non-nullable parameters that do have neither a default value nor a value supplied on the command line by the 
        /// user (/V command line parameter).</remarks>
        /// <param name="report">RS report</param>
        /// <param name="nodefaultonly">True for initial report load, false if only determining whether to enable the 
        /// Parameters toolbar button</param>
        /// <returns>true if matching parameters were found</returns>
        public static bool HasReportParameters(LocalReport report, bool nodefaultonly)
        {
            int userSuppliedParams = 0;
            XmlDocument doc = new XmlDocument();
            doc.Load(report.ReportPath);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            //nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
            String strNameSpace = ReportUtil.GetReportNameSpace(doc);
            if (strNameSpace != null)
            {
                nsmgr.AddNamespace("rds", strNameSpace);
            }
            else
            {
                nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
            }


            string xpathstr = string.Format("//rds:Report//rds:ReportParameters/rds:ReportParameter[(rds:Hidden='false' or not (rds:Hidden)) {0}]", nodefaultonly ? "and (not (rds:DefaultValue)) and (rds:Nullable='false' or not (rds:Nullable))" : "");

            XmlNodeList nodes = doc.DocumentElement.SelectNodes(xpathstr, nsmgr);
            if (nodefaultonly)
            {   // When called in "nodefaultonly mode", subtract out any required parameters that the user provided 
                // on the command line
                foreach (XmlNode node in nodes)
                {
                    if (Globals.UserSuppliedReportParameters.ContainsKey (node.Attributes["Name"].Value))
                        userSuppliedParams++;
                }
            }

            return ((null != nodes) && (nodes.Count > userSuppliedParams));
        }

        public static string GetReportParameter(LocalReport report, string paramname)
        {
            foreach (ReportParameterInfo param in report.GetParameters())
            {
                if (0 == string.Compare(paramname, param.Name, true, CultureInfo.InvariantCulture))
                {
                    return param.Values[0];
                }
            }
            return null;
        }

        public static bool GetReportParameters(LocalReport report, string reportparam, IWin32Window owner, ILogger logger)
        {

            fmReportParameters fmP = new fmReportParameters();

            XmlDocument doc = new XmlDocument();
            doc.Load(report.ReportPath);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);

            //nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");

            String strNameSpace = ReportUtil.GetReportNameSpace(doc);
            if (strNameSpace != null)
            {
                nsmgr.AddNamespace("rds", strNameSpace);
            }
            else
            {
                nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
            }

            string paramsxpath;
            if (0==reportparam.Length)
            {
                paramsxpath = "//rds:Report//rds:ReportParameters/rds:ReportParameter[rds:Hidden='false' or not (rds:Hidden)]";
            }
            else
            {
                paramsxpath = "//rds:Report//rds:ReportParameters/rds:ReportParameter[@Name='" + reportparam + "'and (rds:Hidden='false' or not (rds:Hidden))]";
            }

            XmlNodeList nodes = doc.DocumentElement.SelectNodes(paramsxpath, nsmgr);

            //If no params, bail
            if ((null == nodes) || (0 == nodes.Count))
            {
                logger.LogMessage(sqlnexus.Properties.Resources.Msg_NoParams, MessageOptions.Dialog);
                return false;
            }

            int i = 0;
            foreach (XmlNode node in nodes)
            {
                fmP.tlpClient.RowCount += 1;

                object defval = null;
                object minval = null;
                object maxval = null;

                Label la = new Label();
                la.AutoSize = true;

                fmP.tlpClient.Controls.Add(la,0,i);

                la.Anchor = AnchorStyles.Left;
                la.Location = new Point(0, 3);

                XmlNode valnode = node.SelectSingleNode("rds:ValidValues", nsmgr);

                Control ctl = null;
                TrackBar tctl = null;
                if (null != valnode) //value list
                {
                    ctl = new ComboBox();
                    ((ComboBox)ctl).Size = new Size(200, ((ComboBox)ctl).Size.Height);
//                    ((ComboBox)ctl).DropDownStyle = ComboBoxStyle.DropDownList;

                    XmlNode dsnode = valnode.SelectSingleNode("rds:DataSetReference/rds:DataSetName", nsmgr);
                    if (null != dsnode)  //value list from dataset
                    {
                        XmlNode vfnode = valnode.SelectSingleNode("rds:DataSetReference/rds:ValueField", nsmgr);
                        XmlNode labelFieldNode = valnode.SelectSingleNode("rds:DataSetReference/rds:LabelField", nsmgr);
                        System.Diagnostics.Debug.Assert(null != vfnode);

                        DataTable dt = new DataTable();
                        SqlDataAdapter da = new SqlDataAdapter(fmNexus.GetQueryText(report.ReportPath, report.GetParameters(), dsnode.InnerText), Globals.credentialMgr.ConnectionString);
                        da.Fill(dt);
                        
                        String DisplayMember = (labelFieldNode==null ? vfnode.InnerText.ToString() : labelFieldNode.InnerText.ToString());
                        String ValueMember = vfnode.InnerText.ToString();
                        dt.DefaultView.Sort = dt.Columns[ValueMember].ColumnName;

                        ((ComboBox)ctl).DataSource = dt;
                        ((ComboBox)ctl).DisplayMember = dt.Columns[DisplayMember].ColumnName;
                        ((ComboBox)ctl).ValueMember = dt.Columns[ValueMember].ColumnName;
                        


                        foreach (DataRow r in dt.Rows)
                        {
                            if (null == minval)
                                minval = r[vfnode.InnerText].ToString();
                        //    ((ComboBox)ctl).Items.Add(r[vfnode.InnerText].ToString());
                             maxval = r[vfnode.InnerText].ToString();
                        }
                    }
                    else
                    {
                        XmlNodeList valnodes = valnode.SelectNodes("rds:ParameterValues//rds:Value", nsmgr);
                        if ((null != valnodes) && (0 != valnodes.Count))
                        {
                            foreach (XmlNode vnode in valnodes)
                            {
                                if (null == minval)
                                    minval = vnode.InnerText;
                                ((ComboBox)ctl).Items.Add(vnode.InnerText);
                                maxval = vnode.InnerText;
                            }
                        }
                    }

                    //Create an associated trackbar if there are 10 or more items
                    //in the list
                    DataTable myTable = (((ComboBox)ctl).DataSource as DataTable);
                    Int32 itemCnt = 0;
                    if (myTable != null)
                    {
                        itemCnt = myTable.Rows.Count;

                    }
                    if (((ComboBox)ctl).Items.Count >= 10  || itemCnt > 10)
                    {
                        tctl = new TrackBar();
                        if (itemCnt > 0)
                            tctl.Maximum = itemCnt -1;
                        else
                            tctl.Maximum = ((ComboBox)ctl).Items.Count - 1;

                        tctl.AutoSize = false;
                        tctl.Width = 300;
                        tctl.TickStyle = TickStyle.None;
                        tctl.Height = 30;
                        tctl.Tag = ctl;
                        fmP.tlpClient.Controls.Add(tctl, 2, i);
                        tctl.Scroll += new System.EventHandler(fmP.trackBarComboBox_Scroll);
                    }
                }

                //Get the current value if there is one
                defval = fmReportParameters.GetReportParameter(report, node.Attributes["Name"].Value);

                GetParameterVals(report.ReportPath, node.Attributes["Name"].Value, ref defval, ref minval, ref maxval);

                object def;
                if (0 == string.Compare(node["DataType"].InnerText, "datetime", true, CultureInfo.InvariantCulture))
                {
                    if (null == defval)
                        def = DateTime.Now;
                    else
                        def = Convert.ToDateTime(defval);

                    if (null == ctl)
                    {
                        ctl = new DateTimePicker();
                        ((DateTimePicker)ctl).Size = new Size(200, ((DateTimePicker)ctl).Size.Height);

                        ((DateTimePicker)ctl).Value = Convert.ToDateTime(def);
                        ((DateTimePicker)ctl).ValueChanged += new System.EventHandler(fmP.DateTimePicker_ValueChanged);

                        //((DateTimePicker)ctl).Format = DateTimePickerFormat.Short;
                        ((DateTimePicker)ctl).Format = DateTimePickerFormat.Custom;
                        
                        ((DateTimePicker)ctl).CustomFormat = "yyyy-MM-dd HH:mm:ss";
                        ((DateTimePicker)ctl).ShowUpDown = true;

                        ////If no time supplied, use calendar format, else use time format
                        //if (((DateTimePicker)ctl).Value.TimeOfDay == new TimeSpan(0, 0, 0))
                        //    ((DateTimePicker)ctl).Format = DateTimePickerFormat.Short;
                        
                        if (null != minval)
                        {
                            ((DateTimePicker)ctl).MinDate = Convert.ToDateTime(minval);
                            ((DateTimePicker)ctl).MaxDate = Convert.ToDateTime(maxval);
                            
                            tctl = new TrackBar();

                            tctl.Minimum = 0;
                            tctl.Maximum = (int)(Convert.ToDateTime(maxval).Subtract(Convert.ToDateTime(minval)).TotalSeconds);
                            tctl.TickStyle = TickStyle.None;
//                            tctl.TickFrequency = tctl.Maximum / 25;

                            tctl.AutoSize = false;
                            tctl.Width = 300;
                            tctl.Height = 30;
                            tctl.Tag = ctl;
                            ctl.Tag = tctl;
                            fmP.tlpClient.Controls.Add(tctl, 2, i);
                            tctl.Scroll += new System.EventHandler(fmP.trackBar_DateTimePickerScroll);
                            fmP.DateTimePicker_ValueChanged(ctl, null);
                            
                            tctl = null;  //Keep from being seen by combobox-specific code below
                        }

                    }
                    else  //combobox created above
                    {
                        ((ComboBox)ctl).Text = DateTimeUtil.USString(Convert.ToDateTime(def), "yyyy-MM-dd HH:mm:ss.fff"); //Convert.ToDateTime(def).ToString("yyyy-MM-dd HH:mm:ss.fff");
                    }

                }
                else if (0 == string.Compare(node["DataType"].InnerText, "boolean", true, CultureInfo.InvariantCulture))
                {
                    def = false;
                    if (null == ctl)
                        ctl = new CheckBox();

                    if (null == defval)
                        ((CheckBox)ctl).Checked = Convert.ToBoolean(def);
                    else
                        ((CheckBox)ctl).Checked = Convert.ToBoolean(defval);
                }
                else if (0 == string.Compare(node["DataType"].InnerText, "integer", true, CultureInfo.InvariantCulture))
                {
                    def = "0";  //Masked text box requires strings
                    if (null == ctl)
                    {
                        ctl = new MaskedTextBox();
                        ((MaskedTextBox)ctl).KeyPress += new System.Windows.Forms.KeyPressEventHandler(fmP.maskedTextBox1_IntegerKeyPress);
                        ((MaskedTextBox)ctl).Size = new Size(200, ((MaskedTextBox)ctl).Size.Height);
                    }

                    if (ctl is ComboBox)
                    {
                        if (null == defval)
                            ((ComboBox)ctl).SelectedText = Convert.ToString(def);
                        else
                            ((ComboBox)ctl).SelectedText = Convert.ToString(defval);
                    }
                    else if (ctl is TextBox)
                    {

                        ((TextBox)ctl).TextAlign = HorizontalAlignment.Right;

                        if (null == defval)
                            ((TextBox)ctl).Text = Convert.ToString(def);
                        else
                            ((TextBox)ctl).Text = Convert.ToString(defval);
                    }
                    else if (ctl is MaskedTextBox)
                    {
                        ((MaskedTextBox)ctl).TextAlign = HorizontalAlignment.Right;

                        if (null == defval)
                            ((MaskedTextBox)ctl).Text = Convert.ToString(def);
                        else
                            ((MaskedTextBox)ctl).Text = Convert.ToString(defval);

                    }
                    else
                        throw new ArgumentException("unknow control.  fmReportParameter does not know how to handle this");
                }
                else if (0 == string.Compare(node["DataType"].InnerText, "float", true, CultureInfo.InvariantCulture))
                {
                    def = "0.00";  //Masked text box requires strings
                    if (null == ctl)
                    {
                        ctl = new MaskedTextBox();
                        ((MaskedTextBox)ctl).KeyPress += new System.Windows.Forms.KeyPressEventHandler(fmP.maskedTextBox1_DecimalKeyPress);
                        ((MaskedTextBox)ctl).Size = new Size(200, ((MaskedTextBox)ctl).Size.Height);
                    }
                    ((MaskedTextBox)ctl).TextAlign = HorizontalAlignment.Right;

//                    ctl.DefaultCellStyle.Format = "f";

                    if (null == defval)
                        ((MaskedTextBox)ctl).Text = Convert.ToString(def);
                    else
                        ((MaskedTextBox)ctl).Text = Convert.ToString(defval);
                }
                else  //string or xml
                {
                    def = "";
                    string defvalstr = (string)defval;
                    if (null == ctl)
                    {
                        if ((null != defvalstr) && defvalstr.Length >0 &&
                            ('<' == defvalstr[0]) &&
                            ('>' == defvalstr[defvalstr.Length - 1])) //xml
                        {
                            ctl = new DataGridView();

                            (ctl as DataGridView).RowHeadersVisible = false;
                            (ctl as DataGridView).ScrollBars = ScrollBars.Both;
                            (ctl as DataGridView).AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                            System.IO.StringReader reader = new System.IO.StringReader(defvalstr);
                            DataSet ds;
                            if (0 == defvalstr.IndexOf("<SeriesList>"))  //series list xml
                            {
                                ds = new DataSet("SeriesList");

                                DataTable dt = ds.Tables.Add("Series");

                                DataColumn dc = dt.Columns.Add("Selected", typeof(bool));
                                dc.ColumnMapping = MappingType.Attribute;

                                dc = dt.Columns.Add("SeriesName", typeof(string));
                                dc.ColumnMapping = MappingType.Attribute;
                                dc.ReadOnly = true;
                            }
                            else  //regular xml
                            {
                                ds = new DataSet();
                            }
                            ds.ReadXml(reader);
                            DataView view1 = new DataView(ds.Tables[0]);
                            view1.AllowDelete = false;
                            view1.AllowNew = false;

                            BindingSource bs = new BindingSource();
                            bs.DataSource = view1;
                            (ctl as DataGridView).DataSource = bs;
                            (ctl as DataGridView).Width = 400;
                            (ctl as DataGridView).Height = 200;
                            ctl.Tag = ds;
                        }
                        else //string
                        {

                            ctl = new TextBox();
                            ((TextBox)ctl).Size = new Size(200, ((TextBox)ctl).Size.Height);


                            if (null == defval)
                                ((TextBox)ctl).Text = Convert.ToString(def);
                            else
                                ((TextBox)ctl).Text = defvalstr;
                        }
                    }
                    else //combobox from above
                    {
                        if (null == defval)
                            ((ComboBox)ctl).Text = Convert.ToString(def);
                        else
                            ((ComboBox)ctl).Text = Convert.ToString(defval);
                    }
                }


                if (null != tctl)  //Can only be paired with a combobox
                {
                    int j = ((ComboBox)ctl).Items.IndexOf(((ComboBox)ctl).Text);
                    if (-1 == j)  //Not found, probably due to time truncation; search for partial match
                    {
                        j = 0;
                        foreach (string s in ((ComboBox)ctl).Items)
                        {
                            if (0 == string.Compare(((ComboBox)ctl).Text, s.Substring(0, ((ComboBox)ctl).Text.Length), true, CultureInfo.InvariantCulture))
                            {
                                tctl.Value = j;
                                break;
                            }
                        }
                    }
                    else
                        tctl.Value = j;
                }


                ctl.Name = node.Attributes["Name"].Value;
                try
                {
                    la.Text = node["Prompt"].InnerText;
                }
                catch (Exception ex)  //May not have a prompt
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    la.Text = node.Attributes["Name"].Value;
                }

                fmP.tlpClient.Controls.Add(ctl, 1, i);

                if (ctl is DataGridView)
                    (ctl as DataGridView).AutoSize = true;


                i++;
            }

            if (DialogResult.OK == fmP.ShowDialog(owner))
            {
                ReportParameter[] parameters = new ReportParameter[report.GetParameters().Count];
                int j = 0;
                foreach (Control c in fmP.tlpClient.Controls)
                {
                    string paramval="";
                    if ((c is Label) || (c is TrackBar))
                        continue;  //ignore labels and trackbars 
                    if (c is ComboBox)
                    {
                        ComboBox b = (ComboBox)c;
                        if (b.SelectedValue != null && b.SelectedValue.ToString().Trim().Length > 0)
                            paramval = b.SelectedValue.ToString();
                        else
                            paramval = ((ComboBox)c).Text;
                    }
                    else if (c is TextBoxBase)
                    {
                        paramval = ((TextBoxBase)c).Text;
                    }
                    else if (c is DateTimePicker)
                    {
                        DateTimePicker pck = (DateTimePicker)c;
                        paramval = DateTimeUtil.USString(pck.Value,"yyyy-MM-dd HH:mm:ss") ;// pck.Value.ToString("yyyy-MM-dd HH:mm:ss"); 
                    }
                    else if (c is DataGridView)
                    {
                        paramval = (c.Tag as DataSet).GetXml();
                    }
                    else if (c is CheckBox)
                    {
                        paramval = ((CheckBox)c).Checked.ToString();
                    }
                    parameters[j++] = new ReportParameter((string)c.Name, paramval);
                }
                ReportParameter[] parametersclean = new ReportParameter[j];
                for (int k = 0; k < j; k++)
                    parametersclean[k] = parameters[k];
                report.SetParameters(parametersclean);
                return true;
            }
            else
                return false;
        }

        private static void GetParameterVals(string filename, string paramname, ref object defval, ref object minval, ref object maxval)
        {

            XmlDocument doc = new XmlDocument();
            doc.Load(filename);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);

            //nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
            String strNameSpace = ReportUtil.GetReportNameSpace(doc);
            if (strNameSpace != null)
            {
                nsmgr.AddNamespace("rds", strNameSpace);
            }
            else
            {
                nsmgr.AddNamespace("rds", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
            }


            XmlNode node = doc.DocumentElement.SelectSingleNode("//rds:Report//rds:ReportParameters/rds:ReportParameter[@Name='"+paramname+"']", nsmgr);

            //If not found, bail
            if (null == node) 
            {
                return;
            }
            
            //Get the default value if there is one

            //Check for dataset first
            XmlNode dsetnode = node.SelectSingleNode("rds:DefaultValue/rds:DataSetReference/rds:DataSetName", nsmgr);
            if (null != dsetnode)  //value from dataset
            {
                XmlNode vfnode = node.SelectSingleNode("rds:DefaultValue/rds:DataSetReference/rds:ValueField", nsmgr);
                System.Diagnostics.Debug.Assert(null != vfnode);

                using (SqlConnection conn = new SqlConnection(Globals.credentialMgr.ConnectionString))
                {
                    
                    //jackli: previous code made wrong assumption that the Procedure name is always the same as data set name without any parameters.
                    //SqlCommand cmd = new SqlCommand(dsetnode.InnerText, conn);
                    string DataSetName = dsetnode.InnerText;

                    //jackli: new code will grab the data set name from the parameter. then it goes to data set to get the query
                    XmlNode node2 = doc.DocumentElement.SelectSingleNode("//rds:Report//rds:DataSets/rds:DataSet[@Name='" + DataSetName + "']/rds:Query/rds:CommandText", nsmgr);
                    if (node2 == null)
                    {
                        Util.Logger.LogMessage("This default parameter doesn't have dataset define.  Param is: " + paramname, MessageOptions.All);
                    }
                    String CommandText = node2.InnerText;
                    
                    SqlCommand cmd = new SqlCommand(CommandText, conn);
                    cmd.CommandTimeout = sqlnexus.Properties.Settings.Default.QueryTimeout;
                    conn.Open();
                    SqlDataReader dr = cmd.ExecuteReader();

                    if (dr.Read())
                    {
                     if (null == defval)
                        defval = dr.GetValue(0);
                    }

                    if (dr.Read())
                        minval = dr.GetValue(0);

                    if (dr.Read())
                        maxval = dr.GetValue(0);
                }
            }
            else
            {
                if (null == defval)
                {
                    XmlNode defvalue = node.SelectSingleNode("rds:DefaultValue//rds:Value", nsmgr);
                    if (null != defvalue)
                        defval = defvalue.InnerText;
                }
            }

            // If the user provided a default value on the command line (/V), use that value
            if (Globals.UserSuppliedReportParameters.ContainsKey(paramname))
                defval = Globals.UserSuppliedReportParameters[paramname];

            return;
        }

        private void maskedTextBox1_IntegerKeyPress(object sender, KeyPressEventArgs e)
        {
            if (!((char.IsDigit(e.KeyChar)) || ('-'==e.KeyChar)))
            {
                Console.Beep();
                e.Handled = true;
            }
        }

        private void maskedTextBox1_DecimalKeyPress(object sender, KeyPressEventArgs e)
        {
            if (!((char.IsDigit(e.KeyChar)) || ('-' == e.KeyChar) || ('.' == e.KeyChar)))
            {
                Console.Beep();
                e.Handled = true;
            }
        }

        private void trackBar_DateTimePickerScroll(object sender, EventArgs e)
        {
            TrackBar trk = (sender as TrackBar);
            DateTimePicker col = (trk.Tag as DateTimePicker);

            col.Value = col.MinDate.AddSeconds((double)trk.Value);
        }

        private void trackBarComboBox_Scroll(object sender, EventArgs e)
        {
            TrackBar trk = (sender as TrackBar);
            ComboBox col = (trk.Tag as ComboBox);
            col.SelectedIndex = trk.Value;
            
        }

        private void DateTimePicker_ValueChanged(Object sender, EventArgs e)
        {
            DateTimePicker col = (sender as DateTimePicker);
            if (null != col.Tag)
            {
                TrackBar trk = (col.Tag as TrackBar);
                trk.Value = (int)(col.Value.Subtract(col.MinDate).TotalSeconds);
            }
        }

        private void tlpClient_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}