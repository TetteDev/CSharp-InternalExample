using System;
using System.Runtime.InteropServices;
using static MyInjectableLibrary.Main;

namespace MyInjectableLibrary
{
	public class Memory
	{
		public class Reader
		{
			public static byte[] ReadBytes(IntPtr location, int numBytes)
			{
				if (ThisProcess.Handle == IntPtr.Zero) throw new InvalidOperationException("Host Process Handle was IntPtr.Zero");
				if (location == IntPtr.Zero || numBytes < 1) return new byte[] {};

				byte[] returnedBytes = new byte[numBytes];
				Marshal.Copy(location, returnedBytes, 0, numBytes);
				return returnedBytes;
			}

			public static unsafe byte[] UnsafeReadBytes(IntPtr location, int numBytes)
			{
				if (ThisProcess.Handle == IntPtr.Zero) throw new InvalidOperationException("Host Process Handle was IntPtr.Zero");
				if (location == IntPtr.Zero || numBytes < 1) return new byte[] { };
				byte* ptr = (byte*)location.ToPointer();

				byte[] buff = new byte[numBytes];
				for (int n = 0; n < numBytes; n++)
				{
					buff[n] = ptr[n];
				}
				return buff;
			}
		}

		public class Writer
		{
			// WriteBytes
			// Write<T>
			// etc
		}
	}
}
