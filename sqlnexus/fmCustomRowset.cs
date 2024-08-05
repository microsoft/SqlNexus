using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using NexusInterfaces;
using System.IO;
namespace sqlnexus
{
    [Flags] public  enum Operation 
    { 
        Add =1, 
        Edit =2, 
        Delete =4, 
        All=Add|Edit|Delete
    };

    public partial class fmCustomRowset : Form
    {
        private string CustomXmlFile = Globals.AppDataPath + @"\TextRowsetsCustom.xml";
        
        private ILogger logger;
        public fmCustomRowset(ILogger lgr)
        {
            InitializeComponent();
            logger = lgr;
        }

        private void rdEditRowset_CheckedChanged(object sender, EventArgs e)
        {
            if (rdEditRowset.Checked)
            {
                InitConrols(Operation.Edit);
                PopulateRowset(null);
                this.Refresh();
            }

        }

        private void DisableAll()
        {
            foreach (Control c in Controls)
            {
                c.Enabled = false;
            }
        }
        private void InitControlMap()
        {
            //these should be enabled for all
            llCustomRowsetHelp.Tag = Operation.All;
            grpAddorEditRowset.Tag = Operation.All;
            rdAddRowset.Tag = Operation.All;
            rdEditRowset.Tag = Operation.All;
            rdDelete.Tag = Operation.All;
            btnAdd.Tag = Operation.All;

            //these shoudl be only enabled for add or edit
            lblRowsetName.Tag = Operation.Add | Operation.Edit;
            txtRowsetName.Tag = Operation.Add | Operation.Edit;
            grpEnableDisable.Tag = Operation.Add | Operation.Edit;
            rdEnable.Tag = Operation.Add | Operation.Edit;
            rdDisable.Tag = Operation.Add | Operation.Edit;
            lblIdentifier.Tag = Operation.Add | Operation.Edit;
            txtIdentifier.Tag = Operation.Add | Operation.Edit;
            lblType.Tag = Operation.Add | Operation.Edit;
            cbType.Tag = Operation.Add | Operation.Edit;
            
            //these are only for delete or edit 
            lblSelectRowset.Tag = Operation.Delete | Operation.Edit;
            cbSelectRowset.Tag = Operation.Delete | Operation.Edit;
            

        }
        private void EnableControlByOp(Operation op)
        {
            foreach (Control c in Controls)
            {
                if ((op & (Operation)c.Tag) == op)
                    c.Enabled = true;
            }
        }

        private void InitConrols(Operation op)
        {
            DisableAll();
            InitControlMap();
            EnableControlByOp(op);

            if ((op & Operation.Add) == Operation.Add)
            {
                txtRowsetName.Text = "";
                txtIdentifier.Text = "-- myabctable ";
                btnAdd.Text = "OK";
                PopulateType(null);

            }
            else if ((op & Operation.Add) == Operation.Edit)
            {
                btnAdd.Text = "OK";
            }
            else if ((op & Operation.Add) == Operation.Delete)
            {
                btnAdd.Text = "OK";
            }


            
        }
        private void rdAddRowset_CheckedChanged(object sender, EventArgs e)
        {
            if (rdAddRowset.Checked)
            {
                InitConrols(Operation.Add);
                PopulateRowset(null);
                PopulateType(null);
                this.Refresh();
            }

        }


        private void fmCustomRowset_Load(object sender, EventArgs e)
        {
            if (!File.Exists(CustomXmlFile))
            {
                if (!Directory.Exists(Globals.AppDataPath))
                    Directory.CreateDirectory(Globals.AppDataPath);
                File.Copy(Globals.StartupPath + @"\TextRowsetsCustom_Template.xml", CustomXmlFile);

            }
             CustomRowset.Init(CustomXmlFile);
             rdAddRowset.Checked = true;
             InitConrols(Operation.Add);
             Application.DoEvents();


        }

        void PopulateRowset(String DefaultItem)
        {
            cbSelectRowset.Items.Clear();
            SortedDictionary<string, CustomRowset> cr =  CustomRowset.GetAllCustomRowsets();
            if (cr.Count == 0)
            {
                cbSelectRowset.Text = "";
                cbSelectRowset.Refresh();
                Application.DoEvents();
                return;
            }

            foreach (String key in cr.Keys)
            {
                cbSelectRowset.Items.Add(key);
            }

            if (null == DefaultItem)
                cbSelectRowset.SelectedIndex = 0;
            else
                cbSelectRowset.SelectedItem = DefaultItem;

            cbSelectRowset.Refresh();
            Application.DoEvents();

        }
        void PopulateType(String DefaultItem)
        {
            cbType.Items.Clear();
            cbType.Items.Add("RowsetImportEngine.TextRowset");
            if (null == DefaultItem)
                cbType.SelectedIndex = 0;
            else
                cbType.SelectedItem = DefaultItem;
            
        }
        private void btnAdd_Click(object sender, EventArgs e)
        {
            CustomRowset cr = new CustomRowset(txtRowsetName.Text, rdEnable.Checked, txtIdentifier.Text, cbType.Text);
            if (rdAddRowset.Checked)
            {
                if (CustomRowset.GetAllCustomRowsets().ContainsKey(cr.name))
                {
                    logger.LogMessage(String.Format("rowset name {0} already exists. Use edit to modify this rowset", cr.name), MessageOptions.Dialog);
                    return;
                }
                CustomRowset.Add  (cr);
            }
            else if (rdEditRowset.Checked)
            { 
                CustomRowset.Save (cbSelectRowset.Text, cr);
            }
            else if (rdDelete.Checked)
            {
                if (cbSelectRowset.Text.Trim().Length == 0)
                {
                    logger.LogMessage(String.Format("The rowset list is either empty or you haven't chosen anything to delete yet. try again", cr.name), MessageOptions.Dialog);
                    return;
                }
                CustomRowset.Delete(cbSelectRowset.Text);
            }

            if (CustomRowset.GetAllCustomRowsets().Count == 0)
            {
                rdAddRowset.Checked = true;
            }
            PopulateRowset(null);
            Application.DoEvents();            
            this.Refresh();

        }

