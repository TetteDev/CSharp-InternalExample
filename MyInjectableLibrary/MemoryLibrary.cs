using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static MyInjectableLibrary.PInvoke;

namespace MyInjectableLibrary
{
	public static class IntPtrExtensions
	{
		public static bool SetProtection(this IntPtr @this, int size, MemoryProtectionFlags _newProtection, out MemoryProtectionFlags _oldProtection)
		{
			if (@this == IntPtr.Zero)
			{
				_oldProtection = default;
				return false;
			}

			return MemoryLibrary.Protection.SetPageProtection(@this, size, _newProtection, out _oldProtection);
		}
	}

	public unsafe class MemoryLibrary
	{
		public const string MODULE_NAME = "MyInjectableLibrary.dll";
		public static Process HostProcess = Process.GetCurrentProcess();
		public static IntPtr ExternalProcessHandle;
		public static Memory.Mem Mem;
		
		public static ProcessModule OurModule = HostProcess.FindProcessModule(MODULE_NAME);

		public static void UpdateProcessInformation()
		{
			HostProcess = Process.GetCurrentProcess();
			OurModule = HostProcess.FindProcessModule(MODULE_NAME);
			Mem = new Memory.Mem();
			bool op = Mem.OpenProcess(HostProcess.Id);
			if (op) ExternalProcessHandle = Mem.pHandle;
		}

		public static T* Alloc<T>(bool fillZero = true) where T : unmanaged {
			uint size = (uint)Marshal.SizeOf<T>();
			var ptr = (T*)Marshal.AllocHGlobal((int)size);

			if (fillZero) Unsafe.InitBlock(ptr, 0x0, size);

			return ptr;
		}
		public static void Free<T>(T* freeObject) where T : unmanaged => Marshal.FreeHGlobal((IntPtr)freeObject);

		public static IntPtr AllocateMemory(uint size, MemoryProtectionFlags memoryProtection = MemoryProtectionFlags.ExecuteReadWrite) =>
			size < 1 ? IntPtr.Zero : VirtualAlloc(IntPtr.Zero, new UIntPtr(size), AllocationTypeFlags.Commit | AllocationTypeFlags.Reserve, MemoryProtectionFlags.ExecuteReadWrite);
		public static bool FreeMemory(IntPtr baseAddress, uint optionalSize = 0)
			=> baseAddress != IntPtr.Zero && VirtualFree(baseAddress, optionalSize, FreeType.Release);

		public static IntPtr AllocateMemoryManaged(int size)
			=> size < 1 ? IntPtr.Zero : Marshal.AllocHGlobal(size);
		public static void FreeMemoryManaged(IntPtr address)
		{
			if (address == IntPtr.Zero)
				return;
			Marshal.FreeHGlobal(address);
		}


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

			public static T UnsafeReadMultilevelPointer<T>(IntPtr address, params int[] offsets) where T : struct
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

