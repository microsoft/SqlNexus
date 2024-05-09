using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace sqlnexus
{
    public partial class fmPBReports : Form
    {
        string path;
        public fmPBReports()
        {
            InitializeComponent();
            g_theme.fRec_setControlColors(this);
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            path = Path.Combine(directory + @"Reports\PowerBIReports");
           
             
        }

        private void linkPerfReport_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string reportPath= Path.Combine(path, "Performance Reports.pbit");
            Process.Start(reportPath);
        }

        private void linkReadTraceRpt_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string reportPath = Path.Combine(path, "ReadTrace Reports.pbit");
            Process.Start(reportPath);
        }

       

        private void LinkSQLLinuxReport_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string reportPath = Path.Combine(path, "SQL Perfmon Viewer.pbit");
            Process.Start(reportPath);

        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string reportPath = Path.Combine(path, "System Health Session.pbit");
            Process.Start(reportPath);
        }
    }
}
