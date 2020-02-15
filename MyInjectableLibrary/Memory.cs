using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

//using Reloaded;

namespace MyInjectableLibrary
{
	public class Memory
	{
		public static IntPtr AllocateMemory(uint size, PInvoke.MemoryProtectionFlags memoryProtection = PInvoke.MemoryProtectionFlags.ExecuteReadWrite) =>
			size < 1 ? IntPtr.Zero : PInvoke.VirtualAlloc(IntPtr.Zero, new UIntPtr(size), PInvoke.AllocationTypeFlags.Commit | PInvoke.AllocationTypeFlags.Reserve, PInvoke.MemoryProtectionFlags.ExecuteReadWrite);
		public static bool FreeMemory(IntPtr baseAddress, uint optionalSize = 0)
			=> baseAddress != IntPtr.Zero && PInvoke.VirtualFree(baseAddress, optionalSize, PInvoke.FreeType.Release);

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
							bool virtualProtectResul_RWX = PInvoke.VirtualProtect(location, (int)lpBuffer.RegionSize, PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out var oldProtection);
							if (!virtualProtectResul_RWX)
							{
								// Check if we can set page protection to Execute/Write Copy
								bool virtualProtectResult_EWC = PInvoke.VirtualProtect(location, (int) lpBuffer.RegionSize, PInvoke.MemoryProtectionFlags.ExecuteWriteCopy, out oldProtection);
								if (!virtualProtectResult_EWC) return false;
							}

