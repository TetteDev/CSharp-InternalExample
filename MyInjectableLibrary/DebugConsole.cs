using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MyInjectableLibrary
{
	public class DebugConsole
	{
		private static readonly object _lock = new();
		private static bool _canLog = false;

		public static bool InitiateDebugConsole()
		{
			if (PInvoke.AllocConsole())
			{
				//https://developercommunity.visualstudio.com/content/problem/12166/console-output-is-gone-in-vs2017-works-fine-when-d.html
				// Console.OpenStandardOutput eventually calls into GetStdHandle. As per MSDN documentation of GetStdHandle: http://msdn.microsoft.com/en-us/library/windows/desktop/ms683231(v=vs.85).aspx will return the redirected handle and not the allocated console:
				// "The standard handles of a process may be redirected by a call to  SetStdHandle, in which case  GetStdHandle returns the redirected handle. If the standard handles have been redirected, you can specify the CONIN$ value in a call to the CreateFile function to get a handle to a console's input buffer. Similarly, you can specify the CONOUT$ value to get a handle to a console's active screen buffer."
				// Get the handle to CONOUT$.    
				var stdOutHandle = PInvoke.CreateFile("CONOUT$", PInvoke.DesiredAccess.GenericRead | PInvoke.DesiredAccess.GenericWrite, FileShare.ReadWrite, 0, FileMode.Open, FileAttributes.Normal, 0);
				var stdInHandle = PInvoke.CreateFile("CONIN$", PInvoke.DesiredAccess.GenericRead | PInvoke.DesiredAccess.GenericWrite, FileShare.ReadWrite, 0, FileMode.Open, FileAttributes.Normal, 0);

				if (stdOutHandle == new IntPtr(-1))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error());
				}

				if (stdInHandle == new IntPtr(-1))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error());
				}


				if (!PInvoke.SetStdHandle(PInvoke.StdHandle.Output, stdOutHandle))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error());
				}

				if (!PInvoke.SetStdHandle(PInvoke.StdHandle.Input, stdInHandle))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error());
				}

				var standardOutput = new StreamWriter(Console.OpenStandardOutput()) {AutoFlush = true};
				var standardInput = new StreamReader(Console.OpenStandardInput());

				Console.SetIn(standardInput);
				Console.SetOut(standardOutput);
				_canLog = true;
				return true;
			}
			return false;
		}
		

		public enum LogType
		{
			Blank = ConsoleColor.White,
			Message = ConsoleColor.Green,
			Warning = ConsoleColor.Yellow,
			Error = ConsoleColor.Red,
#if DEBUG
			Debug = ConsoleColor.Magenta,
#endif
		}

#if DEBUG
		public static void DebugLog(string message, LogType messageType = LogType.Debug)
		{
			if (string.IsNullOrEmpty(message) || !_canLog) return;

			lock (_lock)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				string msg = $"[{DateTime.Now.ToLongTimeString()}]";
				Console.ForegroundColor = (ConsoleColor)messageType;
				msg += $"[{messageType.ToString().ToUpper()}] ";
				Console.ResetColor();
				msg += message;

				Console.WriteLine(msg);
				Debug.WriteLine(msg);
			}
		}
#endif

		public static void Log(string message, LogType messageType = LogType.Message)
		{
			if (string.IsNullOrEmpty(message) || !_canLog) return;

			lock (_lock)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				string msg = $"[{DateTime.Now.ToLongTimeString()}]";
				Console.ForegroundColor = (ConsoleColor)messageType;
				msg += $"[{messageType.ToString().ToUpper()}] ";
				Console.ResetColor();
				msg += message;

				Console.WriteLine(msg);
			}
		}

	}
}
