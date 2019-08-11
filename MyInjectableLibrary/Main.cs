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
			    Console.Title = $"{ThisProcess.MainWindowTitle} - Debugging Console";
			    byte t = UnsafeRead<byte>(new IntPtr(0x0040000));
			    Console.WriteLine($"Result: {(int) t:X}");
		    }
		    catch (Exception e)
		    {
				Console.WriteLine(e.Message);
		    }
		    
		}

	    public static unsafe T UnsafeRead<T>(IntPtr address, bool isRelative = false)
	    {
		    bool requiresMarshal = SizeCache<T>.TypeRequiresMarshal;
		    var size = requiresMarshal ? SizeCache<T>.Size : Unsafe.SizeOf<T>();

		    var buffer = Memory.Reader.UnsafeReadBytes(address, size);
		    fixed (byte* b = buffer)
		    {
			    return requiresMarshal ? Marshal.PtrToStructure<T>(new IntPtr(b)) : Unsafe.Read<T>(b);
		    }
		}
    }
}
