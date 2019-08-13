using System;
using System.Windows.Forms;

namespace MyInjectableLibrary
{
	public unsafe partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			MessageBox.Show("Hello!");
		}

		private void Form1_Load(object sender, EventArgs e)
		{

		}
	}
}
