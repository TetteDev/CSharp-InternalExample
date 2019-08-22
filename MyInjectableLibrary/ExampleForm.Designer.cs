namespace MyInjectableLibrary
{
	partial class ExampleForm
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
			this.numericUpDown1 = new System.Windows.Forms.NumericUpDown();
			this.button1 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).BeginInit();
			this.SuspendLayout();
			// 
			// numericUpDown1
			// 
			this.numericUpDown1.DecimalPlaces = 2;
			this.numericUpDown1.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
			this.numericUpDown1.Location = new System.Drawing.Point(13, 13);
			this.numericUpDown1.Name = "numericUpDown1";
			this.numericUpDown1.Size = new System.Drawing.Size(180, 20);
			this.numericUpDown1.TabIndex = 0;
			this.numericUpDown1.Value = new decimal(new int[] {
            1,
            0,
            0,
            131072});
			// 
			// button1
			// 
			this.button1.Location = new System.Drawing.Point(13, 39);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(180, 23);
			this.button1.TabIndex = 1;
			this.button1.Text = "Set Movement Speed Multiplier";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(this.Button1_Click_1);
			// 
			// button2
			// 
			this.button2.Location = new System.Drawing.Point(12, 68);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(180, 23);
			this.button2.TabIndex = 2;
			this.button2.Text = "Reset Movement Speed Multiplier";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(this.Button2_Click);
			// 
			// ExampleForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(341, 314);
			this.Controls.Add(this.button2);
			this.Controls.Add(this.button1);
			this.Controls.Add(this.numericUpDown1);
			this.Name = "ExampleForm";
			this.Text = "ExampleForm";
			this.Load += new System.EventHandler(this.ExampleForm_Load);
			((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.NumericUpDown numericUpDown1;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button button2;
	}
}