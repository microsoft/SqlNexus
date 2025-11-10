namespace sqlnexus
{
    partial class fmPBReports
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(fmPBReports));
            this.label1 = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.LinkSQLLinuxReport = new System.Windows.Forms.LinkLabel();
            this.linkReadTraceRpt = new System.Windows.Forms.LinkLabel();
            this.linkPerfReport = new System.Windows.Forms.LinkLabel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.linkSystemXE = new System.Windows.Forms.LinkLabel();
            this.panel2.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.label1.Location = new System.Drawing.Point(2, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(157, 20);
            this.label1.TabIndex = 5;
            this.label1.Text = "Power BI Reports";
            // 
            // panel2
            // 
            this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel2.Controls.Add(this.label2);
            this.panel2.Location = new System.Drawing.Point(21, 47);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(872, 54);
            this.panel2.TabIndex = 4;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(16, 18);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(681, 20);
            this.label2.TabIndex = 0;
            this.label2.Text = "Please make sure that correct nexus Database name is entered for each report.";
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel1.Controls.Add(this.linkSystemXE);
            this.panel1.Controls.Add(this.LinkSQLLinuxReport);
            this.panel1.Controls.Add(this.linkReadTraceRpt);
            this.panel1.Controls.Add(this.linkPerfReport);
            this.panel1.Location = new System.Drawing.Point(21, 120);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(872, 358);
            this.panel1.TabIndex = 3;
            // 
            // LinkSQLLinuxReport
            // 
            this.LinkSQLLinuxReport.AutoSize = true;
            this.LinkSQLLinuxReport.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LinkSQLLinuxReport.Location = new System.Drawing.Point(39, 148);
            this.LinkSQLLinuxReport.Name = "LinkSQLLinuxReport";
            this.LinkSQLLinuxReport.Size = new System.Drawing.Size(215, 18);
            this.LinkSQLLinuxReport.TabIndex = 3;
            this.LinkSQLLinuxReport.TabStop = true;
            this.LinkSQLLinuxReport.Text = "SQL on Linux Perfmon Reports";
            this.LinkSQLLinuxReport.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkSQLLinuxReport_LinkClicked);
            // 
            // linkReadTraceRpt
            // 
            this.linkReadTraceRpt.AutoSize = true;
            this.linkReadTraceRpt.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkReadTraceRpt.Location = new System.Drawing.Point(39, 64);
            this.linkReadTraceRpt.Name = "linkReadTraceRpt";
            this.linkReadTraceRpt.Size = new System.Drawing.Size(138, 18);
            this.linkReadTraceRpt.TabIndex = 1;
            this.linkReadTraceRpt.TabStop = true;
            this.linkReadTraceRpt.Text = "ReadTrace Reports";
            this.linkReadTraceRpt.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkReadTraceRpt_LinkClicked);
            // 
            // linkPerfReport
            // 
            this.linkPerfReport.AutoSize = true;
            this.linkPerfReport.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkPerfReport.Location = new System.Drawing.Point(40, 28);
            this.linkPerfReport.Name = "linkPerfReport";
            this.linkPerfReport.Size = new System.Drawing.Size(151, 18);
            this.linkPerfReport.TabIndex = 0;
            this.linkPerfReport.TabStop = true;
            this.linkPerfReport.Text = "Performance Reports";
            this.linkPerfReport.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkPerfReport_LinkClicked);
            // 
            // panel3
            // 
            this.panel3.BackColor = System.Drawing.SystemColors.MenuHighlight;
            this.panel3.Controls.Add(this.label1);
            this.panel3.Location = new System.Drawing.Point(21, 12);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(872, 29);
            this.panel3.TabIndex = 6;
            // 
            // linkSystemXE
            // 
            this.linkSystemXE.AutoSize = true;
            this.linkSystemXE.Location = new System.Drawing.Point(40, 102);
            this.linkSystemXE.Name = "linkSystemXE";
            this.linkSystemXE.Size = new System.Drawing.Size(146, 17);
            this.linkSystemXE.TabIndex = 4;
            this.linkSystemXE.TabStop = true;
            this.linkSystemXE.Text = "System Health Report";
            this.linkSystemXE.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // fmPBReports
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(921, 522);
            this.Controls.Add(this.panel3);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "fmPBReports";
            this.Text = "Power BI Reports";
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.LinkLabel linkReadTraceRpt;
        private System.Windows.Forms.LinkLabel linkPerfReport;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.LinkLabel LinkSQLLinuxReport;
        private System.Windows.Forms.LinkLabel linkSystemXE;
    }
}