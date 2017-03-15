using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace sqlnexus
{
    public partial class fmEula : Form
    {
        public fmEula()
        {
            InitializeComponent();
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        public static DialogResult ShowPrompt()
        {
            fmEula fmE = new fmEula();
            return fmE.ShowDialog();
        }

        private void fmEula_Load(object sender, EventArgs e)
        {
            // read eula from text file
            StreamReader tr = new StreamReader("eula.txt");
            txtEula.Text = tr.ReadToEnd();
            txtEula.Select(0, 0);
            tr.Close();
        }
    }
}