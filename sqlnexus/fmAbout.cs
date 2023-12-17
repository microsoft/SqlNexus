using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Reporting.WinForms;

namespace sqlnexus
{
    public partial class fmAbout : Form
    {

        private Point mouseloc;

        public fmAbout()
        {
            InitializeComponent();
        }

        private void fmAbout_Paint(object sender, PaintEventArgs e)
        {
            System.Drawing.Drawing2D.GraphicsPath shape = new System.Drawing.Drawing2D.GraphicsPath();
            shape.AddEllipse(0, 0, this.Width, this.Height);
            this.Region = new System.Drawing.Region(shape);
        }

        private void btClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void tiStarter_Tick(object sender, EventArgs e)
        {
//            tiMover.Enabled = !tiMover.Enabled;
        }

        private void tiMover_Tick(object sender, EventArgs e)
        {
            tiMover.Enabled = false;
            const int delta=10;
            Random r = new Random();
            int moverx = Convert.ToInt32(r.NextDouble());
            int movery = Convert.ToInt32(r.NextDouble());
            if (0 == moverx)
                moverx = -1;
            if (0 == movery)
                movery = -1;

            for (int i = 0; i < delta; i++)
            {
                try
                {
                    int newx = this.Location.X + moverx;
                    int newy = this.Location.Y + movery;
                    if (newx < 0) newx = 0;
                    else if (newx > this.Size.Width) newx = this.Size.Width;
                    if (newy < 0) newy = 0;
                    else if (newy > this.Size.Height) newy = this.Size.Height;
                    
                    this.Location = new Point(newx, newy);
                }
                catch (Exception ex)  //Might get an exception if we try to go off the screen 
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
            tiMover.Enabled = true;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(Properties.Resources.Msg_Nexus_SupportUrl);
        }

        double opacityInc = .05;
        double opacityMax = 1;

        private void tiDissolve_Tick(object sender, EventArgs e)
        {
            if (this.Opacity != opacityMax)
            {
                this.Opacity += opacityInc;
            }
            else if (0 == this.Opacity)
                Close();
            else
            {
                tiDissolve.Enabled = false;
            }
        }

        private void fmAbout_FormClosing(object sender, FormClosingEventArgs e)
        {
            opacityInc = opacityInc*-1;
            opacityMax = 0;
            tiDissolve.Enabled = !tiDissolve.Enabled;
            e.Cancel = tiDissolve.Enabled;
        }

        private void tiWeb_Tick(object sender, EventArgs e)
        {
        }

        private void fmAbout_Load(object sender, EventArgs e)
        {
            //Bitmap Logo = new Bitmap(pbClose.Image);
            //Logo.MakeTransparent(Logo.GetPixel(0, 0));
            //pbClose.Image = (Image)Logo;

            Assembly me = Assembly.GetExecutingAssembly();
            foreach (Attribute a in me.GetCustomAttributes(true))
            {
                if (a is AssemblyCopyrightAttribute)
                    laCopyright.Text = (a as AssemblyCopyrightAttribute).Copyright;
                if (a is AssemblyFileVersionAttribute)
                    laVersion.Text = "Version: " + (a as AssemblyFileVersionAttribute).Version;
            }

            ReportViewer rv = new ReportViewer();
            Assembly rvAssembly = rv.GetType().Assembly;
            Version rvVersion = rvAssembly.GetName().Version;
            
            laRVVersion.Text = "ReportViewer : " + rvVersion;
        }

        private void btClose_Paint(object sender, PaintEventArgs e)
        {
            System.Drawing.Drawing2D.GraphicsPath buttonPath =
                   new System.Drawing.Drawing2D.GraphicsPath();

            // Set a new rectangle to the same size as the button's 
            // ClientRectangle property.
            System.Drawing.Rectangle newRectangle = btClose.ClientRectangle;

            // Decrease the size of the rectangle.
            newRectangle.Inflate(-10, -10);

            // Draw the button's border.
            e.Graphics.DrawEllipse(System.Drawing.Pens.White, newRectangle);

            // Increase the size of the rectangle to include the border.
            newRectangle.Inflate(1, 1);

            // Create a circle within the new rectangle.
            buttonPath.AddEllipse(newRectangle);

            // Set the button's Region property to the newly created 
            // circle region.
            btClose.Region = new System.Drawing.Region(buttonPath);
        }

        private void fmAbout_MouseDown(object sender, MouseEventArgs e)
        {
            mouseloc = new Point(-e.X, -e.Y);
        }

        private void fmAbout_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point p = Control.MousePosition;
                p.Offset(mouseloc.X, mouseloc.Y);
                Location = p;
            }
        }

        private void laVersion_Click(object sender, EventArgs e)
        {

        }

        private void laRVVersion_Click(object sender, EventArgs e)
        {

        }
    }
}