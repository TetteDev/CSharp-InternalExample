using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
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

			public static string UnsafeReadString(IntPtr address, Encoding encoding, int maxLength = 256)
			{
				var data = UnsafeReadBytes(address, maxLength);
				var text = new string(encoding.GetChars(data));
				if (text.Contains("\0"))
					text = text.Substring(0, text.IndexOf('\0'));
				return text;
			}

			public T UnsafeReadMultilevelPointer<T>(IntPtr address, params IntPtr[] offsets) where T : struct
			{
				if (offsets.Length == 0)
				{
					throw new InvalidOperationException("Cannot read a value from unspecified addresses.");
				}

				var temp = UnsafeRead<IntPtr>(address);

				for (int i = 0; i < offsets.Length - 1; i++)
				{
					temp = UnsafeRead<IntPtr>(temp + (int)offsets[i]);
				}
				return UnsafeRead<T>(temp + (int)offsets[offsets.Length - 1]);
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

			public static void UnsafeWrite<T>(IntPtr address, T value, bool virtualProtectNeeded = true)
			{
				if (virtualProtectNeeded)
				{
					PInvoke.VirtualProtect(address, SizeCache<T>.Size, PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out PInvoke.MemoryProtectionFlags old);
					Marshal.StructureToPtr(value, address, false);
					PInvoke.VirtualProtect(address, SizeCache<T>.Size, old, out old);
				}
				else
				{
					Marshal.StructureToPtr(value, address, false);
				}
			}

			public static void UnsafeWriteString(IntPtr address, string str, Encoding encoding)
			{
				byte[] bytes = encoding.GetBytes(str);
				UnsafeWriteBytes(address, bytes);
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

		public class Pattern
		{
			public static unsafe IntPtr FindPattern(IntPtr address, int bufferSize, string pattern, bool resultAbsolute = true)
			{
				if (bufferSize < 1) return IntPtr.Zero;
				byte[] buffer = Reader.UnsafeReadBytes(address, bufferSize);

				if (buffer == null || buffer.Length < 1) return IntPtr.Zero;

				var tmpSplitPattern = pattern.TrimStart(' ').TrimEnd(' ').Split(' ');

				var tmpPattern = new byte[tmpSplitPattern.Length];
				var tmpMask = new byte[tmpSplitPattern.Length];

				for (var i = 0; i < tmpSplitPattern.Length; i++)
				{
					var ba = tmpSplitPattern[i];

					if (ba == "??" || ba.Length == 1 && ba == "?")
					{
						tmpMask[i] = 0x00;
						tmpSplitPattern[i] = "0x00";
					}
					else if (char.IsLetterOrDigit(ba[0]) && ba[1] == '?')
					{
						tmpMask[i] = 0xF0;
						tmpSplitPattern[i] = ba[0] + "0";
					}
					else if (char.IsLetterOrDigit(ba[1]) && ba[0] == '?')
					{
						tmpMask[i] = 0x0F;
						tmpSplitPattern[i] = "0" + ba[1];
					}
					else
					{
						tmpMask[i] = 0xFF;
					}
				}

				for (var i = 0; i < tmpSplitPattern.Length; i++)
					tmpPattern[i] = (byte)(Convert.ToByte(tmpSplitPattern[i], 16) & tmpMask[i]);

				if (tmpMask.Length != tmpPattern.Length)
					throw new ArgumentException($"{nameof(pattern)}.Length != {nameof(tmpMask)}.Length");

				int result = 0 - tmpPattern.Length;
				fixed (byte* pPacketBuffer = buffer)
				{
					do
					{
						result = HelperMethods.FindPattern(pPacketBuffer, buffer.Length, tmpPattern, tmpMask, result + tmpPattern.Length);
						if (result >= 0)
							return resultAbsolute ? IntPtr.Add(address, result) : new IntPtr(result);
					} while (result != -1);
				}
				return IntPtr.Zero;
			}
			public static unsafe IntPtr FindPattern(ProcessModule processModule, string pattern, bool resultAbsolute = true)
			{
				byte[] buffer = Reader.UnsafeReadBytes(processModule.BaseAddress, processModule.ModuleMemorySize);
				if (buffer == null || buffer.Length < 1) return IntPtr.Zero;

				var tmpSplitPattern = pattern.TrimStart(' ').TrimEnd(' ').Split(' ');

				var tmpPattern = new byte[tmpSplitPattern.Length];
				var tmpMask = new byte[tmpSplitPattern.Length];

				for (var i = 0; i < tmpSplitPattern.Length; i++)
				{
					var ba = tmpSplitPattern[i];

					if (ba == "??" || ba.Length == 1 && ba == "?")
					{
						tmpMask[i] = 0x00;
						tmpSplitPattern[i] = "0x00";
					}
					else if (char.IsLetterOrDigit(ba[0]) && ba[1] == '?')
					{
						tmpMask[i] = 0xF0;
						tmpSplitPattern[i] = ba[0] + "0";
					}
					else if (char.IsLetterOrDigit(ba[1]) && ba[0] == '?')
					{
						tmpMask[i] = 0x0F;
						tmpSplitPattern[i] = "0" + ba[1];
					}
					else
					{
						tmpMask[i] = 0xFF;
					}
				}

				for (var i = 0; i < tmpSplitPattern.Length; i++)
					tmpPattern[i] = (byte)(Convert.ToByte(tmpSplitPattern[i], 16) & tmpMask[i]);

				if (tmpMask.Length != tmpPattern.Length)
					throw new ArgumentException($"{nameof(pattern)}.Length != {nameof(tmpMask)}.Length");

				int result = 0 - tmpPattern.Length;
				fixed (byte* pPacketBuffer = buffer)
				{
					do
					{
						result = HelperMethods.FindPattern(pPacketBuffer, buffer.Length, tmpPattern, tmpMask, result + tmpPattern.Length);
						if (result >= 0)
							return resultAbsolute ? IntPtr.Add(processModule.BaseAddress, result) : new IntPtr(result);
					} while (result != -1);
				}
				return IntPtr.Zero;
			}
		}

		public class Functions
		{
			
		}
	}
}
