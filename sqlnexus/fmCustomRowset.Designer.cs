namespace sqlnexus
{
    partial class fmCustomRowset
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
            this.grpAddorEditRowset = new System.Windows.Forms.GroupBox();
            this.rdDelete = new System.Windows.Forms.RadioButton();
            this.rdEditRowset = new System.Windows.Forms.RadioButton();
            this.rdAddRowset = new System.Windows.Forms.RadioButton();
            this.lblSelectRowset = new System.Windows.Forms.Label();
            this.cbSelectRowset = new System.Windows.Forms.ComboBox();
            this.lblRowsetName = new System.Windows.Forms.Label();
            this.txtRowsetName = new System.Windows.Forms.TextBox();
            this.grpEnableDisable = new System.Windows.Forms.GroupBox();
            this.rdDisable = new System.Windows.Forms.RadioButton();
            this.rdEnable = new System.Windows.Forms.RadioButton();
            this.lblIdentifier = new System.Windows.Forms.Label();
            this.txtIdentifier = new System.Windows.Forms.TextBox();
            this.lblType = new System.Windows.Forms.Label();
            this.cbType = new System.Windows.Forms.ComboBox();
            this.btnAdd = new System.Windows.Forms.Button();
            this.llCustomRowsetHelp = new System.Windows.Forms.LinkLabel();
            this.grpAddorEditRowset.SuspendLayout();
            this.grpEnableDisable.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpAddorEditRowset
            // 
            this.grpAddorEditRowset.Controls.Add(this.rdDelete);
            this.grpAddorEditRowset.Controls.Add(this.rdEditRowset);
            this.grpAddorEditRowset.Controls.Add(this.rdAddRowset);
            this.grpAddorEditRowset.Location = new System.Drawing.Point(32, 42);
            this.grpAddorEditRowset.Name = "grpAddorEditRowset";
            this.grpAddorEditRowset.Size = new System.Drawing.Size(287, 54);
            this.grpAddorEditRowset.TabIndex = 0;
            this.grpAddorEditRowset.TabStop = false;
            this.grpAddorEditRowset.Text = "Add or edit a rowset";
            // 
            // rdDelete
            // 
            this.rdDelete.AutoSize = true;
            this.rdDelete.Location = new System.Drawing.Point(195, 19);
            this.rdDelete.Name = "rdDelete";
            this.rdDelete.Size = new System.Drawing.Size(95, 17);
            this.rdDelete.TabIndex = 2;
            this.rdDelete.TabStop = true;
            this.rdDelete.Text = "Delete Rowset";
            this.rdDelete.UseVisualStyleBackColor = true;
            this.rdDelete.CheckedChanged += new System.EventHandler(this.rdDelete_CheckedChanged);
            // 
            // rdEditRowset
            // 
            this.rdEditRowset.AutoSize = true;
            this.rdEditRowset.Location = new System.Drawing.Point(104, 19);
            this.rdEditRowset.Name = "rdEditRowset";
            this.rdEditRowset.Size = new System.Drawing.Size(82, 17);
            this.rdEditRowset.TabIndex = 1;
            this.rdEditRowset.Text = "Edit Rowset";
            this.rdEditRowset.UseVisualStyleBackColor = true;
            this.rdEditRowset.CheckedChanged += new System.EventHandler(this.rdEditRowset_CheckedChanged);
            // 
            // rdAddRowset
            // 
            this.rdAddRowset.AutoSize = true;
            this.rdAddRowset.Checked = true;
            this.rdAddRowset.Location = new System.Drawing.Point(17, 19);
            this.rdAddRowset.Name = "rdAddRowset";
            this.rdAddRowset.Size = new System.Drawing.Size(83, 17);
            this.rdAddRowset.TabIndex = 0;
            this.rdAddRowset.TabStop = true;
            this.rdAddRowset.Text = "Add Rowset";
            this.rdAddRowset.UseVisualStyleBackColor = true;
            this.rdAddRowset.CheckedChanged += new System.EventHandler(this.rdAddRowset_CheckedChanged);
            // 
            // lblSelectRowset
            // 
            this.lblSelectRowset.AutoSize = true;
            this.lblSelectRowset.Location = new System.Drawing.Point(32, 108);
            this.lblSelectRowset.Name = "lblSelectRowset";
            this.lblSelectRowset.Size = new System.Drawing.Size(80, 13);
            this.lblSelectRowset.TabIndex = 1;
            this.lblSelectRowset.Text = "Select a rowset";
            // 
            // cbSelectRowset
            // 
            this.cbSelectRowset.FormattingEnabled = true;
            this.cbSelectRowset.Location = new System.Drawing.Point(143, 102);
            this.cbSelectRowset.Name = "cbSelectRowset";
            this.cbSelectRowset.Size = new System.Drawing.Size(174, 21);
            this.cbSelectRowset.TabIndex = 2;
            this.cbSelectRowset.SelectedIndexChanged += new System.EventHandler(this.cbSelectRowset_SelectedIndexChanged);
            // 
            // lblRowsetName
            // 
            this.lblRowsetName.AutoSize = true;
            this.lblRowsetName.Location = new System.Drawing.Point(32, 139);
            this.lblRowsetName.Name = "lblRowsetName";
            this.lblRowsetName.Size = new System.Drawing.Size(74, 13);
            this.lblRowsetName.TabIndex = 3;
            this.lblRowsetName.Text = "Rowset Name";
            // 
            // txtRowsetName
            // 
            this.txtRowsetName.Location = new System.Drawing.Point(143, 135);
            this.txtRowsetName.Name = "txtRowsetName";
            this.txtRowsetName.Size = new System.Drawing.Size(174, 20);
            this.txtRowsetName.TabIndex = 4;
            // 
            // grpEnableDisable
            // 
            this.grpEnableDisable.Controls.Add(this.rdDisable);
            this.grpEnableDisable.Controls.Add(this.rdEnable);
            this.grpEnableDisable.Location = new System.Drawing.Point(32, 168);
            this.grpEnableDisable.Name = "grpEnableDisable";
            this.grpEnableDisable.Size = new System.Drawing.Size(285, 53);
            this.grpEnableDisable.TabIndex = 5;
            this.grpEnableDisable.TabStop = false;
            this.grpEnableDisable.Text = "Enable this rowset";
            // 
            // rdDisable
            // 
            this.rdDisable.AutoSize = true;
            this.rdDisable.Location = new System.Drawing.Point(162, 25);
            this.rdDisable.Name = "rdDisable";
            this.rdDisable.Size = new System.Drawing.Size(60, 17);
            this.rdDisable.TabIndex = 1;
            this.rdDisable.Text = "Disable";
            this.rdDisable.UseVisualStyleBackColor = true;
            // 
            // rdEnable
            // 
            this.rdEnable.AutoSize = true;
            this.rdEnable.Checked = true;
            this.rdEnable.Location = new System.Drawing.Point(28, 25);
            this.rdEnable.Name = "rdEnable";
            this.rdEnable.Size = new System.Drawing.Size(58, 17);
            this.rdEnable.TabIndex = 0;
            this.rdEnable.TabStop = true;
            this.rdEnable.Text = "Enable";
            this.rdEnable.UseVisualStyleBackColor = true;
            // 
            // lblIdentifier
            // 
            this.lblIdentifier.AutoSize = true;
            this.lblIdentifier.Location = new System.Drawing.Point(32, 237);
            this.lblIdentifier.Name = "lblIdentifier";
            this.lblIdentifier.Size = new System.Drawing.Size(47, 13);
            this.lblIdentifier.TabIndex = 6;
            this.lblIdentifier.Text = "Identifier";
            // 
            // txtIdentifier
            // 
            this.txtIdentifier.Location = new System.Drawing.Point(143, 233);
            this.txtIdentifier.Name = "txtIdentifier";
            this.txtIdentifier.Size = new System.Drawing.Size(174, 20);
            this.txtIdentifier.TabIndex = 7;
            this.txtIdentifier.Text = "-- mytable";
            // 
            // lblType
            // 
            this.lblType.AutoSize = true;
            this.lblType.Location = new System.Drawing.Point(32, 264);
            this.lblType.Name = "lblType";
            this.lblType.Size = new System.Drawing.Size(31, 13);
            this.lblType.TabIndex = 8;
            this.lblType.Text = "Type";
            // 
            // cbType
            // 
            this.cbType.FormattingEnabled = true;
            this.cbType.Location = new System.Drawing.Point(143, 260);
            this.cbType.Name = "cbType";
            this.cbType.Size = new System.Drawing.Size(174, 21);
            this.cbType.TabIndex = 9;
            // 
            // btnAdd
            // 
            this.btnAdd.Location = new System.Drawing.Point(143, 314);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(75, 23);
            this.btnAdd.TabIndex = 10;
            this.btnAdd.Text = "Add";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // llCustomRowsetHelp
            // 
            this.llCustomRowsetHelp.AutoSize = true;
            this.llCustomRowsetHelp.Location = new System.Drawing.Point(289, 2);
            this.llCustomRowsetHelp.Name = "llCustomRowsetHelp";
            this.llCustomRowsetHelp.Size = new System.Drawing.Size(29, 13);
            this.llCustomRowsetHelp.TabIndex = 11;
            this.llCustomRowsetHelp.TabStop = true;
            this.llCustomRowsetHelp.Text = "Help";
            this.llCustomRowsetHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.llCustomRowsetHelp_LinkClicked);
            // 
            // fmCustomRowset
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(356, 356);
            this.Controls.Add(this.llCustomRowsetHelp);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.cbType);
            this.Controls.Add(this.lblType);
            this.Controls.Add(this.txtIdentifier);
            this.Controls.Add(this.lblIdentifier);
            this.Controls.Add(this.grpEnableDisable);
            this.Controls.Add(this.txtRowsetName);
            this.Controls.Add(this.lblRowsetName);
            this.Controls.Add(this.cbSelectRowset);
            this.Controls.Add(this.lblSelectRowset);
            this.Controls.Add(this.grpAddorEditRowset);
            this.Name = "fmCustomRowset";
            this.Text = "Manage Custom Rowset";
            this.Load += new System.EventHandler(this.fmCustomRowset_Load);
            this.grpAddorEditRowset.ResumeLayout(false);
            this.grpAddorEditRowset.PerformLayout();
            this.grpEnableDisable.ResumeLayout(false);
            this.grpEnableDisable.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox grpAddorEditRowset;
        private System.Windows.Forms.RadioButton rdEditRowset;
        private System.Windows.Forms.RadioButton rdAddRowset;
        private System.Windows.Forms.Label lblSelectRowset;
        private System.Windows.Forms.ComboBox cbSelectRowset;
        private System.Windows.Forms.Label lblRowsetName;
        private System.Windows.Forms.TextBox txtRowsetName;
        private System.Windows.Forms.GroupBox grpEnableDisable;
        private System.Windows.Forms.RadioButton rdDisable;
        private System.Windows.Forms.RadioButton rdEnable;
        private System.Windows.Forms.Label lblIdentifier;
        private System.Windows.Forms.TextBox txtIdentifier;
        private System.Windows.Forms.Label lblType;
        private System.Windows.Forms.ComboBox cbType;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.RadioButton rdDelete;
        private System.Windows.Forms.LinkLabel llCustomRowsetHelp;
    }
}