        private void cbSelectRowset_SelectedIndexChanged(object sender, EventArgs e)
        {
            CustomRowset cr = CustomRowset.GetRowsetByName(cbSelectRowset.Text);
            txtIdentifier.Text = cr.identifier;
            txtRowsetName.Text = cr.name;
            if (cr.enabled)
                rdEnable.Checked = true;
            else
                rdDisable.Checked = true;
            PopulateType(cr.type);


            Application.DoEvents();

        }

        private void rdDelete_CheckedChanged(object sender, EventArgs e)
        {
            if (rdDelete.Checked)
            {
                InitConrols(Operation.Delete);
                PopulateRowset(null);
                this.Refresh();
            }
        }

        //todo: this needs to move to BOL
        private void llCustomRowsetHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string message = String.Format("Current version doesn't have UI to add your own column data types.  Using this form will treat all columns as varchar.  Please modify {0} directly", CustomXmlFile);
            MessageBox.Show(message, "Validation Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

    }

    //TODO: this needs to incoporate with TextRowsetImport
    //TODO: need to add column support
    class  CustomRowset
    {
        public static XmlDocument m_Doc;
        public static String xmlFileName;

        public CustomRowset(string strName, bool bEnable, string strIdentifier, string strType)
        {
            this.name = strName;
            this.enabled = bEnable;
            this.identifier = strIdentifier;
            this.type = strType;

        }
        public static void Init(String xmlFile)
        {
            xmlFileName = xmlFile;
            m_Doc = new XmlDocument();
            m_Doc.Load(xmlFile);

        }
        public static SortedDictionary<string, CustomRowset>  GetAllCustomRowsets()
        {
            SortedDictionary<string, CustomRowset> rowsets = new SortedDictionary<string, CustomRowset>();
                XPathNavigator nav = m_Doc.CreateNavigator();
                XPathNodeIterator iter = nav.Select("TextImport/KnownRowsets/Rowset");
                while (iter.MoveNext())
                {
                    String name = iter.Current.GetAttribute("name", "");
                    if (name == "tbl_MYSAMPLETABLE")
                        continue;
                    if (iter.Current.HasChildren)
                        throw new ArgumentException(String.Format(Properties.Resources.Error_CustomRowset_HasChildren, xmlFileName));
                    bool enabled = bool.Parse(iter.Current.GetAttribute("enabled", ""));
                    string identifier = iter.Current.GetAttribute("identifier", "");
                    string type = iter.Current.GetAttribute("type", "");
                    rowsets.Add(name, new CustomRowset(name, enabled, identifier, type));
                }

            return rowsets;
        }
        public static CustomRowset GetRowsetByName(string name)
        {
            return (CustomRowset) GetAllCustomRowsets()[name];
        }
        public static void Save(String OldName, CustomRowset cr)
        {
            ReplaceOrAppend(OldName, cr, Operation.Edit);
        }
        public static void Add(CustomRowset cr)
        {
            ReplaceOrAppend(null, cr, Operation.Add);

        }
        public static void Delete(String OldName)
        {
            ReplaceOrAppend(OldName, null, Operation.Delete);
        }
        static void ReplaceOrAppend(String OldName, CustomRowset cRowset, Operation op)
        {
            XmlElement el = m_Doc.CreateElement("Rowset");
            if (! (op == Operation.Delete))
            {
                el.SetAttribute("name", cRowset.name);
                el.SetAttribute("enabled", cRowset.enabled.ToString());
                el.SetAttribute("identifier", cRowset.identifier);
                el.SetAttribute("type", cRowset.type);
            }
            String xPath = String.Format("TextImport/KnownRowsets/Rowset[@name='{0}']", (null==OldName? cRowset.name:OldName));
            int ExistingRowsetCountByThisName = m_Doc.SelectNodes(xPath).Count;

            //add 
            if (OldName == null)
            {
                if (ExistingRowsetCountByThisName > 0)
                    throw new ArgumentException(string.Format(Properties.Resources.Error_CustomRowset_AlreadyExists, cRowset.name, xmlFileName));
                XmlNode nodeKnownRowsets = m_Doc.SelectSingleNode("TextImport/KnownRowsets");
                nodeKnownRowsets.AppendChild(el);
            }
            else
            {
                if (ExistingRowsetCountByThisName > 1)
                    throw new ArgumentException(String.Format(Properties.Resources.Error_CustomRowset_NotExists, OldName, xmlFileName));
                XmlNode node = m_Doc.SelectSingleNode(String.Format("TextImport/KnownRowsets/Rowset[@name='{0}']", OldName));
                if (node.HasChildNodes)
                    throw new ArgumentException (String.Format("the rowset {0} in {1} has know column types.  Current UI doesn't handle this. Please continue to modify the xml document manually", OldName, xmlFileName));
                if (node != null) //silently fail if it doesn't exist in xmldoc
                {
                    if (op == Operation.Edit)
                        node.ParentNode.ReplaceChild(el, node);
                    else if (op == Operation.Delete)
                        node.ParentNode.RemoveChild(node);
                }


            }
            m_Doc.Save(xmlFileName);
        }

        public String name;
        public bool enabled;
        public string identifier;
        public string type;
    }
}