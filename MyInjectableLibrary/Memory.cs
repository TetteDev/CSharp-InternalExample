using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MyInjectableLibrary
{
	public class Memory
	{
		public static IntPtr AllocateMemory(uint size, PInvoke.MemoryProtectionFlags memoryProtection = PInvoke.MemoryProtectionFlags.ExecuteReadWrite) =>
			size < 1 ? IntPtr.Zero : PInvoke.VirtualAlloc(IntPtr.Zero, new UIntPtr(size), PInvoke.AllocationTypeFlags.Commit, PInvoke.MemoryProtectionFlags.ExecuteReadWrite);

		public static bool FreeMemory(IntPtr baseAddress, uint optionalSize = 0) => baseAddress != IntPtr.Zero && PInvoke.VirtualFree(baseAddress, optionalSize, PInvoke.FreeType.Release);

		public class Reader
		{
			#region Unsafe Methods
			public static unsafe byte[] UnsafeReadBytes(IntPtr location, uint numBytes)
			{
				byte[] buff = new byte[numBytes];

				fixed (void* bufferPtr = buff)
				{
					Unsafe.CopyBlockUnaligned(bufferPtr, (void*)location, numBytes);
					return buff;
				}
			}

			public static unsafe T UnsafeRead<T>(IntPtr address, bool isRelative = false)
			{
				bool requiresMarshal = SizeCache<T>.TypeRequiresMarshal;
				var size = requiresMarshal ? SizeCache<T>.Size : Unsafe.SizeOf<T>();

				var buffer = UnsafeReadBytes(address, (uint)size);
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
				var data = UnsafeReadBytes(address, (uint)maxLength);
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
			[Obsolete("Considering getting rid of this")]
			private static unsafe void UnsafeWriteBytesOldest(IntPtr location, byte[] buffer)
			{
				var ptr = (byte*)location;
				for (int i = 0; i < buffer.Length; i++)
				{
					ptr[i] = buffer[i];
				}
			}

			public static unsafe void UnsafeWriteBytesNoErrors(IntPtr location, byte[] buffer)
			{
				if (location == IntPtr.Zero) return;
				if (buffer == null || buffer.Length < 1) return;

				var ptr = (void*)location;
				fixed (void* pBuff = buffer)
				{
					Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
				}
			}

			public static unsafe bool UnsafeWriteBytes(IntPtr location, byte[] buffer, bool forceVirtualProtect = false)
			{
				if (location == IntPtr.Zero || buffer == null || buffer.Length < 1) return false;

				PInvoke.MEMORY_BASIC_INFORMATION lpBuffer = new PInvoke.MEMORY_BASIC_INFORMATION();
				int virtualQueryResult = -1;
				if (forceVirtualProtect)
				{
					virtualQueryResult = PInvoke.VirtualQuery(location, out lpBuffer, 0x10000);
					if (virtualQueryResult == 0)
					{
						Console.WriteLine($"VirtualQuery returned zero for region 0x{location.ToInt32():X8}");
						return false;
					}
				}

				if (forceVirtualProtect && virtualQueryResult != 0)
				{
					// VirtualQuery is successfull

					// Check if region has any write permission
					if (lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ReadWrite) ||
						lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.WriteCopy) ||
						lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.WriteCombine) ||
						lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteReadWrite) ||
						lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteWriteCopy))
					{
						// We can write
						void* ptr = (void*)location;

						fixed (void* pBuff = buffer)
						{
							Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
						}

						return true;
					}
					else
					{
						if (lpBuffer.AllocationProtect.HasFlag(PInvoke.AllocationTypeFlags.Commit))
						{
							// Page is Committed but doesnt have RWX access
							// Go ahead and change protection temporarily
							bool virtualProtectResult = PInvoke.VirtualProtect(location, (int)lpBuffer.RegionSize, PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out var oldProtection);
							if (!virtualProtectResult) return false; // VirtualProtect failed making committed page RWX

							void* ptr = (void*)location;
							fixed (void* pBuff = buffer)
							{
								Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
							}
							PInvoke.VirtualProtect(location, 0x10000, oldProtection, out var discard);
							return true;
						}
					}
				}
				else
				{
					void* ptr = (void*)location;
					fixed (void* pBuff = buffer)
					{
						Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
					}
					return true;
				}
				return false;
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
			public static unsafe IntPtr FindPatternSingle(IntPtr address, int bufferSize, string pattern, bool resultAbsolute = true)
			{
				if (bufferSize < 1) return IntPtr.Zero;
				byte[] buffer = Reader.UnsafeReadBytes(address, (uint)bufferSize);

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
			public static unsafe IntPtr FindPatternSingle(ProcessModule processModule, string pattern, bool resultAbsolute = true)
			{
				if (processModule == null) return IntPtr.Zero;
				byte[] buffer = Reader.UnsafeReadBytes(processModule.BaseAddress, (uint)processModule.ModuleMemorySize);
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

			public static unsafe List<IntPtr> FindPattern(ProcessModule processModule, string pattern, bool resultAbsolute = true)
			{
				if (processModule == null || pattern == string.Empty) return new List<IntPtr>();
				byte[] buffer = Reader.UnsafeReadBytes(processModule.BaseAddress, (uint)processModule.ModuleMemorySize);
				if (buffer == null || buffer.Length < 1) return new List<IntPtr>();

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


				List<IntPtr> results = new List<IntPtr>();
				int result = 0 - tmpPattern.Length;
				unsafe
				{
					fixed (byte* pPacketBuffer = buffer)
					{
						do
						{
							result = HelperMethods.FindPattern(pPacketBuffer, buffer.Length, tmpPattern, tmpMask, result + tmpPattern.Length);
							if (result >= 0)
								results.Add(resultAbsolute ? IntPtr.Add(processModule.BaseAddress, result) : new IntPtr(result));
						} while (result != -1);
					}
				}

				return results;
			}
			public static unsafe List<IntPtr> FindPattern(IntPtr address, int bufferSize, string pattern, bool resultAbsolute = true)
			{
				if (address == IntPtr.Zero || pattern == string.Empty || bufferSize < 1) return new List<IntPtr>();
				byte[] buffer = Reader.UnsafeReadBytes(address, (uint)bufferSize);
				if (buffer == null || buffer.Length < 1) return new List<IntPtr>();

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


				List<IntPtr> results = new List<IntPtr>();
				int result = 0 - tmpPattern.Length;
				unsafe
				{
					fixed (byte* pPacketBuffer = buffer)
					{
						do
						{
							result = HelperMethods.FindPattern(pPacketBuffer, buffer.Length, tmpPattern, tmpMask, result + tmpPattern.Length);
							if (result >= 0)
								results.Add(resultAbsolute ? IntPtr.Add(address, result) : new IntPtr(result));
						} while (result != -1);
					}
				}

				return results;
			}
		}

		public class Functions
		{
			public static T GetFunction<T>(IntPtr address) => address == IntPtr.Zero ? throw new InvalidOperationException($"Cannot get function Delegate from base address zero") : Marshal.GetDelegateForFunctionPointer<T>(address);
		}

		public class Hooks
		{
			

		}

		public class Detours
		{
			public class Detour
			{
				private IntPtr _location;
				private uint _codeCaveSize = 0;
				private bool _isActive = false;

				private byte[] _originalBytes;
				private IntPtr _activeDetourCodeCaveLocation = IntPtr.Zero;
				private uint _writeLocation = 0;

				public Detour(IntPtr location, uint caveSize = 0x10000)
				{
					if (location == IntPtr.Zero) throw new InvalidOperationException("");
					if (caveSize < 1) caveSize = 0x10000;
					_location = location;
					_codeCaveSize = caveSize;
				}

				public bool SetState(bool active = false)
				{
					if (!active && !_isActive ||
					    active && _isActive) return true;

					return active ? Implement() : UnImplement();
				}

				private bool Implement()
				{
					if (_isActive) return true;
					IntPtr alloc = PInvoke.VirtualAlloc(IntPtr.Zero, new UIntPtr(_codeCaveSize), PInvoke.AllocationTypeFlags.Commit | PInvoke.AllocationTypeFlags.Reserve, PInvoke.MemoryProtectionFlags.ExecuteReadWrite);
					if (alloc == IntPtr.Zero) return false;

					_writeLocation = (uint) alloc;

					List<byte> jmpOut = new List<byte>() { 0xE9 };
					jmpOut.AddRange(BitConverter.GetBytes(_location.ToInt32() + (alloc.ToInt32() + 6) + 6));

					List<byte> jmpIn = new List<byte>() { 0xE9 };
					jmpIn.AddRange(BitConverter.GetBytes(alloc.ToInt32() - _location.ToInt32()));

					Writer.UnsafeWriteBytes(alloc, new byte[] {0x66, 0x0F, 0x1F, 0x44, 0x00, 0x00});
					_writeLocation += 6;
					Writer.UnsafeWriteBytes(new IntPtr(_writeLocation), jmpOut.ToArray());

					_originalBytes = Reader.UnsafeReadBytes(_location, 6);
					Writer.UnsafeWriteBytes(_location, jmpIn.ToArray(), true);

					_activeDetourCodeCaveLocation = alloc;
					_isActive = true;
					return true;
				}

				private bool UnImplement()
				{
					if (!_isActive) return true;
					// Unimplement it

					Memory.Writer.UnsafeWriteBytes(_location, _originalBytes, true);

					if (_activeDetourCodeCaveLocation != IntPtr.Zero)
					{
						bool freeResult = PInvoke.VirtualFree(_activeDetourCodeCaveLocation, 0, PInvoke.FreeType.Release);
						_activeDetourCodeCaveLocation = IntPtr.Zero;
					}
					_isActive = false;
					return true;
				}

				private void WriteToCave(byte[] buffer)
				{

				}
			}
		}
	}
}
