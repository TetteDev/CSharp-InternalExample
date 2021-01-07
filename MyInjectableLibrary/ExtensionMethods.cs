using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MyInjectableLibrary
{
	public static class IntExtensions
	{
		
	}

	public static class ByteArrayExtensions
	{
		public static void Execute(this byte[] obj)
		{
			if (obj == null || obj.Length < 1)
				return;

			unsafe
			{
				fixed (byte* ptr = obj)
				{
					var memoryAddress = (IntPtr) ptr;

					if (!PInvoke.VirtualProtect(memoryAddress, obj.Length,
						PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out var oldProtect))
						throw new Win32Exception();

					IntPtr thread = PInvoke.CreateThread(IntPtr.Zero, 0, memoryAddress, IntPtr.Zero, 0, out _);
					if (thread == IntPtr.Zero)
						throw new Win32Exception();

					PInvoke.WaitForSingleObject(thread, 10000);
					PInvoke.CloseHandle(thread);
				}
			}
		}
	}

	public static class DelegateExtensions
	{
		public static IntPtr GetFunctionPointer(this Delegate obj)
		{
			return Marshal.GetFunctionPointerForDelegate(obj);
		}
	}
}
