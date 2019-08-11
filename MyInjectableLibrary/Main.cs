using System;
using System.Diagnostics;
using System.IO;
using RGiesecke.DllExport;
using System.Runtime.InteropServices;

namespace MyInjectableLibrary
{
    public static class Main
    {
	    public static readonly Process ThisProcess = Process.GetCurrentProcess();
		
	    [DllExport("DllMain", CallingConvention.Cdecl)] // Mark your members with this attribute to export them; if you don't - they won't get exported!
	    public static void EntryPoint() // Note, member name does not have to match export name ("DllMain" in this case).
	    {
		    if (!DebugConsole.InitiateDebugConsole())
		    {
			    File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "hello_from_" + ThisProcess.ProcessName + ".txt"), $"Failed allocating console!");
				return;
		    }

		    Console.Title = $"{ThisProcess.MainWindowTitle} - Debugging Console";

		    byte[] test = Memory.Reader.UnsafeReadBytes(ThisProcess.MainModule.BaseAddress, 10);
		    if (test == null || test.Length < 1)
		    {
			    Console.WriteLine("Failed reading bytes!");
			}
		    else
		    {
				Console.WriteLine($"Read 10 bytes from location 0x{ThisProcess.MainModule.BaseAddress.ToInt32():X8}:");
			    Console.WriteLine($"	{BitConverter.ToString(test).Replace("-", " ")}");
		    }
	    }
	}
}