							void* ptr = (void*)location;
							fixed (void* pBuff = buffer)
							{
								Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint)buffer.Length);
							}
							return Reader.UnsafeReadBytes(location, (uint)buffer.Length) == buffer &&
							       PInvoke.VirtualProtect(location, (int)lpBuffer.RegionSize, oldProtection, out var discard);
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

			private static unsafe IntPtr CEFindPattern(string pattern, PInvoke.CERegion writableMemory = PInvoke.CERegion.YES, PInvoke.CERegion executableMemory = PInvoke.CERegion.DONT_CARE, uint startRange = 0x00000000, uint stopRange = 0xffffffff)
			{
				if (pattern == "") return IntPtr.Zero;
				if (writableMemory == PInvoke.CERegion.NO && executableMemory == PInvoke.CERegion.NO) return IntPtr.Zero;
				if (startRange == stopRange || stopRange > startRange) return IntPtr.Zero;


				List<PInvoke.MEMORY_BASIC_INFORMATION> regionsToScan = new List<PInvoke.MEMORY_BASIC_INFORMATION>();
				for (uint address = startRange; address < stopRange;)
				{
					var virtualQuery = PInvoke.VirtualQuery(new IntPtr(address), out PInvoke.MEMORY_BASIC_INFORMATION mbi, (uint) Marshal.SizeOf<PInvoke.MEMORY_BASIC_INFORMATION>());
					if (virtualQuery != 0)
					{
						if (mbi.State == PInvoke.MemoryState.MEM_COMMIT && !(mbi.Protect.HasFlag(PInvoke.MemoryProtectionFlags.Guard)))
						{
							if (Reader.UnsafeReadBytes(new IntPtr(address),1).Length != 1)
								continue;

							if (executableMemory == PInvoke.CERegion.NO && (mbi.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteReadWrite) ||
							                                                mbi.Protect.HasFlag(PInvoke.MemoryProtectionFlags.Execute) ||
							                                                mbi.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteRead) ||
							                                                mbi.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteWriteCopy)))
							{
								address += (uint)mbi.RegionSize.ToInt32();
								continue;
							}

							if (writableMemory == PInvoke.CERegion.NO && (mbi.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ReadWrite) ||
							                                                mbi.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteReadWrite) ||
							                                                mbi.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteWriteCopy)))
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

			public static uint ExecuteAssembly(List<string> mnemonics, bool recalculateAddressIfNeeded = true)
			{
				IntPtr alloc = IntPtr.Zero;
				try
				{
					if (mnemonics == null || mnemonics.Count < 1) return 0;
					alloc = AllocateMemory(0x10000);
					if (alloc == IntPtr.Zero) throw new Exception("failed allocating codecave");

					if (mnemonics[0].ToLower() != "use32" || mnemonics[0].ToLower() != "use64")
						mnemonics.Insert(0, "use32"); // Assume we´re assemling x86 mnemonics

					if (recalculateAddressIfNeeded)
						if (mnemonics[0] == "use32" || mnemonics[0] == "64")
							mnemonics.Insert(1, $"org {alloc}");

					var asm = Assembler.Assemble(mnemonics);
					if (asm == null || asm.Length < 1) return 0;

					Writer.UnsafeWriteBytes(alloc, asm);

					IntPtr t = PInvoke.CreateThread(IntPtr.Zero, 0, alloc, IntPtr.Zero, 0, out IntPtr threadID);
					if (t == IntPtr.Zero) return 0;

					var result = PInvoke.WaitForSingleObject(t, 0xFFFFFFFF);
					bool res = PInvoke.GetExitCodeThread(t, out uint resultPtr);
					if (!res) throw new Exception("failed get exit code");

					PInvoke.VirtualFree(alloc, 0, PInvoke.FreeType.Release);
					PInvoke.CloseHandle(t);
					return resultPtr;
				}
				catch
				{
					if (alloc != IntPtr.Zero)
						FreeMemory(alloc);
					return 0;
				}
			}
			public static uint ExecuteAssembly(byte[] asm)
			{
				if (asm == null || asm.Length < 1) return uint.MinValue;
				IntPtr alloc = IntPtr.Zero;
				try
				{
					alloc = AllocateMemory(asm.Length < 0x1000 ? (uint) 0x1000 : (uint) 0x10000);
					if (alloc == IntPtr.Zero) throw new Exception("failed allocating codecave");

					Writer.UnsafeWriteBytes(alloc, asm);

					IntPtr t = PInvoke.CreateThread(IntPtr.Zero, 0, alloc, IntPtr.Zero, 0, out IntPtr threadID);
					if (t == IntPtr.Zero) return 0;

					var result = PInvoke.WaitForSingleObject(t, 0xFFFFFFFF); // Wait forever?
					bool res = PInvoke.GetExitCodeThread(t, out uint resultPtr);
					if (!res) throw new Exception("failed get exit code");

					PInvoke.VirtualFree(alloc, 0, PInvoke.FreeType.Release);
					PInvoke.CloseHandle(t);
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

		public class Assembler
		{
			public static byte[] Assemble(List<string> mnemonics, int origin = -1)
			{
				Reloaded.Assembler.Assembler asm = null;
				try
				{
					if (mnemonics == null || mnemonics.Count < 1) return null;
					int originInsertIndex = -1;
					if (mnemonics[0].ToLower() == "use32" ||
					    mnemonics[0].ToLower() == "use64")
					{
						originInsertIndex = 1;
					}
					else
					{
						originInsertIndex = 0;
					}

					if (origin > -1)
						mnemonics.Insert(originInsertIndex, $"org {origin}");

					asm = new Reloaded.Assembler.Assembler();
					return asm?.Assemble(mnemonics);
				}
				finally
				{
					asm?.Dispose();
				}
				
			}
			public static byte[] Assemble(string[] mnemonics, int origin = -1)
			{
				Reloaded.Assembler.Assembler asm = null;
				try
				{
					if (mnemonics == null || mnemonics.Length < 1) return null;
					int originInsertIndex = -1;
					if (mnemonics[0].ToLower() == "use32" ||
					    mnemonics[0].ToLower() == "use64")
					{
						originInsertIndex = 1;
					}
					else
					{
						originInsertIndex = 0;
					}

					if (origin > -1)
					{
						Array.Resize(ref mnemonics, mnemonics.Length + 1);
						
					}

					List<string> tmp = mnemonics.ToList();
					tmp.Insert(originInsertIndex, $"org {origin}");
					mnemonics = tmp.ToArray();

					asm = new Reloaded.Assembler.Assembler();
					return asm?.Assemble(mnemonics);
				}
				finally
				{
					asm?.Dispose();
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
				byte[] buff = Memory.Reader.UnsafeReadBytes(targetProcessModule.BaseAddress, (uint)targetProcessModule.ModuleMemorySize);
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
			public static bool RedirectCode(IntPtr target, int instructionLength, List<string> mnemonics, out IntPtr caveAddress, bool keepOverWrittenInstructions = false)
			{
				if (target == IntPtr.Zero)
				{
					caveAddress = IntPtr.Zero;
					Console.WriteLine($"[Detour] Target address was zero");
					return false;
				}

				if (instructionLength < 5) throw new InvalidOperationException($"Instruction length to overwrite must be atleast 5 bytes");
				byte[] _tmpAsm = Assembler.Assemble(mnemonics, -1);
				IntPtr codecave = AllocateMemory((uint)_tmpAsm.Length + (uint)instructionLength + 5, PInvoke.MemoryProtectionFlags.ExecuteReadWrite);
				if (codecave == IntPtr.Zero) throw new InvalidOperationException($"Failed allocating code for codecave");
				Console.WriteLine($"CodeCave Address: 0x{codecave.ToInt32():X8}");

				List<byte> _caveAsm = new List<byte>();
				List<byte> jumpInAsm = new List<byte>();
				bool origBytesWriteFailed = false;

				try
				{
					jumpInAsm = Assembler.Assemble(new List<string>()
					{
						"use32",
						$"jmp {codecave.ToInt32() - target.ToInt32() - 1}"
					}).ToList();

					for (int n = 0; n < instructionLength - 5; n++)
						jumpInAsm.Add(0x90);

					
					int numBytesBeforeJmpOut = _tmpAsm.Length; /* +  _register dump asm byte array length; */
					if (keepOverWrittenInstructions)
					{
						var _oBytes = Reader.UnsafeReadBytes(target, (uint)instructionLength);
						if (_oBytes == null)
						{
							Console.WriteLine($"{nameof(keepOverWrittenInstructions)} was true but failed to read the original overwritten instructions/bytes, moving on ...");
						}
						else
						{
							numBytesBeforeJmpOut += instructionLength;
							if (!WriteBytesEx(codecave, _oBytes, false))
							{
								FreeMemory(codecave);
								caveAddress = IntPtr.Zero;
								Console.WriteLine($"[Detour] Failed writing original overwritten bytes to start of codecave (0x{codecave.ToInt32():X8}), moving on ...");
								numBytesBeforeJmpOut -= instructionLength;
								origBytesWriteFailed = true;
							} else 
								Console.WriteLine($"Successfully wrote overwritten bytes to the start of the code cave");
						}
					}

					mnemonics.AddRange(new[]
					{
						$"jmp {(target + instructionLength) - (codecave.ToInt32() + numBytesBeforeJmpOut) + 1}"
					});
					_caveAsm = Assembler.Assemble(mnemonics, codecave.ToInt32()).ToList();
				}
				catch (Exception e)
				{
					FreeMemory(codecave);
					caveAddress = IntPtr.Zero;
					Console.WriteLine($"[Detour] Failed assembling stuff");
					return false;
				}


				if (!origBytesWriteFailed)
				{
					if (!WriteBytesEx(IntPtr.Add(codecave, instructionLength), _caveAsm.ToArray(), false))
					{
						FreeMemory(codecave);
						caveAddress = IntPtr.Zero;
						Console.WriteLine($"[Detour] Failed writing mnemonics to codecave (0x{codecave.ToInt32():X8})");
						return false;
					}
				}
				else
				{
					if (!WriteBytesEx(codecave, _caveAsm.ToArray(), false))
					{
						FreeMemory(codecave);
						caveAddress = IntPtr.Zero;
						Console.WriteLine($"[Detour] Failed writing mnemonics to codecave (0x{codecave.ToInt32():X8})");
						return false;
					}
				}
				
				var origBytes = Reader.UnsafeReadBytes(target, (uint)instructionLength);
				if (!WriteBytesEx(target, jumpInAsm.ToArray(), true))
				{
					FreeMemory(codecave);
					caveAddress = IntPtr.Zero;
					Console.WriteLine($"[Detour] Failed writing jmp to codecave at address 0x{target.ToInt32():X8}");
					return false;
				}

				bool confirm = ReadBytesEx(target, 5, out byte[] bytes, true);
				//if (ReadBytesEx(target, 5) == jumpInAsm.ToArray())
				if (bytes != null)
				{
					caveAddress = codecave;
					return true;
				}


				if (origBytes != null && origBytes.Length < 0)
					WriteBytesEx(target, origBytes, true);
				FreeMemory(codecave);
				caveAddress = IntPtr.Zero;
				return false;
			}

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
						Unsafe.CopyBlockUnaligned(bufferPtr, (void*) address, (uint) count);

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

				var virtualQueryResult = PInvoke.VirtualQuery(address, out var lpBuffer, (uint) Marshal.SizeOf<PInvoke.MEMORY_BASIC_INFORMATION>());
				if (virtualQueryResult == 0)
				{
					Console.WriteLine($"VirtualQuery on address 0x{address.ToInt32():X8} returned zero, trying to read anyways ...");
					var buff = new byte[count];

					fixed (void* bufferPtr = buff)
					{
						Unsafe.CopyBlockUnaligned(bufferPtr, (void*) address, (uint) count);

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

				if (lpBuffer.Equals(default(PInvoke.MEMORY_BASIC_INFORMATION)))
				{
					Console.WriteLine("Virtual Query returned zero (was successfull) but the MEMORY_BASIC_INFORMATION was null/default!");
					bytes = null;
					return false;
				}

				if ((lpBuffer.State == PInvoke.MemoryState.MEM_COMMIT ||
				     lpBuffer.State.HasFlag(PInvoke.MemoryState.MEM_COMMIT)) &&
				    lpBuffer.Protect == PInvoke.MemoryProtectionFlags.ExecuteRead ||
				    lpBuffer.Protect == PInvoke.MemoryProtectionFlags.ExecuteReadWrite ||
				    lpBuffer.Protect == PInvoke.MemoryProtectionFlags.ReadWrite ||
				    lpBuffer.Protect == PInvoke.MemoryProtectionFlags.ReadOnly ||

				    lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteRead) ||
				    lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteReadWrite) ||
				    lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ReadWrite) ||
				    lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ReadOnly))
				{
					var buff = new byte[count];
					fixed (void* bufferPtr = buff)
					{
						Unsafe.CopyBlockUnaligned(bufferPtr, (void*) address, (uint) count);

						bytes = buff;
						return true;
					}
				}

				// Region state was not MEM_COMMMIT or region protect flags didnt include any read permission whatsoever
				if (lpBuffer.State != PInvoke.MemoryState.MEM_COMMIT ||
				    !lpBuffer.State.HasFlag(PInvoke.MemoryState.MEM_COMMIT))
				{
					Console.WriteLine("Region State was not MEM_COMMIT, or had flag MEM_COMMIT in it, returning false ...");
					bytes = null;
					return false;
				}

				if (lpBuffer.Protect == PInvoke.MemoryProtectionFlags.NoAccess ||
				    lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.NoAccess))
				{
					Console.WriteLine("Region State was either set to NO_ACCESS or it had the NO_ACCESS flag in it, returning false ...");
					bytes = null;
					return false;
				}


				if (lpBuffer.Type != PInvoke.MemoryType.MEM_MAPPED &&
				    !lpBuffer.Type.HasFlag(PInvoke.MemoryType.MEM_MAPPED))
				{
					// If type is not mapped and type does not contain MEM_MAPPED flag
					Console.WriteLine("Region Type is not MEM_MAPPED nor does the Region Type flags contain the MEM_MAPPED flag, returning false");
					bytes = null;
					return false;
				}

				if (!(lpBuffer.Protect == PInvoke.MemoryProtectionFlags.ExecuteRead ||
				      lpBuffer.Protect == PInvoke.MemoryProtectionFlags.ExecuteReadWrite ||
				      lpBuffer.Protect == PInvoke.MemoryProtectionFlags.ReadWrite ||
				      lpBuffer.Protect == PInvoke.MemoryProtectionFlags.ReadOnly ||

				      lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteRead) ||
				      lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteReadWrite) ||
				      lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ReadWrite) ||
				      lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ReadOnly)))
				{
					var virtualProtectRegionRWX = PInvoke.VirtualProtect(lpBuffer.AllocationBase, lpBuffer.RegionSize.ToInt32(), PInvoke.MemoryProtectionFlags.ExecuteReadWrite,
						out var _oldProtectionFlags);
					if (virtualProtectRegionRWX)
					{
						var buff = new byte[count];
						fixed (void* bufferPtr = buff)
						{
							Unsafe.CopyBlockUnaligned(bufferPtr, (void*) address, (uint) count);

							try
							{
								bytes = buff;
								return true;
							}
							finally
							{
								PInvoke.VirtualProtect(lpBuffer.AllocationBase, lpBuffer.RegionSize.ToInt32(), _oldProtectionFlags, out _);
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
						Unsafe.CopyBlockUnaligned(bufferPtr, (void*) address, (uint) count);
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

				var virtualQueryResult = PInvoke.VirtualQuery(address, out var lpBuffer, (uint)Marshal.SizeOf<PInvoke.MEMORY_BASIC_INFORMATION>());
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


					if ((lpBuffer.Protect == PInvoke.MemoryProtectionFlags.ExecuteReadWrite ||
					    lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ExecuteReadWrite) ||
					    
					    lpBuffer.Protect == PInvoke.MemoryProtectionFlags.ReadWrite ||
					    lpBuffer.Protect.HasFlag(PInvoke.MemoryProtectionFlags.ReadWrite))

					    &&

					    (lpBuffer.State == PInvoke.MemoryState.MEM_COMMIT ||
					    lpBuffer.State.HasFlag(PInvoke.MemoryState.MEM_COMMIT))

					    &&

					    lpBuffer.Type == PInvoke.MemoryType.MEM_MAPPED ||
					    (lpBuffer.Type.HasFlag(PInvoke.MemoryType.MEM_MAPPED)))
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
						if (!(lpBuffer.State == PInvoke.MemoryState.MEM_COMMIT ||
						      lpBuffer.State.HasFlag(PInvoke.MemoryState.MEM_COMMIT)))
						{
							Console.WriteLine($"Region was not committed, returning false");
							return false;
						}

						// Type Flags was not MEM_MAPPED or did not have MEM_MAPPED flag in it
						if (!(lpBuffer.Type == PInvoke.MemoryType.MEM_MAPPED ||
						      lpBuffer.Type.HasFlag(PInvoke.MemoryType.MEM_MAPPED)))
						{
							Console.WriteLine($"Region was not mapped, check if Region type flags has MEM_IMAGE flag");
							if (lpBuffer.Type == PInvoke.MemoryType.MEM_IMAGE ||
							    lpBuffer.Type.HasFlag(PInvoke.MemoryType.MEM_IMAGE))
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

						bool regionVirtualProtectSetRWX = PInvoke.VirtualProtect(lpBuffer.AllocationBase, lpBuffer.RegionSize.ToInt32(), PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out var _oldProtection);
						if (regionVirtualProtectSetRWX)
						{
							Console.WriteLine($"Setting region protection to Read/Write/Execute was successfull!");
							try
							{
								void* ptr = (void*) address;
								fixed (void* pBuff = buffer)
								{
									Unsafe.CopyBlockUnaligned(ptr, pBuff, (uint) buffer.Length);
								}

								return true;
							}
							finally
							{
								PInvoke.VirtualProtect(lpBuffer.AllocationBase, lpBuffer.RegionSize.ToInt32(), _oldProtection, out _);
							}
						}
						else
						{
							Console.WriteLine($"Region State flags had MEM_COMMIT flag: {lpBuffer.State.HasFlag(PInvoke.MemoryState.MEM_COMMIT)}\n" +
							                  $"Region Type flags had MEM_MAPPED flag: {lpBuffer.Type.HasFlag(PInvoke.MemoryType.MEM_MAPPED)}\n" +
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

			public static bool RegisterOnExecutedCallback(IntPtr instructionLocation, int instructionLength, Structs.CallbackDelegate callbackFunction, byte[] onExecutedAssembly,
				bool keepOverwrittenBytes = true, bool placeCustomShellcodeFirst = false)
			{
				if (instructionLength < 5)
					return false;

				IntPtr _callbackAddr = Marshal.GetFunctionPointerForDelegate(callbackFunction);
				if (_callbackAddr == IntPtr.Zero)
				{
					Console.WriteLine($"Address of delegate was zero!");
					return false;
				}

				var _cave = AllocateMemory(0x10000);
				if (_cave == IntPtr.Zero)
				{
					Console.WriteLine($"Failed allocating memory for code cave");
					return false;
				}
				
				int nops = instructionLength - 5;
				List<byte> jmpIn = Assembler.Assemble(new List<string>()
				{
					"use32",
					$"jmp 0x{_cave.ToInt32():X8}"
				}, instructionLocation.ToInt32()).ToList();

				for (int n = 0; n < nops; n++)
					jmpIn.Add(0x90);

				Console.WriteLine($"C# Function Address: 0x{_callbackAddr.ToInt32():X8}");
				Console.WriteLine($"Code Cave: 0x{_cave.ToInt32():X8}");

				// TODO: Check protection of address before changing it

				bool protecc = Protection.SetPageProtection(instructionLocation, instructionLength, PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out var old);
				if (!protecc)
				{
					Console.WriteLine($"Failed changing page protection!");
					return false;
				}

				int offset = 0;

				byte[] oBytes = Reader.UnsafeReadBytes(instructionLocation, (uint)instructionLength);
				
				if (keepOverwrittenBytes)
				{
					if (placeCustomShellcodeFirst)
					{
						bool onExecutedAssemblyBytes = Writer.UnsafeWriteBytes(_cave + offset, onExecutedAssembly);
						offset += onExecutedAssemblyBytes ? onExecutedAssembly.Length : 0;

						bool oBytesWrite = Writer.UnsafeWriteBytes(_cave + offset, oBytes);

						if (!oBytesWrite)
						{
							FreeMemory(_cave);
							Protection.SetPageProtection(instructionLocation, instructionLength, old, out _);
							Console.WriteLine($"Failed writing original bytes to code cave!");
							return false;
						}
					}
					else
					{
						bool oBytesWrite = Writer.UnsafeWriteBytes(_cave, oBytes);
						offset += oBytesWrite ? instructionLength : 0;

						if (!oBytesWrite)
						{
							FreeMemory(_cave);
							Protection.SetPageProtection(instructionLocation, instructionLength, old, out _);
							Console.WriteLine($"Failed writing original bytes to code cave!");
							return false;
						}

						if (onExecutedAssembly != null && onExecutedAssembly.Length > 0)
						{
							bool onExecutedAssemblyBytes = Writer.UnsafeWriteBytes(_cave + offset, onExecutedAssembly);
							offset += onExecutedAssemblyBytes ? onExecutedAssembly.Length : 0;
						}
					}
				}
				else
				{
					if (onExecutedAssembly != null && onExecutedAssembly.Length > 0)
					{
						bool onExecutedAssemblyBytes = Writer.UnsafeWriteBytes(_cave + offset, onExecutedAssembly);
						offset += onExecutedAssemblyBytes ? onExecutedAssembly.Length : 0;
					}
				}

				IntPtr _register = AllocateMemory((uint)Marshal.SizeOf(typeof(Structs.Registers)), PInvoke.MemoryProtectionFlags.ReadWrite);
				if (_register == IntPtr.Zero)
				{
					FreeMemory(_cave);
					Protection.SetPageProtection(instructionLocation, instructionLength, old, out _);
					return false;
				}

				List<byte> callDelegate = new List<byte>();
				try
				{
					callDelegate = Assembler.Assemble(new List<string>()
					{
						"use32",

						$"mov [0x{_register.ToInt32():X8}], eax",
						$"mov [0x{_register.ToInt32() + 4:X8}], ebx",
						$"mov [0x{_register.ToInt32() + 8:X8}], ecx",
						$"mov [0x{_register.ToInt32() + 12:X8}], edx",
						$"mov [0x{_register.ToInt32() + 16:X8}], esi",
						$"mov [0x{_register.ToInt32() + 20:X8}], edi",
						$"mov [0x{_register.ToInt32() + 24:X8}], ebp",
						$"mov dword [0x{_register.ToInt32() + 28:X8}], 0x{_cave.ToInt32():X8}",

						// Address to our cave
						$"push 0x{_register.ToInt32():X8}",  // pushes address of our _register struct to the stack
						$"call 0x{_callbackAddr.ToInt32():X8}", // call our c# method (callbackFunction parameter) from unmanaged code
						"pop ebx", // Should put address of our _register pointer into ebx, stack is now back to normal?


						$"jmp 0x{(instructionLocation.ToInt32() + instructionLength):X8}"
					}, _cave.ToInt32() + offset).ToList();
				}
				catch
				{
					FreeMemory(_cave);
					FreeMemory(_register);
					Protection.SetPageProtection(instructionLocation, instructionLength, old, out _);
					Console.WriteLine($"Failed assembling cave body code!");
					return false;
				}

				bool result = Writer.UnsafeWriteBytes(_cave + offset, callDelegate.ToArray()) && Writer.UnsafeWriteBytes(instructionLocation, jmpIn.ToArray());
				if (!result)
				{
					bool jumpInSuccessful = Reader.UnsafeReadBytes(instructionLocation, (uint)jmpIn.Count) == jmpIn.ToArray();
					bool jumpOutSucessfull = Reader.UnsafeReadBytes(_cave, (uint) callDelegate.Count) == callDelegate.ToArray();

					if (!jumpOutSucessfull)
					{
						Protection.SetPageProtection(instructionLocation, instructionLength, old, out _);
						FreeMemory(_cave);
						FreeMemory(_register);
						Console.WriteLine($"Failed writing jump out bytes!");
						return false;
					}

					if (!jumpInSuccessful)
					{
						Protection.SetPageProtection(instructionLocation, instructionLength, old, out _);
						FreeMemory(_cave);
						FreeMemory(_register);
						Console.WriteLine($"Failed writing jump in bytes!");
						return false;
					}
				}

				bool protecc_restore = Protection.SetPageProtection(instructionLocation, instructionLength, old, out _);
				if (!protecc_restore)
				{
					Console.WriteLine($"Failed restoring page protection!");
				}

				if (result)
					Protection.SetPageProtection(_cave, 0x1000, PInvoke.MemoryProtectionFlags.ExecuteRead, out _);
				return result;
			}

			public static unsafe IntPtr HookSet(IntPtr funcAddress, IntPtr hkAddress, int optPrologueLengthFixup = 5)
			{
				// Returns address to original unhooked function
				// Return value is also used in function UnsetHook

				IntPtr oFunctionAddress = AllocateMemory(10 + (uint)(optPrologueLengthFixup - 5));
				if (oFunctionAddress == IntPtr.Zero)
					return IntPtr.Zero;

				Threads.SuspendProcess();
				Protection.SetPageProtection(funcAddress, optPrologueLengthFixup, PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out var initProtect);

				Writer.UnsafeWriteBytes(oFunctionAddress, Reader.UnsafeReadBytes(funcAddress, (uint)optPrologueLengthFixup));

				byte* t = (byte*) funcAddress;
				*t = 0xe9;
				t++;
				*(uint*)t = ((uint)hkAddress - (uint)t - 4);

				byte* nopLocation = (byte*) (funcAddress + 5);
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
				Protection.SetPageProtection(oFunctionAddress, 10 + (optPrologueLengthFixup - 5), PInvoke.MemoryProtectionFlags.ExecuteRead, out _);
				Threads.ResumeProcess();

				return oFunctionAddress;
			}
			public static unsafe void UnsetHook(IntPtr funcAddress, IntPtr HookSetReturnValue, int optPrologueLengthFixup = 5)
			{
				Protection.SetPageProtection(funcAddress, optPrologueLengthFixup, PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out var old);
				Writer.UnsafeWriteBytes(funcAddress,
					Reader.UnsafeReadBytes(HookSetReturnValue, (uint) optPrologueLengthFixup));
				Protection.SetPageProtection(funcAddress, optPrologueLengthFixup, old, out _);
				FreeMemory(HookSetReturnValue, 10 + (uint)optPrologueLengthFixup - 5);
			}

			private static IntPtr Test(IntPtr funcAddress, IntPtr hkAddress, int optPrologueLengthFixup = 5)
			{
				Protection.SetPageProtection(funcAddress, optPrologueLengthFixup,
					PInvoke.MemoryProtectionFlags.ExecuteReadWrite,
					out var old);

				if (optPrologueLengthFixup > 5)
				{
					// Room for additional prologue bytes + jmp real Hook
					IntPtr _middleCave = AllocateMemory((uint) (optPrologueLengthFixup - 3) + 5);

					byte[] overflowingBytes = Reader.UnsafeReadBytes(funcAddress + 3, (uint)optPrologueLengthFixup - 3);
					Writer.UnsafeWriteBytes(_middleCave, overflowingBytes);

					byte[] jmpToRealHook = Assembler.Assemble(new List<string>
						{
							"use32",
							$"jmp 0x{hkAddress.ToInt32():X8}"
						}, _middleCave.ToInt32() + overflowingBytes.Length);

					Writer.UnsafeWriteBytes(_middleCave + overflowingBytes.Length, jmpToRealHook);

					// Room for original prologue + jmp to (original function + optPrologueLengthFixup)
					IntPtr _newRegion = AllocateMemory((uint) optPrologueLengthFixup + 5);

					Writer.UnsafeWriteBytes(_newRegion,
						Reader.UnsafeReadBytes(funcAddress, (uint)optPrologueLengthFixup));

					byte[] jmpToOriginal = Assembler.Assemble(new List<string>()
						{
							"use32",
							$"jmp 0x{(funcAddress.ToInt32() + optPrologueLengthFixup):X8}"
						}, _newRegion.ToInt32() + optPrologueLengthFixup);

					Writer.UnsafeWriteBytes(_newRegion + optPrologueLengthFixup, jmpToOriginal);

					int nopsNeeded = optPrologueLengthFixup - 5;
					List<byte> jmpToMiddleCave = Assembler.Assemble(new List<string>()
					{
						"use32",
						$"jmp 0x{_middleCave.ToInt32():X8}"
					}, funcAddress.ToInt32()).ToList();

					for (int n = 0; n < nopsNeeded;n++)
						jmpToMiddleCave.Add(0x90);

					// write jmp
					Writer.UnsafeWriteBytes(funcAddress, jmpToMiddleCave.ToArray());

					return _newRegion;
				}

				return optPrologueLengthFixup < 5 ? IntPtr.Zero : HookSet(funcAddress, hkAddress, 5);
			}

			private class Detour
			{
				public enum DetourState
				{
					Enabled = 1,
					Disabled = 2,
					ExceptionEncountered = 3
				}

				private IntPtr _detourStartLocation;
				private uint _detourStartLocationBytesOverwrittenCount;
				private byte[] _detourStartLocationBytesOverwritten;

				private IntPtr _detourCodeCaveLocation;
				private byte[] _detourCodeCaveAsm;
				private List<string> _detourCodeCaveAsmMnemonics;

				public DetourState CurrentDetourState
				{
					get => CurrentDetourState;
					private set { }
				}

				public Detour(IntPtr detourLocation, uint numBytesOverWrite, List<string> codeCaveMnemonics, int codecaveMnemonicsRebaseOrigin = -1)
				{
					if (detourLocation == IntPtr.Zero || numBytesOverWrite < 1) throw new InvalidOperationException($"{nameof(detourLocation)} and {nameof(numBytesOverWrite)} cannt be zero!");

					_detourStartLocation = detourLocation;
					_detourStartLocationBytesOverwrittenCount = numBytesOverWrite;
					_detourStartLocationBytesOverwritten = Memory.Reader.UnsafeReadBytes(_detourStartLocation, _detourStartLocationBytesOverwrittenCount);

					_detourCodeCaveAsm = codecaveMnemonicsRebaseOrigin != -1 ? Assembler.Assemble(codeCaveMnemonics, codecaveMnemonicsRebaseOrigin) : Assembler.Assemble(codeCaveMnemonics);
					_detourCodeCaveAsmMnemonics = codeCaveMnemonics;
				}

				public void SetState(DetourState newState = DetourState.Enabled)
				{
					if (CurrentDetourState == newState) return;

					switch (newState)
					{
						case DetourState.Enabled:
							Enable();
							break;
						case DetourState.Disabled:
							Disable();
							break;
						default:
							return;
					}

					CurrentDetourState = newState;
				}

				private void Enable()
				{
					// 32bit only
					_detourCodeCaveLocation = Memory.AllocateMemory((uint)_detourCodeCaveAsm.Length + 5 /* plus 5 for jmp out */, PInvoke.MemoryProtectionFlags.ExecuteReadWrite);
					if (_detourStartLocation == IntPtr.Zero) throw new InvalidOperationException($"Failed allocating {_detourCodeCaveAsm.Length + 5} bytes for the codecave");

					uint nopsNeeded = _detourStartLocationBytesOverwrittenCount - 5;
					List<Byte> jumpInBytes = Memory.Assembler.Assemble(new List<string>()
					{
						"use32",
						$"jmp {_detourCodeCaveLocation}"
					}, _detourStartLocation.ToInt32()).ToList();

					for (int n = 0; n < nopsNeeded;n++)
						jumpInBytes.Add(0x90);


					Writer.UnsafeWriteBytes(_detourCodeCaveLocation, _detourCodeCaveAsm, true);
					Writer.UnsafeWriteBytes(_detourCodeCaveLocation + _detourCodeCaveAsm.Length, 
						Assembler.Assemble(new List<string>()
						{
							"use32",
							$"jmp {_detourStartLocation.ToInt32() + _detourStartLocationBytesOverwrittenCount}"
						}, _detourCodeCaveLocation.ToInt32() + _detourCodeCaveAsm.Length), true);

					Writer.UnsafeWriteBytes(_detourStartLocation, jumpInBytes.ToArray(), true);
					CurrentDetourState = DetourState.Enabled;
				}

				private void Disable()
				{
					Writer.UnsafeWriteBytes(_detourStartLocation, _detourStartLocationBytesOverwritten, true);
					FreeMemory(_detourCodeCaveLocation, 0);
					CurrentDetourState = DetourState.Disabled;
				}
			}
		}

		public class Protection
		{
			public static bool SetPageProtection(IntPtr baseAddress, int size, PInvoke.MemoryProtectionFlags newProtection, out PInvoke.MemoryProtectionFlags oldProtection)
			{
				bool res = PInvoke.VirtualProtect(baseAddress, size, newProtection, out var oldProtect);
				oldProtection = oldProtect;
				return res;
			}

			public static PInvoke.MEMORY_BASIC_INFORMATION GetPageProtection(IntPtr baseAddress)
			{
				int res = PInvoke.VirtualQuery(baseAddress, out PInvoke.MEMORY_BASIC_INFORMATION buff, (uint)Marshal.SizeOf<PInvoke.MEMORY_BASIC_INFORMATION>());
				return buff;
			}
		}

		public class Threads
		{
			public static void SuspendProcess(string moduleExclude = "MyInjectableLibrary.dll")
			{
				var process = Process.GetCurrentProcess(); // throws exception if process does not exist
				ProcessModule ourModule = process.FindProcessModule(moduleExclude);
				IntPtr start = IntPtr.Zero;
				IntPtr end = IntPtr.Zero;
				
				if (ourModule != null)
				{
					start = ourModule.BaseAddress;
					end = ourModule.BaseAddress + ourModule.ModuleMemorySize;
				}


				foreach (ProcessThread pT in process.Threads)
				{
					if (start == IntPtr.Zero || end == IntPtr.Zero)
					{
						IntPtr pOpenThread = PInvoke.OpenThread(PInvoke.ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);
						if (pOpenThread == IntPtr.Zero)
							continue;

						PInvoke.SuspendThread(pOpenThread);
						PInvoke.CloseHandle(pOpenThread);
					}
					else
					{
						if (pT.StartAddress.ToInt32() < start.ToInt32() || pT.StartAddress.ToInt32() > end.ToInt32())
						{
							IntPtr pOpenThread = PInvoke.OpenThread(PInvoke.ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);
							if (pOpenThread == IntPtr.Zero)
								continue;

							PInvoke.SuspendThread(pOpenThread);
							PInvoke.CloseHandle(pOpenThread);
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
					IntPtr pOpenThread = PInvoke.OpenThread(PInvoke.ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

					if (pOpenThread == IntPtr.Zero)
					{
						continue;
					}

					var suspendCount = 0;
					do
					{
						suspendCount = PInvoke.ResumeThread(pOpenThread);
					} while (suspendCount > 0);

					PInvoke.CloseHandle(pOpenThread);
				}
			}
		}
	}
}
