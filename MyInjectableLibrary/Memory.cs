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
using System.Windows.Forms.VisualStyles;

//using Reloaded;

namespace MyInjectableLibrary
{
	public class Memory
	{
		public static IntPtr AllocateMemory(uint size, PInvoke.MemoryProtectionFlags memoryProtection = PInvoke.MemoryProtectionFlags.ExecuteReadWrite) =>
			size < 1 ? IntPtr.Zero : PInvoke.VirtualAlloc(IntPtr.Zero, new UIntPtr(size), PInvoke.AllocationTypeFlags.Commit | PInvoke.AllocationTypeFlags.Reserve, PInvoke.MemoryProtectionFlags.ExecuteReadWrite);

		public static bool FreeMemory(IntPtr baseAddress, uint optionalSize = 0) 
			=> baseAddress != IntPtr.Zero && PInvoke.VirtualFree(baseAddress, optionalSize, PInvoke.FreeType.Release);

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
				if (startRange == stopRange) return IntPtr.Zero;


				List<PInvoke.MEMORY_BASIC_INFORMATION> regionsToScan = new List<PInvoke.MEMORY_BASIC_INFORMATION>();
				for (uint address = startRange; address < stopRange;)
				{
					var virtualQuery = PInvoke.VirtualQuery(new IntPtr(address), out PInvoke.MEMORY_BASIC_INFORMATION mbi, (uint) Marshal.SizeOf<PInvoke.MEMORY_BASIC_INFORMATION>());
					if (virtualQuery != 0)
					{
						if (mbi.State == PInvoke.MemoryState.MEM_COMMIT && !(mbi.Protect.HasFlag(PInvoke.MemoryProtectionFlags.Guard)))
						{
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
				if (mnemonics == null || mnemonics.Count < 1) return 0;
				IntPtr alloc = AllocateMemory(0x10000);
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
			public static uint ExecuteAssembly(byte[] asm)
			{
				if (asm == null || asm.Length < 1) return uint.MinValue;
				IntPtr alloc = IntPtr.Zero;
				alloc = AllocateMemory(asm.Length < 0x1000 ? (uint) 0x1000 : (uint) 0x10000);

				if (alloc == IntPtr.Zero) throw new Exception("failed allocating codecave");

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

					if (origin == -1)
						mnemonics.Insert(originInsertIndex, $"org {origin}");

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
			public class X64RunPE
			{
				public  static unsafe void InjectAndRun(byte[] payloadBuffer, string pathHost, string optionalArgs = "")
				{
					if (payloadBuffer == null || payloadBuffer.Length < 1) return;
					if (!File.Exists(pathHost)) return;

					int e_lfanew = 0;
					int sizeOfImage = 0;
					int sizeOfHeaders = 0;
					int entryPoint = 0;

					short numberOfSections = 0;
					short sizeOfOptionalHeader = 0;
					long imageBase = 0;

					fixed (byte* pBuffer = payloadBuffer)
					{
						e_lfanew = *(int*) (pBuffer + 0x3c);
						sizeOfImage = *(int*)(pBuffer + e_lfanew + 0x8 + 0x038);
						sizeOfHeaders = *(int*)(pBuffer + e_lfanew + 0x8 + 0x03c);
						entryPoint = *(int*)(pBuffer + e_lfanew + 0x8 + 0x10);

						numberOfSections = *(short*) (pBuffer + e_lfanew + 0x4 + 0x2);
						sizeOfOptionalHeader = *(short*)(pBuffer + e_lfanew + 0x4 + 0x10);
						imageBase = *(short*)(pBuffer + e_lfanew + 0x18 + 0x18);
					}

					byte[] bStartupInfo = new byte[0x68];
					byte[] bProcessInfo = new byte[0x18];

					IntPtr pThreadContext = Allocate(0x4d0, 16);

					string target_host = pathHost;
					if (!string.IsNullOrEmpty(optionalArgs))
						target_host += " " + optionalArgs;
					string currentDirectory = Directory.GetCurrentDirectory();

					*(int*) (pThreadContext.ToInt32() + 0x30) = 0x0010001b;

					PInvoke.CreateProcess(null, target_host, IntPtr.Zero, IntPtr.Zero, true, 0x4u, IntPtr.Zero, currentDirectory, bStartupInfo, bProcessInfo);

					long processHandle = 0;
					long threadHandle = 0;

					fixed (byte* pBuff = bProcessInfo)
					{
						processHandle = *(long*) (pBuff + 0x0);
						threadHandle = *(long*)(pBuff + 0x8);
					}

					PInvoke.ZwUnmapViewOfSection(processHandle, imageBase);
					PInvoke.VirtualAlloc(new IntPtr(imageBase), new UIntPtr((uint)sizeOfImage), (PInvoke.AllocationTypeFlags)0x3000, (PInvoke.MemoryProtectionFlags)0x40);

					Memory.Writer.UnsafeWriteBytes(new IntPtr(imageBase), payloadBuffer, false);

					for (short i = 0; i < numberOfSections; i++)
					{
						byte[] section = new byte[0x28];
						Buffer.BlockCopy(payloadBuffer, e_lfanew + (0x18 + sizeOfOptionalHeader) + (0x28 * i), section, 0, 0x28);

						fixed (byte* pBuff = section)
						{
							int virtualAddress = *(int*) (pBuff + 0xC);
							int sizeOfRawData = *(int*) (pBuff + 0x10);
							int pointerToRawData = *(int*)(pBuff + 0x14);

							byte[] bRawData = new byte[sizeOfRawData];
							Buffer.BlockCopy(payloadBuffer, pointerToRawData, bRawData, 0, bRawData.Length);

							Memory.Writer.UnsafeWriteBytes(new IntPtr(imageBase + virtualAddress), bRawData, false);
						}
					}

					PInvoke.GetThreadContext(threadHandle, pThreadContext);

					byte[] bImageBase = BitConverter.GetBytes(imageBase);

					long rdx = *(long*) (pThreadContext + 0x88);
					Memory.Writer.UnsafeWriteBytes(new IntPtr(rdx + 16), bImageBase, false);

					*(long*) (pThreadContext.ToInt32() + 0x80) = imageBase + entryPoint; 

					PInvoke.SetThreadContext(threadHandle, pThreadContext);
					PInvoke.ResumeThread(new IntPtr(threadHandle));

					Marshal.FreeHGlobal(pThreadContext);
					PInvoke.CloseHandle(new IntPtr(processHandle));
					PInvoke.CloseHandle(new IntPtr(threadHandle));
				}

				public static unsafe void InjectAndRunOriginal(byte[] payloadBuffer, string host, string args)
				{
					if (payloadBuffer == null || payloadBuffer.Length < 1) return;

					int e_lfanew = Marshal.ReadInt32(payloadBuffer, 0x3c);
					int sizeOfImage = Marshal.ReadInt32(payloadBuffer, e_lfanew + 0x18 + 0x038);
					int sizeOfHeaders = Marshal.ReadInt32(payloadBuffer, e_lfanew + 0x18 + 0x03c);
					int entryPoint = Marshal.ReadInt32(payloadBuffer, e_lfanew + 0x18 + 0x10);

					short numberOfSections = Marshal.ReadInt16(payloadBuffer, e_lfanew + 0x4 + 0x2);
					short sizeOfOptionalHeader = Marshal.ReadInt16(payloadBuffer, e_lfanew + 0x4 + 0x10);
					long imageBase = Marshal.ReadInt64(payloadBuffer, e_lfanew + 0x18 + 0x18);


					byte[] bStartupInfo = new byte[0x68];
					byte[] bProcessInfo = new byte[0x18];

					IntPtr pThreadContext = Allocate(0x4d0, 16);

					string target_host = host;
					if (!string.IsNullOrEmpty(args))
						target_host += " " + args;
					string currentDirectory = Directory.GetCurrentDirectory();

					Marshal.WriteInt32(pThreadContext, 0x30, 0x0010001b);

					PInvoke.CreateProcess(null, target_host, IntPtr.Zero, IntPtr.Zero, true, 0x4u, IntPtr.Zero, currentDirectory, bStartupInfo, bProcessInfo);
					long processHandle = Marshal.ReadInt64(bProcessInfo, 0x0);
					long threadHandle = Marshal.ReadInt64(bProcessInfo, 0x8);

					PInvoke.ZwUnmapViewOfSection(processHandle, imageBase);
					PInvoke.VirtualAlloc(new IntPtr(imageBase), new UIntPtr((uint)sizeOfImage), (PInvoke.AllocationTypeFlags)0x3000, (PInvoke.MemoryProtectionFlags)0x40);

					Memory.Writer.UnsafeWriteBytes(new IntPtr(imageBase), payloadBuffer, false);

					for (short i = 0; i < numberOfSections; i++)
					{
						byte[] section = new byte[0x28];
						Buffer.BlockCopy(payloadBuffer, e_lfanew + (0x18 + sizeOfOptionalHeader) + (0x28 * i), section, 0, 0x28);

						int virtualAddress = Marshal.ReadInt32(section, 0x00c);
						int sizeOfRawData = Marshal.ReadInt32(section, 0x010);
						int pointerToRawData = Marshal.ReadInt32(section, 0x014);

						byte[] bRawData = new byte[sizeOfRawData];
						Buffer.BlockCopy(payloadBuffer, pointerToRawData, bRawData, 0, bRawData.Length);

						Memory.Writer.UnsafeWriteBytes(new IntPtr(imageBase + virtualAddress), bRawData, false);
					}

					PInvoke.GetThreadContext(threadHandle, pThreadContext);

					byte[] bImageBase = BitConverter.GetBytes(imageBase);

					long rdx = Marshal.ReadInt64(pThreadContext, 0x88);
					Memory.Writer.UnsafeWriteBytes(new IntPtr(rdx + 16), bImageBase, false);

					Marshal.WriteInt64(pThreadContext, 0x80 /* rcx */, imageBase + entryPoint);

					PInvoke.SetThreadContext(threadHandle, pThreadContext);
					PInvoke.ResumeThread(new IntPtr(threadHandle));

					Marshal.FreeHGlobal(pThreadContext);
					PInvoke.CloseHandle(new IntPtr(processHandle));
					PInvoke.CloseHandle(new IntPtr(threadHandle));
				}
				private static IntPtr Allocate(int size, int alignment)
				{
					IntPtr allocated = Marshal.AllocHGlobal(size + (alignment / 2));
					return Align(allocated, alignment);
				}
				private static IntPtr Align(IntPtr source, int alignment)
				{
					long source64 = source.ToInt64() + (alignment - 1);
					long aligned = alignment * (source64 / alignment);
					return new IntPtr(aligned);
				}
			}
		}

		private class HooksCustom
		{
			public unsafe void* SetHookOrig(void* baseAddress, void* hookAddress, out byte[] originalBytes)
			{
				PInvoke.MemoryProtectionFlags oldProtect;
				byte* newRegion = (byte*) PInvoke.VirtualAlloc(IntPtr.Zero, new UIntPtr(10), PInvoke.AllocationTypeFlags.Commit | PInvoke.AllocationTypeFlags.Reserve, PInvoke.MemoryProtectionFlags.ExecuteReadWrite);
				// if (newregion == 0) we failed

				// set function to be hooked protection to rwx
				bool virtualProtectFunctionPrologue = PInvoke.VirtualProtect(new IntPtr(baseAddress), 5, PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out oldProtect);

				// copy prologue from original function to our allocated region
				byte[] origBytes = Reader.UnsafeReadBytes(new IntPtr(baseAddress), 5);
				Writer.UnsafeWriteBytes(new IntPtr(newRegion), origBytes);

				byte* t = (byte*) baseAddress;
				*t = 0xe9;
				t++;

				*(int*) t = ((int) hookAddress - (int) (t) - 4);
				bool virtualProtectFunctionPrologueRestore = PInvoke.VirtualProtect(new IntPtr(baseAddress), 5, oldProtect, out var discard);

				t = newRegion + 5;
				*t = 0xe9;
				t++;

				*(int*) t = ((int) baseAddress - (int) t + 1);

				bool newRegionVirtualProtectRX = PInvoke.VirtualProtect(new IntPtr(newRegion), 10, PInvoke.MemoryProtectionFlags.ExecuteRead, out discard);

				originalBytes = origBytes;
				return (void*) newRegion;
			}

			public static unsafe T SetHook<T>(void* baseAddress, void* hookAddress, out byte[] originalBytes)
			{
				if ((uint) baseAddress == 0x0 || (uint) hookAddress == 0x0) throw new InvalidOperationException($"SetHook<T>(void* {nameof(baseAddress)}, void* {nameof(hookAddress)})" +
				                                                                                                $"\nParameter {nameof(baseAddress)} = 0x{(uint)baseAddress:X8}\n" +
				                                                                                                $"Parameter {nameof(hookAddress)} = 0x{(uint)hookAddress:X8}");
				// alloc new space for the trampoline
				// 5 bytes for the the copied function prologue, and 5 bytes for the jmp back to the original
				// byte* newregion = (byte*) VirtualAlloc(0, 10, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
				byte* newRegion = (byte*)PInvoke.VirtualAlloc(IntPtr.Zero, new UIntPtr(10), PInvoke.AllocationTypeFlags.Commit | PInvoke.AllocationTypeFlags.Reserve, PInvoke.MemoryProtectionFlags.ExecuteReadWrite);
				if ((uint) newRegion == 0x0) throw new InvalidOperationException($"Failed allocating memory (parameter {nameof(newRegion)}, size requested: 10 bytes)");
				
				// unprotect prologue of our function that should be hooked
				// VirtualProtect(baseAddr, 5, PAGE_EXECUTE_READWRITE, &oldprotect);
				bool virtualProtectFunctionPrologue = PInvoke.VirtualProtect(new IntPtr(baseAddress), 5, PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out var oldProtect);

				// memcpy( newregion, baseAddr, 5);
				byte[] origBytes = Reader.UnsafeReadBytes(new IntPtr(baseAddress), 5);
				if (origBytes.Length == 0 || origBytes == null) throw new InvalidOperationException($"Failed reading orignal bytes");
				Writer.UnsafeWriteBytes(new IntPtr(newRegion), origBytes);

				byte* t = (byte*)baseAddress;
				*t = 0xE9; // jmp
				t++;

				//jmp relative to our function
				*(uint*)t = (uint)hookAddress - (uint)t - 4;

				// restore prologues protection
				// VirtualProtect(baseAddr, 5, oldprotect, &oldprotect);
				if (virtualProtectFunctionPrologue)
					PInvoke.VirtualProtect(new IntPtr(baseAddress), 5, oldProtect, out var discard_1);

				t = newRegion + 5;
				*t = 0xE9;
				t++;

				// jmp relative back to original function
				*(uint*)t = (uint)baseAddress - (uint)t + 1;

				// we have to set protection to PAGE_EXECUTE_READ
				// VirtualProtect(newregion, 10, PAGE_EXECUTE_READ, 0);
				bool newRegionVirtualProtectRX = PInvoke.VirtualProtect(new IntPtr(newRegion), 10, PInvoke.MemoryProtectionFlags.ExecuteRead, out var discard_2);

				originalBytes = origBytes;

				// this is the pointer to function that we will call from the hook
				return Functions.GetFunction<T>(new IntPtr((void*) newRegion));
			}

			

			public static unsafe void UnsetHook(void* addr, void* original, byte[] originalBytes)
			{
				if (originalBytes == null || originalBytes.Length < 1) throw new InvalidOperationException("Cannot unsethook as originalbytes array was null or empty!");
				// get functions address
				PInvoke.MemoryProtectionFlags oldprotect;
				PInvoke.VirtualProtect(new IntPtr(addr), 5, PInvoke.MemoryProtectionFlags.ExecuteReadWrite, out oldprotect);

				Writer.UnsafeWriteBytes(new IntPtr(addr), originalBytes);

				PInvoke.VirtualProtect(new IntPtr(addr), 5, oldprotect, out oldprotect);

				// Also free
				PInvoke.VirtualFree(new IntPtr(original), 10, PInvoke.FreeType.Release);
				original = null;
			}
		}

		public class Dumper
		{
			public static void DumpEntireProcess()
			{

			}

			public static void DumpProcessModule(ProcessModule targetProcessModule)
			{

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

					
					int numBytesBeforeJmpOut = _tmpAsm.Length; /* +  register dump asm byte array length; */
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
	}
}
