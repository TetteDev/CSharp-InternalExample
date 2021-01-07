using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using static MyInjectableLibrary.PInvoke;
using static MyInjectableLibrary.MemoryLibrary;
using static MyInjectableLibrary.DebugConsole;

namespace MyInjectableLibrary
{
    public static unsafe class Main
    {
	    public static ExampleForm DebugForm; 
	    public static Thread DebugFormThread; 

		private static bool _first = false;

		[DllExport("DllMain", CallingConvention.Cdecl)]
	    public static unsafe void EntryPoint()
	    {
			#region Setup
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
			
			UpdateProcessInformation();

			if (!InitiateDebugConsole())
				MessageBox.Show("Failed initiating the debugging console, please restart the program as admin!", 
					"Debugging Console Exception", 
					MessageBoxButtons.OK, 
					MessageBoxIcon.Error);
			else
			{
				Console.CancelKeyPress += Console_CancelKeyPress;
			}

			if (!PatchEtw())
				DebugLog("PatchEtw returned false", LogType.Warning);
			#endregion

			// Optional
			InitiateDebugForm();


			Console.WriteLine("Hello!");
			Console.ReadLine();
	    }

		
		public static void InitiateDebugForm()
	    {
			if (DebugForm != null) return;

			new Thread(() =>
			{
				if (DebugForm != null) return;
				DebugForm = new ExampleForm();
				DebugForm.ShowDialog();
			}).Start();
			
	    }
		static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			HelperMethods.PrintExceptionData(e?.ExceptionObject, writeToFile: true);
			if (!Debugger.IsAttached) return;

			Exception obj = (Exception)e?.ExceptionObject;
			if (obj != null)
				SetLastError((uint)obj.HResult);

			Debugger.Break();
		}
		static void CurrentDomain_ProcessExit(object sender, EventArgs e)
		{

		}
		static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			if (!_first)
			{
				e.Cancel = true;
				_first = true;
				return;
			}

			if (!(MessageBox.Show("Are you sure you want to close the program?", "Please confirm!", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes))
				e.Cancel = true;
		}
	}
}
