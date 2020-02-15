using System;
using System.Linq;
using System.Windows.Forms;

namespace MyInjectableLibrary
{
	public unsafe partial  class ExampleForm : Form
	{
		public ExampleForm()
		{
			InitializeComponent();
		}

		public void OnStartup()
		{
			
			
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			
		}

		private void ExampleForm_Load(object sender, EventArgs e)
		{
			CheckForIllegalCrossThreadCalls = false;

			var values = Enum.GetValues(typeof(Keys)).Cast<Keys>();
			foreach (var key in values)
			{
				comboBox1.Items.Add(key.ToString());
			}

			comboBox1.Text = "V";
		}

		private void Button2_Click(object sender, EventArgs e)
		{
			
		}

		private void Button1_Click_1(object sender, EventArgs e)
		{
			
		}

		private void NumericUpDown1_ValueChanged(object sender, EventArgs e)
		{

		}

		

		

		private void CheckBox1_CheckedChanged(object sender, EventArgs e)
		{
			
		}

		private void ToolStripDropDownButton1_Click(object sender, EventArgs e)
		{

		}

		private void ToolStripDropDownButton1_TextChanged(object sender, EventArgs e)
		{
			
		}

		private void Button1_Click_2(object sender, EventArgs e)
		{
			
		}

		private void Button2_Click_1(object sender, EventArgs e)
		{
			
		}

		private void Button3_Click(object sender, EventArgs e)
		{
			
		}

		private void Button4_Click(object sender, EventArgs e)
		{
			
		}

		private void CheckBox2_CheckedChanged(object sender, EventArgs e)
		{
			
		}

		private void CheckBox3_CheckedChanged(object sender, EventArgs e)
		{
			
		}

		private void Button5_Click(object sender, EventArgs e)
		{
			
		}

		private void Button6_Click(object sender, EventArgs e)
		{
			
		}

		private void Button7_Click(object sender, EventArgs e)
		{
			
		}

		public enum WindowType : byte
		{
			// Token: 0x040001CA RID: 458
			ActionSit = 2,
			// Token: 0x040001CB RID: 459
			ActionStand = 5,
			// Token: 0x040001CC RID: 460
			OptimizeInterface = 13,
			// Token: 0x040001CD RID: 461
			FusionInterface,
			// Token: 0x040001CE RID: 462
			BlacksmithInterface = 16,
			// Token: 0x040001CF RID: 463
			TeleportInterface,
			// Token: 0x040001D0 RID: 464
			EnhancementInterface = 19,
			// Token: 0x040001D1 RID: 465
			EidolonFeedInterface = 24,
			// Token: 0x040001D2 RID: 466
			ArcheologyInterface = 26,
			// Token: 0x040001D3 RID: 467
			AlchemyInterface,
			// Token: 0x040001D4 RID: 468
			EidolonOptimisationInterface,
			// Token: 0x040001D5 RID: 469
			HousingCraftInterface = 30,
			// Token: 0x040001D6 RID: 470
			BegleiterWnd
		}

		private void CheckBox5_CheckedChanged(object sender, EventArgs e)
		{
			
		}

		

		private void Label5_Click(object sender, EventArgs e)
		{

		}

		private void Label6_Click(object sender, EventArgs e)
		{
			
		}

		private void Button8_Click(object sender, EventArgs e)
		{
			
		}

		private void CheckBox1_CheckedChanged_1(object sender, EventArgs e)
		{
			
		}

		private void CheckBox4_CheckedChanged(object sender, EventArgs e)
		{
			
		}

		private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			
		}

		private void CheckBox5_CheckedChanged_1(object sender, EventArgs e)
		{
			
		}

		private void CheckBox2_CheckedChanged_1(object sender, EventArgs e)
		{
			
		}

		private void CheckBox6_CheckedChanged(object sender, EventArgs e)
		{
			
		}

		private void CheckBox7_CheckedChanged(object sender, EventArgs e)
		{
			
		}

		private void CheckBox8_CheckedChanged(object sender, EventArgs e)
		{
			
		}
	}
}
