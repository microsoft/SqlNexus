using RowsetImportEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using sqlnexus;
using Microsoft.Win32;

namespace RowsetEditor
{
    public partial class fmRowsetEditor : Form
    {
        
        private Hashtable KnownRowsets;
        private DataTable tblKnownColumns;
        private String originalValue;
        private int changeRowIndex;
        private XmlDocument xDoc;
        private Boolean isHeaderChanged = false;
        private BindingSource BS;
        private Boolean hasRemovedRows;
        private Boolean hasAddedRows;
        private DictionaryEntry entrySelectedRowset;
        private String TextRowsetXMLFile;
        private String[] changedDatasets;



        public fmRowsetEditor()
        {
            InitializeComponent();
            //dgvKnownColumns.AutoGenerateColumns = false;

            tblKnownColumns = new DataTable();
            tblKnownColumns.Columns.Add("ColumnName");
            tblKnownColumns.Columns.Add("ColumnType");
            tblKnownColumns.Columns.Add("ColumnLength");
            tblKnownColumns.Columns.Add("valuetoken");
            
            //Hidden column to track modifications
            DataColumn c = new DataColumn("Modified");
            c.DataType = typeof(Boolean);
            tblKnownColumns.Columns.Add(c);

            //Hidden column to track inserted values.
            DataColumn insertedCol = new DataColumn("Inserted");
            insertedCol.DataType = typeof(Boolean);
            tblKnownColumns.Columns.Add(insertedCol);
        }
        public void setKnownRowsets (Hashtable ks, ref XmlDocument Doc, String fileName)
        {
            TextRowsetXMLFile = fileName;
            KnownRowsets = ks;
            xDoc = Doc;
            BS = new BindingSource() ;
            BS.DataSource = KnownRowsets;
            cmbRowsets.DataSource = BS;
            cmbRowsets.DisplayMember = "Key";
            cmbRowsets.ValueMember = "Value";

        }

        private void cmbRowsets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (hasRemovedRows || isHeaderChanged || hasAddedRows)
            {
                if (MessageBox.Show("There are unsaved changes, Yes to save them? No and they will be lost", "Question", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    btnSave.PerformClick();
                }
            }

            //I user deletes rows hasRemovedRows will be set to on and we will rebuild the whole node.
            hasRemovedRows = false;

            tblKnownColumns.Rows.Clear();
            txtIdentifier.Text = string.Empty;
            
            //if we don't have a selection simply exit
            if (cmbRowsets.SelectedItem == null) { return; }
            
            //Collect selected Dictionary Item key is table name and value is XmlNode.
            entrySelectedRowset = (DictionaryEntry)  cmbRowsets.SelectedItem;
            XmlNode RowsetNode = (XmlNode) entrySelectedRowset.Value;
            
            IEnumerable <XmlNode> ColumnsNode = RowsetNode.ChildNodes.OfType<XmlNode>().Where(x => x.Name == "KnownColumns");
            
            //if we don't have <KnownColumns> then we either don't have columns or columns are listed without this tag.
            if (ColumnsNode.Any<XmlNode>())
            {
                //Since we have KnownColumsn, retrieve one level down for columns
                ColumnsNode = ColumnsNode.First<XmlNode>().ChildNodes.OfType<XmlNode>().Where(x => x.Name == "Column");
            } else
            {
                //try to get columns directly
                ColumnsNode = RowsetNode.ChildNodes.OfType<XmlNode>().Where(x => x.Name == "Column");
            }


            foreach (XmlNode nodeColumn in ColumnsNode) 
            {
                
                XmlElement elColumn = nodeColumn as XmlElement;
                String sType = elColumn.GetAttribute("type").Replace("RowsetImportEngine.", "");

                tblKnownColumns.Rows.Add(elColumn.GetAttribute("name"), sType, elColumn.GetAttribute("length"), elColumn.GetAttribute("valuetoken"), false);
                
            }
            
            dgvKnownColumns.DataSource = new BindingSource (tblKnownColumns, null);

            String strRowsetID = ((XmlElement)RowsetNode).GetAttribute("identifier");
            String strEnabled = ((XmlElement)RowsetNode).GetAttribute("enabled");

            chkEnabled.Checked = strEnabled.Equals("true");
            txtIdentifier.Text = strRowsetID;
            isHeaderChanged = false;

        }


