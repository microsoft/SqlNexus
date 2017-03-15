using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace sqlnexus
{
    public partial class fmStartCollectorPrompt : Form
    {
        public fmStartCollectorPrompt()
        {
            InitializeComponent();
        }
        public fmStartCollectorPrompt(string prompt)
        {
            InitializeComponent();
            this.label1.Text = prompt;
        }

    }
}