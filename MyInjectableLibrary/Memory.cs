using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using static MyInjectableLibrary.Main;

namespace MyInjectableLibrary
{
	public class Memory
	{
		public class Reader
		{
			#region Unsafe Methods
			public static unsafe byte[] UnsafeReadBytes(IntPtr location, int numBytes)
			{
				if (ThisProcess.Handle == IntPtr.Zero) throw new InvalidOperationException("Host Process Handle was IntPtr.Zero");
				if (location == IntPtr.Zero || numBytes < 1) return new byte[] { };

				var ret = new byte[numBytes];
				var ptr = (byte*)location;
				for (int i = 0; i < numBytes; i++)
				{
					ret[i] = ptr[i];
				}
				return ret;
			}

			[HandleProcessCorruptedStateExceptions]
			public static unsafe T UnsafeRead<T>(IntPtr address)
			{
				try
				{
					if (address == IntPtr.Zero)
					{
						throw new InvalidOperationException("Cannot retrieve a value at address 0");
					}

					object ret;
					switch (SizeCache<T>.TypeCode)
					{
						case TypeCode.Object:

							if (SizeCache<T>.IsIntPtr)
							{
								return (T)(object)*(IntPtr*)address;
							}

							// If the type doesn't require an explicit Marshal call, then ignore it and memcpy the fuckin thing.
							if (!SizeCache<T>.TypeRequiresMarshal)
							{
								T o = default(T);
								void* ptr = SizeCache<T>.GetUnsafePtr(ref o);

								PInvoke.MoveMemory(ptr, (void*)address, SizeCache<T>.Size);

								return o;
							}

							// All System.Object's require marshaling!
							ret = Marshal.PtrToStructure(address, typeof(T));
							break;
						case TypeCode.Boolean:
							ret = *(byte*)address != 0;
							break;
						case TypeCode.Char:
							ret = *(char*)address;
							break;
						case TypeCode.SByte:
							ret = *(sbyte*)address;
							break;
						case TypeCode.Byte:
							ret = *(byte*)address;
							break;
						case TypeCode.Int16:
							ret = *(short*)address;
							break;
						case TypeCode.UInt16:
							ret = *(ushort*)address;
							break;
						case TypeCode.Int32:
							ret = *(int*)address;
							break;
						case TypeCode.UInt32:
							ret = *(uint*)address;
							break;
						case TypeCode.Int64:
							ret = *(long*)address;
							break;
						case TypeCode.UInt64:
							ret = *(ulong*)address;
							break;
						case TypeCode.Single:
							ret = *(float*)address;
							break;
						case TypeCode.Double:
							ret = *(double*)address;
							break;
						case TypeCode.Decimal:
							// Probably safe to remove this. I'm unaware of anything that actually uses "decimal" that would require memory reading...
							ret = *(decimal*)address;
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
					return (T)ret;
				}
				catch (AccessViolationException ex)
				{
					Trace.WriteLine("Access Violation on " + address + " with type " + typeof(T).Name);
					return default(T);
				}
			}

			public static T[] UnsafeReadArray<T>(IntPtr address, int count) where T : struct
			{
				int size = SizeCache<T>.Size;
				var ret = new T[count];
				for (int i = 0; i < count; i++)
				{
					ret[i] = UnsafeRead<T>(address + (i * size));
				}
				return ret;
			}
			#endregion
		}

		public class Writer
		{
			#region Unsafe Methods

			public static unsafe void UnsafeWriteBytes(IntPtr location, byte[] buffer)
			{
				var ptr = (byte*)location;
				for (int i = 0; i < buffer.Length; i++)
				{
					ptr[i] = buffer[i];
				}
			}

			
			public static void UnsafeWrite<T>(IntPtr address, T value)
			{
				PInvoke.VirtualProtect(address, SizeCache<T>.Size, PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out PInvoke.MemoryProtectionFlags old);
				Marshal.StructureToPtr(value, address, false);
				PInvoke.VirtualProtect(address, SizeCache<T>.Size, old, out old);
			}
			

			public void WriteArray<T>(IntPtr address, T[] array) where T : struct
			{
				int size = SizeCache<T>.Size;
				for (int i = 0; i < array.Length; i++)
				{
					T val = array[i];
					UnsafeWrite(address + (i * size), val);
				}
			}
			#endregion

		}
	}
}
