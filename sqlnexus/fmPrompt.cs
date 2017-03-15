using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace sqlnexus
{
    public partial class fmPrompt : Form
    {
        public fmPrompt()
        {
            InitializeComponent();
        }
        public fmPrompt(string prompt, string caption)
        {
            InitializeComponent();
            laPrompt.Text = prompt;
            this.Text = caption;
        }
        public static DialogResult ShowPrompt(string prompt, string caption, out bool dontAsk)
        {
            fmPrompt fmP = new fmPrompt(prompt, caption);
            DialogResult res=fmP.ShowDialog();
            dontAsk = fmP.ckDontAsk.Checked;
            return res;
        }
    }
}