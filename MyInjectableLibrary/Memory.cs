using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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

			public static unsafe T UnsafeRead<T>(IntPtr address, bool isRelative = false)
			{
				bool requiresMarshal = SizeCache<T>.TypeRequiresMarshal;
				var size = requiresMarshal ? SizeCache<T>.Size : Unsafe.SizeOf<T>();

				var buffer = UnsafeReadBytes(address, size);
				fixed (byte* b = buffer)
				{
					return requiresMarshal ? Marshal.PtrToStructure<T>(new IntPtr(b)) : Unsafe.Read<T>(b);
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
					return UnsafeRead<T>(address, false);

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

			public static unsafe void UnsafeWrite<T>(IntPtr address, T value, bool virtualProtectNeeded = true)
			{
				bool requiresMarshal = SizeCache<T>.TypeRequiresMarshal;
				if (requiresMarshal)
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
				else
				{
					if (virtualProtectNeeded)
					{
						PInvoke.VirtualProtect(address, SizeCache<T>.Size, PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out PInvoke.MemoryProtectionFlags old);
						Unsafe.Write((void*)address, value);
						PInvoke.VirtualProtect(address, SizeCache<T>.Size, old, out old);
					}
					else
					{
						Unsafe.Write((void*)address, value);
					}
				}
			}

			public static void UnsafeWriteString(IntPtr address, string str, Encoding encoding)
			{
				byte[] bytes = encoding.GetBytes(str);
				UnsafeWriteBytes(address, bytes);
			}

			public void UnsafeWriteArray<T>(IntPtr address, T[] array) where T : struct
			{
				int size = SizeCache<T>.Size;
				for (int i = 0; i < array.Length; i++)
				{
					T val = array[i];
					UnsafeWrite(address + (i * size), val);
				}
			}

			public void UnsafeWriteMultiLevelPointer<T>(IntPtr address, T value, params IntPtr[] offsets)
			{
				if (offsets.Length == 0)
					UnsafeWrite(address, value);

				// 
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
				if (processModule == null) return IntPtr.Zero;
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