				MEMORY_BASIC_INFORMATION lpBuffer = new MEMORY_BASIC_INFORMATION();
				int virtualQueryResult = -1;
				if (forceVirtualProtect)
				{
					virtualQueryResult = VirtualQuery(location, out lpBuffer, 0x10000);
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
					if (lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ReadWrite) ||
						lpBuffer.Protect.HasFlag(MemoryProtectionFlags.WriteCopy) ||
						lpBuffer.Protect.HasFlag(MemoryProtectionFlags.WriteCombine) ||
						lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ExecuteReadWrite) ||
						lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ExecuteWriteCopy))
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
						if (lpBuffer.AllocationProtect.HasFlag(AllocationTypeFlags.Commit))
						{
							// Page is Committed but doesnt have RWX access
							// Go ahead and change protection temporarily
							bool virtualProtectResul_RWX = VirtualProtect(location, (int)lpBuffer.RegionSize, MemoryProtectionFlags.ExecuteReadWrite, out var oldProtection);
							if (!virtualProtectResul_RWX)
							{
								// Check if we can set page protection to Execute/Write Copy
								bool virtualProtectResult_EWC = VirtualProtect(location, (int) lpBuffer.RegionSize, MemoryProtectionFlags.ExecuteWriteCopy, out oldProtection);
								if (!virtualProtectResult_EWC) return false;
							}

							void* ptr = (void*)location;
							fixed (void* pBuff = buffer)
							{
								Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
							}
							return Reader.UnsafeReadBytes(location, (uint)buffer.Length) == buffer &&
							       VirtualProtect(location, (int)lpBuffer.RegionSize, oldProtection, out var discard);
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
						VirtualProtect(address, SizeCache<T>.Size, MemoryProtectionFlags.ExecuteReadWrite, out MemoryProtectionFlags old);
						Marshal.StructureToPtr(value, address, false);
						VirtualProtect(address, SizeCache<T>.Size, old, out old);
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
						VirtualProtect(address, SizeCache<T>.Size, MemoryProtectionFlags.ExecuteReadWrite, out MemoryProtectionFlags old);
						Unsafe.Write((void*)address, value);
						VirtualProtect(address, SizeCache<T>.Size, old, out old);
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

			public static unsafe IntPtr CEFindPattern(string pattern, CERegion writableMemory = CERegion.YES, CERegion executableMemory = CERegion.DONT_CARE, uint startRange = 0x00000000, uint stopRange = 0xffffffff)
			{
				if (pattern == "") return IntPtr.Zero;
				if (writableMemory == CERegion.NO && executableMemory == CERegion.NO) return IntPtr.Zero;
				if (startRange == stopRange || stopRange > startRange) return IntPtr.Zero;


				List<MEMORY_BASIC_INFORMATION> regionsToScan = new List<MEMORY_BASIC_INFORMATION>();
				for (uint address = startRange; address < stopRange;)
				{
					var virtualQuery = VirtualQuery(new IntPtr(address), out MEMORY_BASIC_INFORMATION mbi, (uint) Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
					if (virtualQuery != 0)
					{
						if (mbi.State == MemoryState.MEM_COMMIT && !(mbi.Protect.HasFlag(MemoryProtectionFlags.Guard)))
						{
							if (Reader.UnsafeReadBytes(new IntPtr(address),1).Length != 1)
								continue;

							if (executableMemory == CERegion.NO && (mbi.Protect.HasFlag(MemoryProtectionFlags.ExecuteReadWrite) ||
							                                                mbi.Protect.HasFlag(MemoryProtectionFlags.Execute) ||
							                                                mbi.Protect.HasFlag(MemoryProtectionFlags.ExecuteRead) ||
							                                                mbi.Protect.HasFlag(MemoryProtectionFlags.ExecuteWriteCopy)))
							{
								address += (uint)mbi.RegionSize.ToInt32();
								continue;
							}

							if (writableMemory == CERegion.NO && (mbi.Protect.HasFlag(MemoryProtectionFlags.ReadWrite) ||
							                                                mbi.Protect.HasFlag(MemoryProtectionFlags.ExecuteReadWrite) ||
							                                                mbi.Protect.HasFlag(MemoryProtectionFlags.ExecuteWriteCopy)))
							{
								address += (uint)mbi.RegionSize.ToInt32();
								continue;
							}
							regionsToScan.Add(mbi);
						}
					}
					else
					{
						Console.WriteLine($"VirtualQuery on address 0x{address:X8} returned zero, incrementing address by 4096 (0x1000) and moving on");
						address += 0x1000;
					}

					address += (uint)mbi.RegionSize.ToInt32();
				}

				if (regionsToScan.Count < 1) return IntPtr.Zero;

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

				ConcurrentBag<IntPtr> results_r = new ConcurrentBag<IntPtr>();

				Parallel.ForEach(regionsToScan, (region) =>
				{
					byte[] buffer = Reader.UnsafeReadBytes(region.BaseAddress, (uint)region.RegionSize.ToInt32());
					int result = 0 - tmpPattern.Length;
					unsafe
					{
						fixed (byte* pPacketBuffer = buffer)
						{
							do
							{
								result = HelperMethods.FindPattern(pPacketBuffer, buffer.Length, tmpPattern, tmpMask, result + tmpPattern.Length);
								if (result >= 0)
									results_r.Add(IntPtr.Add(region.BaseAddress, result));
							} while (result != -1);
						}
					}
				});

				return results_r.Count > 0 ? results_r.ToList()[0] : IntPtr.Zero;
			}
		}

		public class Functions
		{
			public static T GetFunction<T>(IntPtr address) => address == IntPtr.Zero ? throw new InvalidOperationException($"Cannot get function Delegate from base address zero") : Marshal.GetDelegateForFunctionPointer<T>(address);

			public static uint ExecuteAssembly(byte[] asm)
			{
				if (asm == null || asm.Length < 1) return uint.MinValue;
				IntPtr alloc = IntPtr.Zero;
				try
				{
					alloc = AllocateMemory(asm.Length < 0x1000 ? (uint) 0x1000 : (uint) 0x10000);
					if (alloc == IntPtr.Zero) throw new Exception("failed allocating codecave");

					Writer.UnsafeWriteBytes(alloc, asm);

					IntPtr t = CreateThread(IntPtr.Zero, 0, alloc, IntPtr.Zero, 0, out IntPtr threadID);
					if (t == IntPtr.Zero) return 0;

					var result = WaitForSingleObject(t, 0xFFFFFFFF); // Wait forever?
					bool res = GetExitCodeThread(t, out uint resultPtr);
					if (!res) throw new Exception("failed get exit code");

					VirtualFree(alloc, 0, FreeType.Release);
					CloseHandle(t);
					return resultPtr;
				}
				catch
				{
					if (alloc != IntPtr.Zero)
						FreeMemory(alloc);
					return 0;
				}
			}
		}

		public class Injector
		{
			
		}

		public class Dumper
		{
			public static void DumpEntireProcess()
			{
				Console.WriteLine("DumpEntireProcess() has not been implemented yet");
			}

			public static bool DumpProcessModule(ProcessModule targetProcessModule, string saveDirectory = "default")
			{
				byte[] buff = MemoryLibrary.Reader.UnsafeReadBytes(targetProcessModule.BaseAddress, (uint)targetProcessModule.ModuleMemorySize);
				if (buff == null || buff.Length < 1) return false;

				if (saveDirectory != "default")
				{
					File.WriteAllBytes(Path.Combine(saveDirectory, $"{targetProcessModule.FileName}_dumped.bin"), buff);
					return true;
				}
				else
				{
					File.WriteAllBytes(Path.Combine(Environment.CurrentDirectory, $"{targetProcessModule.FileName}_dumped.bin"), buff);
					return true;
				}
			}
		}

		public class Detours
		{

			public static unsafe bool ReadBytesEx(IntPtr address, int count, out byte[] bytes, bool tryVirtualProtectIfNeeded = false)
			{
				if (address == IntPtr.Zero || count < 1)
				{
					bytes = null;
					return false;
				}

				if (!tryVirtualProtectIfNeeded)
				{
					var buff = new byte[count];

					fixed (void* bufferPtr = buff)
					{
						Unsafe.CopyBlockUnaligned(bufferPtr, (void*)address, (uint)count);

						bytes = buff;
						return true; // It might have failed here
					}
				}

				var withoutVirtualProtectSuccess = ReadBytesEx(address, count, out var success_bytes);
				if (withoutVirtualProtectSuccess)
				{
					Console.WriteLine($"ReadBytesEx on address 0x{address.ToInt32():X8} with tryVirtualProtectIfNeeded bool set to false successfully read {count} bytes!");
					bytes = success_bytes;
					return true;
				}

				var virtualQueryResult = VirtualQuery(address, out var lpBuffer, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
				if (virtualQueryResult == 0)
				{
					Console.WriteLine($"VirtualQuery on address 0x{address.ToInt32():X8} returned zero, trying to read anyways ...");
					var buff = new byte[count];

					fixed (void* bufferPtr = buff)
					{
						Unsafe.CopyBlockUnaligned(bufferPtr, (void*)address, (uint)count);

						bytes = buff;
						return true;
					}
				}

				Console.WriteLine($"Target Address: 0x{address.ToInt32():X8}\n" +
								  $"Requested amount of bytes to read: {count}\n\n" +
								  "Target Address Information:\n" +
								  $"	* Allocation Base: {lpBuffer.AllocationBase.ToInt32():X8}\n" +
								  $"	* Base Address: {lpBuffer.BaseAddress.ToInt32():X8}\n" +
								  $"	* Region Size: {lpBuffer.RegionSize.ToInt32()} Bytes\n" +
								  $"	* Region State: {lpBuffer.State.ToString()}\n" +
								  $"	* Region Type: {lpBuffer.Type.ToString()}\n" +
								  $"	* Region Protection Flags: {lpBuffer.Protect.ToString()}\n" +
								  $"	* Region Allocation Flags: {lpBuffer.AllocationProtect.ToString()}");

				if (lpBuffer.Equals(default(MEMORY_BASIC_INFORMATION)))
				{
					Console.WriteLine("Virtual Query returned zero (was successfull) but the MEMORY_BASIC_INFORMATION was null/default!");
					bytes = null;
					return false;
				}

				if ((lpBuffer.State == MemoryState.MEM_COMMIT ||
					 lpBuffer.State.HasFlag(MemoryState.MEM_COMMIT)) &&
					lpBuffer.Protect == MemoryProtectionFlags.ExecuteRead ||
					lpBuffer.Protect == MemoryProtectionFlags.ExecuteReadWrite ||
					lpBuffer.Protect == MemoryProtectionFlags.ReadWrite ||
					lpBuffer.Protect == MemoryProtectionFlags.ReadOnly ||

					lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ExecuteRead) ||
					lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ExecuteReadWrite) ||
					lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ReadWrite) ||
					lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ReadOnly))
				{
					var buff = new byte[count];
					fixed (void* bufferPtr = buff)
					{
						Unsafe.CopyBlockUnaligned(bufferPtr, (void*)address, (uint)count);

						bytes = buff;
						return true;
					}
				}

				// Region state was not MEM_COMMMIT or region protect flags didnt include any read permission whatsoever
				if (lpBuffer.State != MemoryState.MEM_COMMIT ||
					!lpBuffer.State.HasFlag(MemoryState.MEM_COMMIT))
				{
					Console.WriteLine("Region State was not MEM_COMMIT, or had flag MEM_COMMIT in it, returning false ...");
					bytes = null;
					return false;
				}

				if (lpBuffer.Protect == MemoryProtectionFlags.NoAccess ||
					lpBuffer.Protect.HasFlag(MemoryProtectionFlags.NoAccess))
				{
					Console.WriteLine("Region State was either set to NO_ACCESS or it had the NO_ACCESS flag in it, returning false ...");
					bytes = null;
					return false;
				}


				if (lpBuffer.Type != MemoryType.MEM_MAPPED &&
					!lpBuffer.Type.HasFlag(MemoryType.MEM_MAPPED))
				{
					// If type is not mapped and type does not contain MEM_MAPPED flag
					Console.WriteLine("Region Type is not MEM_MAPPED nor does the Region Type flags contain the MEM_MAPPED flag, returning false");
					bytes = null;
					return false;
				}

				if (!(lpBuffer.Protect == MemoryProtectionFlags.ExecuteRead ||
					  lpBuffer.Protect == MemoryProtectionFlags.ExecuteReadWrite ||
					  lpBuffer.Protect == MemoryProtectionFlags.ReadWrite ||
					  lpBuffer.Protect == MemoryProtectionFlags.ReadOnly ||

					  lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ExecuteRead) ||
					  lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ExecuteReadWrite) ||
					  lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ReadWrite) ||
					  lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ReadOnly)))
				{
					var virtualProtectRegionRWX = VirtualProtect(lpBuffer.AllocationBase, lpBuffer.RegionSize.ToInt32(), MemoryProtectionFlags.ExecuteReadWrite,
						out var _oldProtectionFlags);
					if (virtualProtectRegionRWX)
					{
						var buff = new byte[count];
						fixed (void* bufferPtr = buff)
						{
							Unsafe.CopyBlockUnaligned(bufferPtr, (void*)address, (uint)count);

							try
							{
								bytes = buff;
								return true;
							}
							finally
							{
								VirtualProtect(lpBuffer.AllocationBase, lpBuffer.RegionSize.ToInt32(), _oldProtectionFlags, out _);
							}
						}
					}

					// VirtualProtect on region with read/write/execute permissions failed
					Console.WriteLine("Virtual Protect failed setting code page to EXECUTE/READ/WRITE permissions, returning false");
					bytes = null;
					return false;
				}

				{
					var buff = new byte[count];
					fixed (void* bufferPtr = buff)
					{
						Unsafe.CopyBlockUnaligned(bufferPtr, (void*)address, (uint)count);
						bytes = buff;
						return true;
					}
				}
			}
			public static unsafe bool WriteBytesEx(IntPtr address, byte[] buffer, bool tryVirtualProtectIfNeeded = false)
			{
				if (address == IntPtr.Zero || buffer == null || buffer.Length < 1) return false;

				if (!tryVirtualProtectIfNeeded)
				{
					void* ptr = (void*)address;
					fixed (void* pBuff = buffer)
					{
						Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
					}
					return true;
				}

				//var withoutVirtualProtectSuccess = WriteBytesEx(address, buffer, false);
				//if (withoutVirtualProtectSuccess)
				//{
				//	Console.WriteLine($"ReadBytesEx on address 0x{address.ToInt32():X8} with tryVirtualProtectIfNeeded bool set to false successfully wrote {buffer.Length} bytes!");
				//	return true;
				//}

				var virtualQueryResult = VirtualQuery(address, out var lpBuffer, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
				if (virtualQueryResult == 0)
				{
					Console.WriteLine($"VirtualQuery on address 0x{address.ToInt32():X8} returned zero, trying to read anyways ...");
					void* ptr = (void*)address;
					fixed (void* pBuff = buffer)
					{
						Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
					}
					return true;
				}
				else
				{
					Console.WriteLine($"Target Address: 0x{address.ToInt32():X8}\n" +
									  $"Requested amount of bytes to write: {buffer.Length}\n\n" +
									  "Target Address Information:\n" +
									  $"	* Allocation Base: 0x{lpBuffer.AllocationBase.ToInt32():X8}\n" +
									  $"	* Base Address: 0x{lpBuffer.BaseAddress.ToInt32():X8}\n" +
									  $"	* Region Size: {lpBuffer.RegionSize.ToInt32()} Bytes (Hex: 0x{lpBuffer.RegionSize.ToInt32():X})\n" +
									  $"	* Region State: {lpBuffer.State.ToString()}\n" +
									  $"	* Region Type: {lpBuffer.Type.ToString()}\n" +
									  $"	* Region Protection Flags: {lpBuffer.Protect.ToString()}\n" +
									  $"	* Region Allocation Flags: {lpBuffer.AllocationProtect.ToString()}\n");


					if ((lpBuffer.Protect == MemoryProtectionFlags.ExecuteReadWrite ||
						lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ExecuteReadWrite) ||

						lpBuffer.Protect == MemoryProtectionFlags.ReadWrite ||
						lpBuffer.Protect.HasFlag(MemoryProtectionFlags.ReadWrite))

						&&

						(lpBuffer.State == MemoryState.MEM_COMMIT ||
						lpBuffer.State.HasFlag(MemoryState.MEM_COMMIT))

						&&

						lpBuffer.Type == MemoryType.MEM_MAPPED ||
						(lpBuffer.Type.HasFlag(MemoryType.MEM_MAPPED)))
					{
						Console.WriteLine($"VirtualQuery on address 0x{address.ToInt32():X8} returned zero, trying to read anyways ...");
						void* ptr = (void*)address;
						fixed (void* pBuff = buffer)
						{
							Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
						}
						return true;
					}
					else
					{
						// State Flags was not MEM_COMMIT or did not have MEM_COMMIT flag in it
						if (!(lpBuffer.State == MemoryState.MEM_COMMIT ||
							  lpBuffer.State.HasFlag(MemoryState.MEM_COMMIT)))
						{
							Console.WriteLine($"Region was not committed, returning false");
							return false;
						}

						// Type Flags was not MEM_MAPPED or did not have MEM_MAPPED flag in it
						if (!(lpBuffer.Type == MemoryType.MEM_MAPPED ||
							  lpBuffer.Type.HasFlag(MemoryType.MEM_MAPPED)))
						{
							Console.WriteLine($"Region was not mapped, check if Region type flags has MEM_IMAGE flag");
							if (lpBuffer.Type == MemoryType.MEM_IMAGE ||
								lpBuffer.Type.HasFlag(MemoryType.MEM_IMAGE))
							{
								Console.WriteLine($"Region type flags had MEM_IMAGE flag, proceeeding to write ...");
								void* ptr = (void*)address;
								fixed (void* pBuff = buffer)
								{
									Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
								}
								return true;
							}

							Console.WriteLine($"Region Type flags did not have MEM_IMAGE flag");
							return false;
						}

						bool regionVirtualProtectSetRWX = VirtualProtect(lpBuffer.AllocationBase, lpBuffer.RegionSize.ToInt32(), MemoryProtectionFlags.ExecuteReadWrite, out var _oldProtection);
						if (regionVirtualProtectSetRWX)
						{
							Console.WriteLine($"Setting region protection to Read/Write/Execute was successfull!");
							try
							{
								void* ptr = (void*)address;
								fixed (void* pBuff = buffer)
								{
									Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
								}

								return true;
							}
							finally
							{
								VirtualProtect(lpBuffer.AllocationBase, lpBuffer.RegionSize.ToInt32(), _oldProtection, out _);
							}
						}
						else
						{
							Console.WriteLine($"Region State flags had MEM_COMMIT flag: {lpBuffer.State.HasFlag(MemoryState.MEM_COMMIT)}\n" +
											  $"Region Type flags had MEM_MAPPED flag: {lpBuffer.Type.HasFlag(MemoryType.MEM_MAPPED)}\n" +
											  $"Attempt to set region to RWX: {(regionVirtualProtectSetRWX ? "Success" : "Failed")}\n" +
											  $"Attempting to write without changing any protection ...");

							void* ptr = (void*)address;
							fixed (void* pBuff = buffer)
							{
								Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
							}

							return true;
						}
					}
				}
			}



			public static unsafe IntPtr HookSet(IntPtr funcAddress, IntPtr hkAddress, int optPrologueLengthFixup = 5)
			{
				// Returns address to original unhooked function
				// Return value is also used in function UnsetHook

				IntPtr oFunctionAddress = AllocateMemory(10 + (uint)(optPrologueLengthFixup - 5));
				if (oFunctionAddress == IntPtr.Zero)
					return IntPtr.Zero;

				Threads.SuspendProcess();
				Protection.SetPageProtection(funcAddress, optPrologueLengthFixup, MemoryProtectionFlags.ExecuteReadWrite, out var initProtect);

				Writer.UnsafeWriteBytes(oFunctionAddress, Reader.UnsafeReadBytes(funcAddress, (uint)optPrologueLengthFixup));

				byte* t = (byte*)funcAddress;
				*t = 0xe9;
				t++;
				*(uint*)t = ((uint)hkAddress - (uint)t - 4);

				byte* nopLocation = (byte*)(funcAddress + 5);
				for (int n = 0; n < optPrologueLengthFixup - 5; n++)
				{
					*nopLocation = 0x90;
					nopLocation++;
				}

				Protection.SetPageProtection(funcAddress, optPrologueLengthFixup, initProtect, out _);
				t = (byte*)oFunctionAddress + optPrologueLengthFixup;
				*t = 0xe9;
				t++;

				*(uint*)t = ((uint)funcAddress - (uint)t + 1 + (uint)(optPrologueLengthFixup - 5));
				Protection.SetPageProtection(oFunctionAddress, 10 + (optPrologueLengthFixup - 5), MemoryProtectionFlags.ExecuteRead, out _);
				Threads.ResumeProcess();

				return oFunctionAddress;
			}
			public static unsafe void UnsetHook(IntPtr funcAddress, IntPtr HookSetReturnValue, int optPrologueLengthFixup = 5)
			{
				Protection.SetPageProtection(funcAddress, optPrologueLengthFixup, MemoryProtectionFlags.ExecuteReadWrite, out var old);
				Writer.UnsafeWriteBytes(funcAddress,
					Reader.UnsafeReadBytes(HookSetReturnValue, (uint)optPrologueLengthFixup));
				Protection.SetPageProtection(funcAddress, optPrologueLengthFixup, old, out _);
				FreeMemory(HookSetReturnValue, 10 + (uint)optPrologueLengthFixup - 5);
			}

		}

		public class Protection
		{
			public static bool SetPageProtection(IntPtr baseAddress, int size, MemoryProtectionFlags newProtection, out MemoryProtectionFlags oldProtection)
			{
				bool res = VirtualProtect(baseAddress, size, newProtection, out var oldProtect);
				oldProtection = oldProtect;
				return res;
			}

			public static MEMORY_BASIC_INFORMATION GetPageProtection(IntPtr baseAddress)
			{
				int res = VirtualQuery(baseAddress, out MEMORY_BASIC_INFORMATION buff, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
				return buff;
			}
		}

		public class Threads
		{
			public static void SuspendProcess()
			{
				UpdateProcessInformation();

				ProcessModule ourModule = OurModule;
				IntPtr start = IntPtr.Zero;
				IntPtr end = IntPtr.Zero;
				
				if (ourModule != null)
				{
					start = ourModule.BaseAddress;
					end = ourModule.BaseAddress + ourModule.ModuleMemorySize;
				}


				foreach (ProcessThread pT in HostProcess.Threads)
				{
					if (start == IntPtr.Zero || end == IntPtr.Zero)
					{
						IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);
						if (pOpenThread == IntPtr.Zero)
							continue;

						SuspendThread(pOpenThread);
						CloseHandle(pOpenThread);
					}
					else
					{
						if (pT.StartAddress.ToInt32() < start.ToInt32() || pT.StartAddress.ToInt32() > end.ToInt32())
						{
							IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);
							if (pOpenThread == IntPtr.Zero)
								continue;

							SuspendThread(pOpenThread);
							CloseHandle(pOpenThread);
						}
					}
				}
			}

			public static void ResumeProcess()
			{
				var process = Process.GetCurrentProcess();

				if (process.ProcessName == string.Empty)
					return;

				foreach (ProcessThread pT in process.Threads)
				{
					IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

					if (pOpenThread == IntPtr.Zero)
					{
						continue;
					}

					var suspendCount = 0;
					do
					{
						suspendCount = ResumeThread(pOpenThread);
					} while (suspendCount > 0);

					CloseHandle(pOpenThread);
				}
			}
		}
	}
}
