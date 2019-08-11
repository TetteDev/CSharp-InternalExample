using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MyInjectableLibrary
{
	public class PInvoke
	{

		[DllImport("kernel32.dll")]
		public static extern bool AllocConsole();

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr CreateFile(string lpFileName
			, [MarshalAs(UnmanagedType.U4)] DesiredAccess dwDesiredAccess
			, [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode
			, uint lpSecurityAttributes
			, [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition
			, [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes
			, uint hTemplateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool SetStdHandle(StdHandle nStdHandle, IntPtr hHandle);

		public enum StdHandle : int
		{
			Input = -10,
			Output = -11,
			Error = -12
		}

		[Flags]
		public enum DesiredAccess : uint
		{
			GenericRead = 0x80000000,
			GenericWrite = 0x40000000,
			GenericExecute = 0x20000000,
			GenericAll = 0x10000000
		}
	}
}
