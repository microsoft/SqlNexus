using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace RowsetEditor
{
    public partial class fmNewRowset : Form
    {
        public String newRowsetName;
        public String newIdentifier;
        public XmlNode xKnownColumns;
        public fmNewRowset()
        {
            InitializeComponent();
        }

        private void btnReturn_Click(object sender, EventArgs e)
        {
            newRowsetName = txtRowsetName.Text;
            newIdentifier = txtIdentifier.Text;
            if (!chkFromQuery.Checked)
            {
                this.Close();
            } else
            {
               // xKnownColumns = new XmlNode("Node");
                return;
            }
        }
    }
}