        private void btnSave_Click(object sender, EventArgs e)
        {
            
            XmlNode RowsetNode = (XmlNode)entrySelectedRowset.Value;

            XmlNode KnownColumnsNode = RowsetNode.FirstChild;
            Boolean hasKnownColumnsTag = KnownColumnsNode.Name == "KnownColumns";

            //if we the node doesn't have <knownColumns> then we need to add it.
            if (!hasKnownColumnsTag)
            {
                RowsetNode.InnerXml = String.Concat("<KnownColumns>", RowsetNode.InnerXml, "</KnownColumns>");
            }

            String rowsetEnabled = "true";
            String rowsetId;
            String selectedRowsetName = (String) entrySelectedRowset.Key;

            //if user updated the header , rowset identifier or enabled state then we need to update the full node.

            if (isHeaderChanged)
            {
                if (!chkEnabled.Checked) { rowsetEnabled = "false"; }
                rowsetId = txtIdentifier.Text;
                ((XmlElement)RowsetNode).SetAttribute("enabled", rowsetEnabled);
                ((XmlElement)RowsetNode).SetAttribute("identifier", rowsetId);
            }

            DataView dv = new DataView(tblKnownColumns);

            if (!hasRemovedRows)
            {
                //if we don't have removed rows, then filter on modified only
                dv.RowFilter = "Modified = true";
            } else
            {
                //if we have removed rows, we consider all as new and remove all old ones
                KnownColumnsNode.RemoveAll();
            }

            foreach (DataRowView vRow in dv)
            {
                DataRow row = vRow.Row;

                String elementName = row["ColumnName"] as string;
                String elementLength = row["ColumnLength"] as string;
                String elementValueToken = row["valuetoken"] as string;
                String elementType = String.Concat("RowsetImportEngine.", (row["ColumnType"] as string));

                Boolean ins = hasRemovedRows || (row["Inserted"] == DBNull.Value ? false : (bool)row["Inserted"]);

                if (!ins) //not insert , then update row
                {
                    foreach (XmlElement xEl in KnownColumnsNode.ChildNodes)
                    {
                        if (elementName == xEl.GetAttribute("name"))
                        {

                            xEl.SetAttribute("type", elementType);
                            if (elementLength != null && elementLength.Length > 0)
                            {
                                xEl.SetAttribute("length", elementLength);
                            }

                            if (elementValueToken != null && elementValueToken.Length > 0)
                            {
                                xEl.SetAttribute("valuetoken", elementValueToken);
                            }

                        }
                    }
                } else //this is a new row we need ot insert
                {
                    XmlElement xCol = xDoc.CreateElement("Column");
                    xCol.SetAttribute("name", elementName);
                    xCol.SetAttribute("type", elementType);
                    if (elementLength != null && elementLength.Length > 0)
                    {
                        xCol.SetAttribute("length", elementLength);
                    }
                    if (elementValueToken != null && elementValueToken.Length> 0)
                    {
                        xCol.SetAttribute("valuetoken", elementValueToken);
                    }
                    

                    
                    KnownColumnsNode.AppendChild(xCol);
                }
                row["Inserted"] = false;
                row["Modified"] = false;
 
            }

            foreach (XmlNode node in xDoc.DocumentElement.ChildNodes)
            {
                // first node is <KnownRowsets> ... have to go to nexted loc node 
                foreach (XmlElement locNode in node.OfType<XmlElement>().Where(n => n.Name.Equals("Rowset") && n.GetAttribute("name") == selectedRowsetName))
                {
                    node.ReplaceChild(RowsetNode, locNode);
                }
            }
            //overwrite original file, we can add functionality to allow users to create a copy instead.
            try
            {
                if (MessageBox.Show("Do you want to save your changes?", "Save Rowsets", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    String fName = TextRowsetXMLFile;

#if DEBUG 
                    if (MessageBox.Show("In debug mode \n Do you want to save to a different file", "Save Rowsets", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        fName = "c:\\del\\r.xml";
                    }
                    
#endif
                    xDoc.Save(fName);
                    MessageBox.Show("Document updated and saved successfully", "Save Rowsets", MessageBoxButtons.OK);
                }

                cmbRowsets.Refresh();
                isHeaderChanged = false;
                hasAddedRows = false;

            } catch (Exception ex)
            {
                MessageBox.Show("Document failed to save with message :  \n" + ex.Message, "Save Rowsets", MessageBoxButtons.OK);
            }
        }

        private void dgvKnownColumns_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            
            originalValue = dgvKnownColumns[e.ColumnIndex, e.RowIndex].Value as String;
            changeRowIndex = e.RowIndex;

        }

        private void dgvKnownColumns_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            changeRowIndex =  e.RowIndex;
            String currentValue = dgvKnownColumns[e.ColumnIndex, e.RowIndex].Value as String;
            Boolean isModified = false;
            if (dgvKnownColumns["Modified", changeRowIndex].Value != null) { 
                isModified = (bool)dgvKnownColumns["Modified", changeRowIndex].Value;
            }
            dgvKnownColumns["Modified", changeRowIndex].Value = (originalValue != currentValue) || isModified;
            return;


        }

