using System.Collections.Generic;
using System.Windows.Forms;

namespace RowsetEditor
{
    partial class fmRowsetEditor
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
            this.cmbRowsets = new System.Windows.Forms.ComboBox();
            this.dgvKnownColumns = new System.Windows.Forms.DataGridView();
            this.ColumnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnType = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ColumnLength = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.valuetoken = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Modified = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.Inserted = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.btnSave = new System.Windows.Forms.Button();
            this.txtIdentifier = new System.Windows.Forms.TextBox();
            this.chkEnabled = new System.Windows.Forms.CheckBox();
            this.btnNew = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.btnDelete = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dgvKnownColumns)).BeginInit();
            this.SuspendLayout();
            // 
            // cmbRowsets
            // 
            this.cmbRowsets.FormattingEnabled = true;
            this.cmbRowsets.Location = new System.Drawing.Point(105, 41);
            this.cmbRowsets.Name = "cmbRowsets";
            this.cmbRowsets.Size = new System.Drawing.Size(584, 24);
            this.cmbRowsets.TabIndex = 0;
            this.cmbRowsets.SelectedIndexChanged += new System.EventHandler(this.cmbRowsets_SelectedIndexChanged);
            // 
            // dgvKnownColumns
            // 
            this.dgvKnownColumns.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvKnownColumns.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnName,
            this.ColumnType,
            this.ColumnLength,
            this.valuetoken,
            this.Modified,
            this.Inserted});
            this.dgvKnownColumns.Location = new System.Drawing.Point(12, 170);
            this.dgvKnownColumns.Name = "dgvKnownColumns";
            this.dgvKnownColumns.RowHeadersWidth = 51;
            this.dgvKnownColumns.RowTemplate.Height = 24;
            this.dgvKnownColumns.Size = new System.Drawing.Size(881, 588);
            this.dgvKnownColumns.TabIndex = 1;
            this.dgvKnownColumns.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.dgvKnownColumns_CellBeginEdit);
            this.dgvKnownColumns.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvKnownColumns_CellEndEdit);
            this.dgvKnownColumns.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dgvKnownColumns_DataError);
            this.dgvKnownColumns.DefaultValuesNeeded += new System.Windows.Forms.DataGridViewRowEventHandler(this.dgvKnownColumns_DefaultValuesNeeded);
            this.dgvKnownColumns.UserDeletedRow += new System.Windows.Forms.DataGridViewRowEventHandler(this.dgvKnownColumns_UserDeletedRow);
            // 
            // ColumnName
            // 
            this.ColumnName.DataPropertyName = "ColumnName";
            this.ColumnName.HeaderText = "Column Name";
            this.ColumnName.MinimumWidth = 6;
            this.ColumnName.Name = "ColumnName";
            this.ColumnName.Width = 125;
            // 
            // ColumnType
            // 
            this.ColumnType.DataPropertyName = "ColumnType";
            this.ColumnType.HeaderText = "Column Type";
            this.ColumnType.Items.AddRange(new object[] {
            "DateTimeColumn",
            "IntColumn",
            "BigIntColumn",
            "NVarCharColumn",
            "VarCharColumn",
            "VarBinaryColumn",
            "FloatColumn",
            "DecimalColumn",
            "DateTimeOffsetColumn"});
            this.ColumnType.MinimumWidth = 6;
            this.ColumnType.Name = "ColumnType";
            this.ColumnType.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnType.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.ColumnType.Width = 125;
            // 
            // ColumnLength
            // 
            this.ColumnLength.DataPropertyName = "ColumnLength";
            this.ColumnLength.HeaderText = "Length";
            this.ColumnLength.MinimumWidth = 6;
            this.ColumnLength.Name = "ColumnLength";
            this.ColumnLength.Width = 125;
            // 
            // valuetoken
            // 
            this.valuetoken.DataPropertyName = "valuetoken";
            this.valuetoken.HeaderText = "Value Token";
            this.valuetoken.MinimumWidth = 6;
            this.valuetoken.Name = "valuetoken";
            this.valuetoken.Width = 125;
            // 
            // Modified
            // 
            this.Modified.DataPropertyName = "Modified";
            this.Modified.HeaderText = "Modified";
            this.Modified.MinimumWidth = 6;
            this.Modified.Name = "Modified";
            this.Modified.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.Modified.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.Modified.Visible = false;
            this.Modified.Width = 125;
            // 
            // Inserted
            // 
            this.Inserted.DataPropertyName = "Inserted";
            this.Inserted.HeaderText = "Inserted";
            this.Inserted.MinimumWidth = 6;
            this.Inserted.Name = "Inserted";
            this.Inserted.Visible = false;
            this.Inserted.Width = 125;
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(549, 127);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(139, 23);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "Save Rowset";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // txtIdentifier
            // 
            this.txtIdentifier.Location = new System.Drawing.Point(105, 81);
            this.txtIdentifier.Name = "txtIdentifier";
            this.txtIdentifier.Size = new System.Drawing.Size(584, 22);
            this.txtIdentifier.TabIndex = 3;
            this.txtIdentifier.TextChanged += new System.EventHandler(this.txtIdentifier_TextChanged);
            // 
            // chkEnabled
            // 
            this.chkEnabled.AutoSize = true;
            this.chkEnabled.Location = new System.Drawing.Point(695, 83);
            this.chkEnabled.Name = "chkEnabled";
            this.chkEnabled.Size = new System.Drawing.Size(80, 20);
            this.chkEnabled.TabIndex = 4;
            this.chkEnabled.Text = "Enabled";
            this.chkEnabled.UseVisualStyleBackColor = true;
            this.chkEnabled.CheckedChanged += new System.EventHandler(this.chkEnabled_CheckedChanged);
            // 
            // btnNew
            // 
            this.btnNew.Location = new System.Drawing.Point(266, 127);
            this.btnNew.Name = "btnNew";
            this.btnNew.Size = new System.Drawing.Size(132, 23);
            this.btnNew.TabIndex = 5;
            this.btnNew.Text = "New Rowset";
            this.btnNew.UseVisualStyleBackColor = true;
            this.btnNew.Click += new System.EventHandler(this.btnNew_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 44);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(83, 16);
            this.label1.TabIndex = 6;
            this.label1.Text = "Table Name";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 84);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(57, 16);
            this.label2.TabIndex = 7;
            this.label2.Text = "Identifier";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(16, 151);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(101, 16);
            this.label3.TabIndex = 8;
            this.label3.Text = "Known Columns";
            // 
            // btnDelete
            // 
            this.btnDelete.Location = new System.Drawing.Point(404, 127);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(139, 23);
            this.btnDelete.TabIndex = 9;
            this.btnDelete.Text = "Delete Rowset";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // fmRowsetEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(905, 763);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnNew);
            this.Controls.Add(this.chkEnabled);
            this.Controls.Add(this.txtIdentifier);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.dgvKnownColumns);
            this.Controls.Add(this.cmbRowsets);
            this.Name = "fmRowsetEditor";
            this.Text = "Rowset Editor";
            ((System.ComponentModel.ISupportInitialize)(this.dgvKnownColumns)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

  
        private System.Windows.Forms.ComboBox cmbRowsets;
        private DataGridView dgvKnownColumns;
        private Button btnSave;
        private TextBox txtIdentifier;
        private CheckBox chkEnabled;
        private Button btnNew;
        private Label label1;
        private Label label2;
        private Label label3;
        private Button btnDelete;
        private DataGridViewTextBoxColumn ColumnName;
        private DataGridViewComboBoxColumn ColumnType;
        private DataGridViewTextBoxColumn ColumnLength;
        private DataGridViewTextBoxColumn valuetoken;
        private DataGridViewCheckBoxColumn Modified;
        private DataGridViewCheckBoxColumn Inserted;
    }
}