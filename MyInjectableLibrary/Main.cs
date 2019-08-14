using System;
using System.Diagnostics;
using System.IO;
using RGiesecke.DllExport;
using System.Runtime.InteropServices;

namespace MyInjectableLibrary
{
    public static class Main
    {
	    public static Process ThisProcess = Process.GetCurrentProcess();

	    [DllExport("DllMain", CallingConvention.Cdecl)] // Mark your members with this attribute to export them; if you don't - they won't get exported!
	    public static void EntryPoint() // Note, member name does not have to match export name ("DllMain" in this case).
	    {
			ThisProcess = Process.GetCurrentProcess();
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			if (!DebugConsole.InitiateDebugConsole())
		    {
			    File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "hello_from_" + ThisProcess.ProcessName + ".txt"), $"Failed allocating console!");
				return;
		    }

			// Do stuff here
	    }


		static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			if (e.ExceptionObject == null) return;
			HelperMethods.PrintExceptionData(e.ExceptionObject, true);
			if (Debugger.IsAttached) Debugger.Break();
		}
    }
}