        private void txtIdentifier_TextChanged(object sender, EventArgs e)
        {
            isHeaderChanged = true;
        }

        private void chkEnabled_CheckedChanged(object sender, EventArgs e)
        {
            isHeaderChanged = true;
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            fmNewRowset fmNew = new fmNewRowset();
            fmNew.ShowDialog(this);


            String strRowsetName = fmNew.newRowsetName;
            String strNewIdentifier = fmNew.newIdentifier;

            fmNew.Dispose();

            //if we didnt' receive any Rowset name return without adding new record.
            if (string.IsNullOrEmpty( strRowsetName) ) { return;  }


            XmlDocument doc = new XmlDocument();

            XmlElement xKnownRowset = xDoc.CreateElement("Rowset");
;
            XmlElement xKnownColumns = xDoc.CreateElement("KnownColumns");

            xKnownRowset.AppendChild(xKnownColumns);

            xKnownRowset.SetAttribute("name", strRowsetName);
            xKnownRowset.SetAttribute("enabled", "true");
            xKnownRowset.SetAttribute("identifier", strNewIdentifier);
            xKnownRowset.SetAttribute("type", "RowsetImportEngine.TextRowset");
            

            chkEnabled.Checked = true;

            KnownRowsets.Add(strRowsetName, (XmlNode)xKnownRowset);
            xDoc.DocumentElement.FirstChild.AppendChild(xKnownRowset);

            cmbRowsets.DataSource = new BindingSource(KnownRowsets, null);
            //BS.ResetBindings(false);
            //BS.ResetAllowNew();
            //BS = new BindingSource();
            //BS.DataSource = KnownRowsets;

            //cmbRowsets.DataSource = BS;
            //cmbRowsets.Refresh();
            int i = cmbRowsets.FindString(strRowsetName);
            
            cmbRowsets.SelectedIndex = i;

        }

        private void dgvKnownColumns_DefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            
            e.Row.Cells["ColumnName"].Value = "Enter Column Name";
            e.Row.Cells["Modified"].Value = false;
            e.Row.Cells["ColumnType"].Value = "NVarCharColumn";
            e.Row.Cells["Inserted"].Value = true;

        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            String rowsetName = entrySelectedRowset.Key.ToString();

            //remove from hashtable
            KnownRowsets.Remove(rowsetName);

            cmbRowsets.DataSource = new BindingSource (KnownRowsets, null);
            cmbRowsets.Refresh();

            //remove from document
            foreach (XmlNode node in xDoc.DocumentElement.ChildNodes)
            {
                foreach (XmlElement node2 in node.OfType<XmlElement>().Where(n => n.Name == "Rowset" && n.GetAttribute("name") == rowsetName ))
                {
                    node.RemoveChild(node2);
                }
            }
        }

        private void dgvKnownColumns_UserDeletedRow(object sender, DataGridViewRowEventArgs e)
        {
            hasRemovedRows = true;
            //e.Row
        }

        private void dgvKnownColumns_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            //ignore Data Errors from DataGrid (since they come from combobox mainly)
            return;
        }

        private void dgvKnownColumns_UserAddedRow(object sender, DataGridViewRowEventArgs e)
        {
            hasAddedRows = true;
            return;
        }
    }
}
