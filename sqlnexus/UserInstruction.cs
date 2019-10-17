using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;

using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;



    public partial class UserInstruction  : Form
    {
        string _URL;
        bool _fatel;
        public UserInstruction(string url, bool Fatel)
        {
            _URL = url;
            _fatel = Fatel;
            InitializeComponent();
        }

        private void Form3_Load(object sender, EventArgs e)
        {
            //string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName) + "\\ReportViewerAssemblyNOtFound.htm";


            webBrowser1.Navigate(_URL);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (_fatel == true)
                Application.Exit();
            else
            {
                this.Visible = false;
                this.Dispose();

            }

            
        }
    }

