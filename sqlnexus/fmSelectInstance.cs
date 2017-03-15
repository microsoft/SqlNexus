using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace sqlnexus
{
    public partial class fmSelectInstance : Form
    {
        public fmSelectInstance()
        {
            InitializeComponent();
        }

        private void fmSelectInstance_Load(object sender, EventArgs e)
        {
            SqlInstances inst = (SqlInstances)this.Tag;

            foreach (String k in inst.InstanceList())
            {
                cmbInstanceList.Items.Add (k);
            }
            cmbInstanceList.SelectedIndex = 0;
            Application.DoEvents();
        }

        private void cmbInstanceList_SelectedIndexChanged(object sender, EventArgs e)
        {
            SqlInstances inst = (SqlInstances)this.Tag;
            ComboBox cb = (ComboBox)sender;
            inst.InstanceToImport = cb.SelectedItem.ToString();

        }
    }
}