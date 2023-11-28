namespace RowsetEditor
{
    partial class fmNewRowset
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
            this.components = new System.ComponentModel.Container();
            this.btnReturn = new System.Windows.Forms.Button();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.txtQuery = new System.Windows.Forms.TextBox();
            this.txtRowsetName = new System.Windows.Forms.TextBox();
            this.txtIdentifier = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.chkFromQuery = new System.Windows.Forms.CheckBox();
            this.btnExec = new System.Windows.Forms.Button();
            this.tabMain = new System.Windows.Forms.TabControl();
            this.tabGrid = new System.Windows.Forms.TabPage();
            this.dgvNewRowset = new System.Windows.Forms.DataGridView();
            this.tabConnect = new System.Windows.Forms.TabPage();
            this.txtServerName = new System.Windows.Forms.TextBox();
            this.btnconnect = new System.Windows.Forms.Button();
            this.tabSQL = new System.Windows.Forms.TabPage();
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colType = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colLength = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label3 = new System.Windows.Forms.Label();
            this.tabMain.SuspendLayout();
            this.tabGrid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvNewRowset)).BeginInit();
            this.tabConnect.SuspendLayout();
            this.tabSQL.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnReturn
            // 
            this.btnReturn.Location = new System.Drawing.Point(654, 108);
            this.btnReturn.Name = "btnReturn";
            this.btnReturn.Size = new System.Drawing.Size(116, 23);
            this.btnReturn.TabIndex = 0;
            this.btnReturn.Text = "Return Rowset";
            this.btnReturn.UseVisualStyleBackColor = true;
            this.btnReturn.Click += new System.EventHandler(this.btnReturn_Click);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(61, 4);
            // 
            // txtQuery
            // 
            this.txtQuery.Enabled = false;
            this.txtQuery.Location = new System.Drawing.Point(14, 6);
            this.txtQuery.Multiline = true;
            this.txtQuery.Name = "txtQuery";
            this.txtQuery.Size = new System.Drawing.Size(700, 289);
            this.txtQuery.TabIndex = 5;
            // 
            // txtRowsetName
            // 
            this.txtRowsetName.Location = new System.Drawing.Point(164, 29);
            this.txtRowsetName.Name = "txtRowsetName";
            this.txtRowsetName.Size = new System.Drawing.Size(463, 22);
            this.txtRowsetName.TabIndex = 0;
            // 
            // txtIdentifier
            // 
            this.txtIdentifier.Location = new System.Drawing.Point(164, 71);
            this.txtIdentifier.Name = "txtIdentifier";
            this.txtIdentifier.Size = new System.Drawing.Size(463, 22);
            this.txtIdentifier.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(40, 34);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(83, 16);
            this.label1.TabIndex = 8;
            this.label1.Text = "Table Name";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(40, 71);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(57, 16);
            this.label2.TabIndex = 9;
            this.label2.Text = "Identifier";
            // 
            // chkFromQuery
            // 
            this.chkFromQuery.AutoSize = true;
            this.chkFromQuery.Location = new System.Drawing.Point(31, 110);
            this.chkFromQuery.Name = "chkFromQuery";
            this.chkFromQuery.Size = new System.Drawing.Size(99, 20);
            this.chkFromQuery.TabIndex = 2;
            this.chkFromQuery.Text = "From Query";
            this.chkFromQuery.UseVisualStyleBackColor = true;
            this.chkFromQuery.CheckedChanged += new System.EventHandler(this.chkFromQuery_CheckedChanged);
            // 
            // btnExec
            // 
            this.btnExec.Location = new System.Drawing.Point(576, 301);
            this.btnExec.Name = "btnExec";
            this.btnExec.Size = new System.Drawing.Size(128, 23);
            this.btnExec.TabIndex = 11;
            this.btnExec.Text = "Exec Query";
            this.btnExec.UseVisualStyleBackColor = true;
            this.btnExec.Click += new System.EventHandler(this.btnExec_Click);
            // 
            // tabMain
            // 
            this.tabMain.Appearance = System.Windows.Forms.TabAppearance.FlatButtons;
            this.tabMain.Controls.Add(this.tabGrid);
            this.tabMain.Controls.Add(this.tabConnect);
            this.tabMain.Controls.Add(this.tabSQL);
            this.tabMain.ItemSize = new System.Drawing.Size(0, 1);
            this.tabMain.Location = new System.Drawing.Point(31, 156);
            this.tabMain.Multiline = true;
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(757, 524);
            this.tabMain.TabIndex = 12;
            // 
            // tabGrid
            // 
            this.tabGrid.Controls.Add(this.dgvNewRowset);
            this.tabGrid.Location = new System.Drawing.Point(4, 5);
            this.tabGrid.Name = "tabGrid";
            this.tabGrid.Padding = new System.Windows.Forms.Padding(3);
            this.tabGrid.Size = new System.Drawing.Size(749, 515);
            this.tabGrid.TabIndex = 1;
            this.tabGrid.Text = "tabGrid";
            this.tabGrid.UseVisualStyleBackColor = true;
            // 
            // dgvNewRowset
            // 
            this.dgvNewRowset.AllowUserToResizeRows = false;
            this.dgvNewRowset.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvNewRowset.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colName,
            this.colType,
            this.colLength});
            this.dgvNewRowset.Location = new System.Drawing.Point(8, 6);
            this.dgvNewRowset.Name = "dgvNewRowset";
            this.dgvNewRowset.RowHeadersWidth = 51;
            this.dgvNewRowset.RowTemplate.Height = 24;
            this.dgvNewRowset.Size = new System.Drawing.Size(705, 314);
            this.dgvNewRowset.TabIndex = 0;
            // 
            // tabConnect
            // 
            this.tabConnect.Controls.Add(this.label3);
            this.tabConnect.Controls.Add(this.txtServerName);
            this.tabConnect.Controls.Add(this.btnconnect);
            this.tabConnect.Location = new System.Drawing.Point(4, 5);
            this.tabConnect.Name = "tabConnect";
            this.tabConnect.Padding = new System.Windows.Forms.Padding(3);
            this.tabConnect.Size = new System.Drawing.Size(749, 515);
            this.tabConnect.TabIndex = 2;
            this.tabConnect.Text = "tabConnect";
            this.tabConnect.UseVisualStyleBackColor = true;
            // 
            // txtServerName
            // 
            this.txtServerName.Location = new System.Drawing.Point(81, 191);
            this.txtServerName.Name = "txtServerName";
            this.txtServerName.Size = new System.Drawing.Size(331, 22);
            this.txtServerName.TabIndex = 4;
            // 
            // btnconnect
            // 
            this.btnconnect.Location = new System.Drawing.Point(478, 190);
            this.btnconnect.Name = "btnconnect";
            this.btnconnect.Size = new System.Drawing.Size(179, 23);
            this.btnconnect.TabIndex = 5;
            this.btnconnect.Text = "Connect to SQL";
            this.btnconnect.UseVisualStyleBackColor = true;
            this.btnconnect.Click += new System.EventHandler(this.btnconnect_Click);
            // 
            // tabSQL
            // 
            this.tabSQL.Controls.Add(this.btnExec);
            this.tabSQL.Controls.Add(this.txtQuery);
            this.tabSQL.Location = new System.Drawing.Point(4, 5);
            this.tabSQL.Name = "tabSQL";
            this.tabSQL.Padding = new System.Windows.Forms.Padding(3);
            this.tabSQL.Size = new System.Drawing.Size(749, 515);
            this.tabSQL.TabIndex = 0;
            this.tabSQL.Text = "tabSQL";
            this.tabSQL.UseVisualStyleBackColor = true;
            // 
            // colName
            // 
            this.colName.DataPropertyName = "Name";
            this.colName.HeaderText = "Name";
            this.colName.MinimumWidth = 6;
            this.colName.Name = "colName";
            this.colName.Width = 125;
            // 
            // colType
            // 
            this.colType.DataPropertyName = "Type";
            this.colType.HeaderText = "Type";
            this.colType.Items.AddRange(new object[] {
            "DateTimeColumn",
            "IntColumn",
            "BigIntColumn",
            "NVarCharColumn",
            "VarCharColumn",
            "VarBinaryColumn",
            "FloatColumn",
            "DecimalColumn",
            "DateTimeOffsetColumn"});
            this.colType.MinimumWidth = 6;
            this.colType.Name = "colType";
            this.colType.Width = 125;
            // 
            // colLength
            // 
            this.colLength.DataPropertyName = "Length";
            this.colLength.HeaderText = "Length";
            this.colLength.MinimumWidth = 6;
            this.colLength.Name = "colLength";
            this.colLength.Width = 125;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(82, 165);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(116, 16);
            this.label3.TabIndex = 2;
            this.label3.Text = "SQL Server Name";
            // 
            // fmNewRowset
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 692);
            this.Controls.Add(this.tabMain);
            this.Controls.Add(this.chkFromQuery);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtIdentifier);
            this.Controls.Add(this.txtRowsetName);
            this.Controls.Add(this.btnReturn);
            this.Name = "fmNewRowset";
            this.Text = "New KnownRowset";
            this.tabMain.ResumeLayout(false);
            this.tabGrid.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvNewRowset)).EndInit();
            this.tabConnect.ResumeLayout(false);
            this.tabConnect.PerformLayout();
            this.tabSQL.ResumeLayout(false);
            this.tabSQL.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnReturn;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.TextBox txtQuery;
        private System.Windows.Forms.TextBox txtRowsetName;
        private System.Windows.Forms.TextBox txtIdentifier;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox chkFromQuery;
        private System.Windows.Forms.Button btnExec;
        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabSQL;
        private System.Windows.Forms.TabPage tabGrid;
        private System.Windows.Forms.DataGridView dgvNewRowset;
        private System.Windows.Forms.TabPage tabConnect;
        private System.Windows.Forms.TextBox txtServerName;
        private System.Windows.Forms.Button btnconnect;
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewComboBoxColumn colType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLength;
        private System.Windows.Forms.Label label3;
    }
}