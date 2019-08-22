using System;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows.Forms;

namespace MyInjectableLibrary
{
	public unsafe partial  class ExampleForm : Form
	{
		public static HookDelegates.Entity* CurrentEntity;

		public ExampleForm()
		{
			InitializeComponent();
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			
		}

		private void ExampleForm_Load(object sender, EventArgs e)
		{
			CurrentEntity = (HookDelegates.Entity*)Main.LocalPlayerBase;
		}

		private void Button2_Click(object sender, EventArgs e)
		{
			if (CurrentEntity == null)
			{
				MessageBox.Show("CurrentEntity pointer is null");
				return;
			}
			CurrentEntity->ptrEntityInfoStruct->ResetMovementSpeedMultiplier();
		}

		private void Button1_Click_1(object sender, EventArgs e)
		{
			if (CurrentEntity == null)
			{
				MessageBox.Show("CurrentEntity pointer is null");
				return;
			}
			CurrentEntity->ptrEntityInfoStruct->MovementSpeedMultiplier = (float) numericUpDown1.Value;
		}
	}
}
