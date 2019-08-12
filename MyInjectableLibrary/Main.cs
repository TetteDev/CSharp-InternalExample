using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using RGiesecke.DllExport;
using System.Runtime.InteropServices;
using System.Threading;

namespace MyInjectableLibrary
{
    public static class Main
    {
	    public static readonly Process ThisProcess = Process.GetCurrentProcess();

	    public static Thread WatcherThread;
		
	    [DllExport("DllMain", CallingConvention.Cdecl)] // Mark your members with this attribute to export them; if you don't - they won't get exported!
	    public static void EntryPoint() // Note, member name does not have to match export name ("DllMain" in this case).
	    {
		    if (!DebugConsole.InitiateDebugConsole())
		    {
			    File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "hello_from_" + ThisProcess.ProcessName + ".txt"), $"Failed allocating console!");
				return;
		    }

		    try
		    {
			    ProcessModule pm = ThisProcess.FindProcessModule("panoramauiclient.dll");
			    if (pm != null)
			    {

			    }
		    }
		    catch (Exception e)
		    {
				Console.WriteLine(e.Message);
		    }
		    
		}

	    
    }
}
