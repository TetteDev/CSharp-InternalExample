using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MyInjectableLibrary
{
	public class HookDelegates
	{
		#region CreateMutexA
		public static CreateMutexDelegate origCreateMutexA;

		[UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
		public delegate IntPtr CreateMutexDelegate(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName);
		#endregion

		#region GetLocalPlayer
		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public unsafe delegate int GET_LOCAL_PLAYER_DELEGATE(uint* thiscall);

		public static GET_LOCAL_PLAYER_DELEGATE origGet_Local_Player;
		#endregion

		#region MoveTo
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] // This need to match the unmanaged function calling convention
		public delegate void MoveToDelegate(
			[MarshalAs(UnmanagedType.R4)]
			float x,

			[MarshalAs(UnmanagedType.R4)]
			float y);
		#endregion

		#region InternetOpenA
		[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public delegate IntPtr INTERNET_OPEN_A(
			string lpszAgent,
			uint dwAccessType,
			string lpszProxy,
			string lpszProxyBypass,
			ushort dwFlags);

		public static INTERNET_OPEN_A origInternetOpenA;
		#endregion

		#region Module32_Next
		[StructLayout(LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
		public struct MODULEENTRY32
		{
			internal uint dwSize;
			internal uint th32ModuleID;
			internal uint th32ProcessID;
			internal uint GlblcntUsage;
			internal uint ProccntUsage;
			internal IntPtr modBaseAddr;
			internal uint modBaseSize;
			internal IntPtr hModule;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
			internal string szModule;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			internal string szExePath;
		}

		[DllImport("kernel32.dll")]
		public static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

		public delegate bool MODULE32NEXT_DELEGATE(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

		public static MODULE32NEXT_DELEGATE origModule32Next;
		#endregion

		#region SetNextState
		[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
		public delegate byte SETNEXTSTATE_DELEGATE(int _this, int a2);

		public static SETNEXTSTATE_DELEGATE origSetNextState;

		public static byte SetNextState_Hook(int _this, int a2)
		{
			Console.WriteLine($"Inside SetNextState:\n" +
			                  $"	_this = 0x{_this:X8}\n" +
			                  $"	a2 = 0x{a2:X8}");


			return origSetNextState(_this, a2);
		}
		#endregion

		#region OpenUI
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate byte OPENUI_DELEGATE([MarshalAs(UnmanagedType.I1)] ExampleForm.WindowType windowId = ExampleForm.WindowType.TeleportInterface, int unkParam1 = 0, short unkParam2 = 0);

		public static OPENUI_DELEGATE origOpenUI;
		#endregion

		#region AK Specific Structs

		[StructLayout(LayoutKind.Explicit)]
		public unsafe struct FishingWnd
		{
			[FieldOffset(0x204)]
			public uint* hookedFishPtr;

			[FieldOffset(0x21C)]
			public readonly FishingState FishingState;

			[FieldOffset(0x21E)]
			public readonly byte unkByte; // Possible another state of some sort?

			[FieldOffset(0x23C)]
			public float CurrentDurability;

			[FieldOffset(0x24c)]
			public float RangeMin;

			[FieldOffset(0x250)]
			public float RangeMax;

			[FieldOffset(0x26C)]
			public float LineValue;
		}



		[StructLayout(LayoutKind.Explicit)]
		public unsafe struct CurrentMapOld
		{
			// Token: 0x0400005F RID: 95
			[FieldOffset(4)]
			public readonly short Name;

			// Token: 0x04000060 RID: 96
			[FieldOffset(6)]
			public readonly short mapID;

			// Token: 0x04000061 RID: 97
			[FieldOffset(36)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] UnkStr1;

			// Token: 0x04000062 RID: 98
			[FieldOffset(8)]
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
			public readonly string mapName1;

			// Token: 0x04000063 RID: 99
			[FieldOffset(36)]
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
			public readonly string mapName2;

			// Token: 0x04000064 RID: 100
			[FieldOffset(84)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] Model;

			// Token: 0x04000065 RID: 101
			[FieldOffset(112)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] Scene;

			// Token: 0x04000066 RID: 102
			[FieldOffset(140)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] MapImagePvp;

			// Token: 0x04000067 RID: 103
			[FieldOffset(168)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] MapImageNoPvp;

			// Token: 0x04000068 RID: 104
			[FieldOffset(196)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] BGM1;

			// Token: 0x04000069 RID: 105
			[FieldOffset(224)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] BGM2;

			// Token: 0x0400006A RID: 106
			[FieldOffset(252)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] BGM3;

			// Token: 0x0400006B RID: 107
			[FieldOffset(280)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] BGM4;

			// Token: 0x0400006C RID: 108
			[FieldOffset(316)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] UnkStr10;

			// Token: 0x0400006D RID: 109
			[FieldOffset(344)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] Description;

			// Token: 0x0400006E RID: 110
			[FieldOffset(444)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] LoadingScreenName;

			// Token: 0x0400006F RID: 111
			[FieldOffset(480)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] DungeonEntryImage;

			// Token: 0x04000070 RID: 112
			[FieldOffset(508)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
			public byte[] DungeonTransferImage;
		}

		[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi)]
		public unsafe struct CurrentMap
		{
			[FieldOffset(0x6)] public readonly short MapID;
			[FieldOffset(0x8)] private fixed byte MapNamePtr1[16];
			[FieldOffset(0x28)] private fixed byte MapNamePtr2[16];
			
			public string MapName1
			{
				get {
					fixed (byte* strBuff = MapNamePtr1)
						return *strBuff == 0x0 ? "n/a" : Marshal.PtrToStringAnsi(new IntPtr(strBuff));
				}
				private set { }
			}
			public string MapName2
			{
				get
				{
					fixed (byte* strBuff = MapNamePtr2)
						return *strBuff == 0x0 ? "n/a" : Marshal.PtrToStringAnsi(new IntPtr(strBuff));
				}
				private set { }
			}
		}

		[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi)]
		public unsafe struct EntityInfo
		{
			[FieldOffset(0x8)] public readonly uint CurrentHealth;//8
			[FieldOffset(0xC)] public readonly uint Cash;//C
			[FieldOffset(0x10)] public readonly uint Level;//10
			[FieldOffset(0x14)] public readonly float MovementSpeed; // 0x14
			[FieldOffset(0x24)] public readonly uint MaxHealth;
			[FieldOffset(0x130)] private fixed byte CharacterNamePtr[16];
			[FieldOffset(0x554)] public void* InventoryPtr;

			public unsafe CurrentMap* CurrentMap => (CurrentMap*)*(uint*)(Main.CurrentMapBase);

			public bool IsInGame
			{
				get
				{
					if (Main.CurrentMapBase == 0) return false;
					if (CurrentMap != null && CurrentMap->MapID > 0 && CurrentMap->MapID != 0x63 /* Loading Screen */)
					{
						Entity* locPlayer = (Entity*)Main.LocalPlayerBase;
						if (locPlayer != null && Main.LocalPlayerBase != 0)
						{
							return locPlayer->ptrEntityInfoStruct != null &&
							       locPlayer->model != null &&
							       locPlayer->actor != null &&
							       locPlayer->ptrEntityInfoStruct->CharacterName != "";
						}
					}

					return false;
				}
				private set { }
			}

			public string CharacterName
			{
				get
				{
					fixed (byte* strBuff = CharacterNamePtr)
						return *strBuff == 0x0 ? "n/a" : Marshal.PtrToStringAnsi(new IntPtr(strBuff));
				}
				private set
				{
					if (Main.LocalPlayerBase == 0) return;
					if (value.Length > 2147483645) return;
					byte[] str_bytes = Encoding.Default.GetBytes(value);
					if (str_bytes.Length < 1) return;

					Entity* ptr = (Entity*)Main.LocalPlayerBase;
					Memory.Writer.UnsafeWriteBytes(new IntPtr((uint*)ptr->ptrEntityInfoStruct) + 0x130,
						str_bytes,
						true);
				}
			}
			public string CharacterTitle
			{
				get
				{
					uint baseAddress = Memory.Reader.UnsafeReadMultilevelPointer<uint>(Main.ThisProcess.MainModule.BaseAddress + 0x18DC544, 0x14, 0x64, 0x10, 0x10 /*, 0x16C */);
					//fixed (byte* strBuff = baseAddress+0x88)
					//return *strBuff == 0x0 ? "n/a" : Marshal.PtrToStringAnsi(new IntPtr(strBuff));
					return Memory.Reader.UnsafeReadString(new IntPtr(baseAddress + 0x88), Encoding.Default, 32);
				}
			}

			public uint GetInventoryAccessPtr() => (uint)InventoryPtr;
			public InventoryBag* GetInventoryBag(uint bagId, InventoryType inventoryType, uint lpINVENTORY_ACCESS_FUNCTION)
			{
				if (lpINVENTORY_ACCESS_FUNCTION == 0x0) return null;
				uint thisPtr = GetInventoryAccessPtr();
				if (thisPtr == 0) return null;

				return (InventoryBag*)Memory.Functions.ExecuteAssembly(new List<string>
				{
					"use32",
					$"mov ecx, {thisPtr}",
					$"push {bagId}",
					$"push {inventoryType}",
					$"call {lpINVENTORY_ACCESS_FUNCTION}"
				}, true);
			}
			public uint GetInventorySize(uint lpInventory_Access_Function)
			{
				uint count = 0;
				for (uint i = 0; i < 15; ++i)
				{
					InventoryBag* bag = GetInventoryBag(i, InventoryType.IT_BackPack, lpInventory_Access_Function);
					if (bag != null)
						count += bag->GetItemCount();
				}

				return count;
			}
			public bool GetInventoryEmptySlots(out List<uint> slotIds, InventoryType inventoryType, uint lpInventory_Access_Function)
			{
				if (lpInventory_Access_Function == 0)
					throw new InvalidOperationException($"{nameof(lpInventory_Access_Function)} cannot be 0");

				uint count = 0;
				List<uint> returnList = new List<uint>();

				for (uint i = 0; i <= 15; ++i)
				{
					InventoryBag* bag = GetInventoryBag(i, inventoryType, lpInventory_Access_Function);
					if (bag != null)
					{
						uint newCount = bag->GetItemCount();
						for (uint j = 0; j < newCount; ++j)
						{
							InventoryItem* item = bag->GetItem(j);
							if (item == null)
								returnList.Add(count + j);
						}

						count += newCount;
					}
				}

				slotIds = returnList;
				return true;
			}

			public Jumpstate GetJumpState
			{
				get
				{
					uint baseAddress = Memory.Reader.UnsafeReadMultilevelPointer<uint>(Main.ThisProcess.MainModule.BaseAddress + 0x18DC544, 0x14, 0x64, 0x10, 0x10 /*, 0x16C */);
					return Memory.Reader.UnsafeRead<Jumpstate>(new IntPtr(baseAddress + 0x194));
				}
				private set { }
			}

			public float MovementSpeedMultiplier
			{
				get => Unsafe.Read<float>((void*)0x012FFF48);
				set => Memory.Writer.UnsafeWrite(new IntPtr(0x012FFF48), value, true);
			}

			public PInvoke.Vector3 Location3D
			{
				get
				{
					uint baseAddress = Memory.Reader.UnsafeReadMultilevelPointer<uint>(Main.ThisProcess.MainModule.BaseAddress + 0x18DC544, 0x14, 0x64, 0x10, 0x10 /*, 0x16C */);
					return Memory.Reader.UnsafeRead<PInvoke.Vector3>(new IntPtr(baseAddress + 0x16C), false);
				}
				set => TeleportTo(value);
			}
			private void TeleportTo(PInvoke.Vector3 location3d)
			{
				if (location3d.IsEmpty) return;
				uint baseAddress = Memory.Reader.UnsafeReadMultilevelPointer<uint>(Main.ThisProcess.MainModule.BaseAddress + 0x18DC544, 0x14, 0x64, 0x10, 0x10 /*, 0x16C */);
				Memory.Writer.UnsafeWrite(new IntPtr(baseAddress + 0x16C), location3d, true);
			}

			public HookDelegates.MoveToDelegate MoveTo
			{
				private set { }
				get
				{
					IntPtr address = Memory.Pattern.FindPatternSingle(Main.ThisProcess.MainModule,
						"55 8b ec e8 ?? ?? ?? ?? 8b c8 e8 ?? ?? ?? ?? 85 c0 74 07", true);
					return address == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<HookDelegates.MoveToDelegate>(address);
				}
			}

			public void SetMovementSpeed(float value)
			{
				Entity* ptr = (Entity*)Main.LocalPlayerBase;
				if (ptr == null) return;
				Memory.Writer.UnsafeWrite(new IntPtr((uint*)ptr->ptrEntityInfoStruct) + 0x14, value, true);
			}
			public void ResetMovementSpeedMultiplier()
			{
				MovementSpeedMultiplier = 0.01f;
			}
		};

		public unsafe struct InventoryBag
		{
			private uint unk1;
			public uint bagID;
			private InventoryItem** begin;
			private InventoryItem** end;

			public uint GetItemCount()
			{
				return (((uint)end - (uint)begin) >> 2);
			}

			public InventoryItem* GetItem(uint index)
			{
				return (index < GetItemCount()) ? begin[index] : null;
			}
		}

		public unsafe struct InventoryItem // size = 0x150
		{
			public uint itemID;
			private fixed byte unk[0x14C];
		};
		public unsafe struct Entity
		{
			private uint unk1; // 0x0
			private uint unk2; // 0x4
			public uint EntityID; // 0x8
			public EntityInfo* ptrEntityInfoStruct;
			public void* model; // 0x10
			private void* unkPtr1; // 0x14
			private void* unkPtr2; // 0x18
			private fixed byte unk3[10]; // 10 bytes
			public uint TypeID;
			public void* actor;
		}

		public enum InventoryType { IT_BackPack = 0, IT_Equipment = 1, IT_Bank = 4, IT_BackPack_Bags = 5, IT_EudemonInventory = 6 };
		public enum Jumpstate
		{
			OnGround = 0,
			Ascending = 1,
			Descending = 2,
		}

		[StructLayout(LayoutKind.Explicit)]
		public unsafe struct MainPlayerInfo
		{
			[FieldOffset(0x554)]
			public void* ptrInventory;
		}

		public static unsafe Entity* GetLocalPlayer(uint targetingcollectionsbase, uint getlocalplayerfn)
		{
			IntPtr alloc = Memory.AllocateMemory(0x10000);
			if (alloc == IntPtr.Zero) throw new Exception("failed alloc");

			byte[] get_local_player_asm = Memory.Assembler.Assemble(new List<string>()
			{
				"use32",
				$"org {alloc.ToInt32()}", // automatically recalculate any absolute addresses to relative (for call instructions or jumps)
			    $"mov eax, {targetingcollectionsbase}",
				"mov ecx, [ds:eax]",
				"test ecx, ecx",
				"jz @out",
				$"call {getlocalplayerfn}",
				"@out:",
				"retn",
			});

			Memory.Writer.UnsafeWriteBytes(alloc, get_local_player_asm);

			IntPtr t = PInvoke.CreateThread(IntPtr.Zero, 0, alloc, IntPtr.Zero, 0, out IntPtr threadID);
			if (t == IntPtr.Zero) throw new Exception("failed create thread");

			var result = PInvoke.WaitForSingleObject(t, 0xFFFFFFFF);
			bool res = PInvoke.GetExitCodeThread(t, out uint resultPtr);
			if (!res) throw new Exception("failed get exit code");

			PInvoke.VirtualFree(alloc, 0, PInvoke.FreeType.Release);
			PInvoke.CloseHandle(t);

			Entity* localPlayer = (Entity*)resultPtr;
			return localPlayer->EntityID == 0 ? null : localPlayer;
		}

		public static void SetNextState(uint fishingWndAddress)
		{
			if (fishingWndAddress == 0) return;
			if (Main.CatchProcess == 0)
			{
				// error
				return;
			}
			IntPtr _alloc = Memory.AllocateMemory(6u);
			if (_alloc == IntPtr.Zero) return;
			
			try
			{
				Memory.Functions.ExecuteAssembly(new List<string>()
				{
					"use32",
					$"push {_alloc}",
					$"mov ecx, {fishingWndAddress}",
					$"call {Main.CatchProcess}",
					"retn",
				}, true);
			}
			finally
			{
				bool _free = Memory.FreeMemory(_alloc);
				if (!_free)
					Console.WriteLine($"[WARNING] SetNextState - Freeing of _alloc failed");
			}
		}

		public static void StopFishing(uint fishingWndAddress)
		{
			if (Main.StopFishing == 0)
			{
				// error
				return;
			}
			if (fishingWndAddress == 0) return;
			IntPtr _alloc = Memory.AllocateMemory(6u);
			if (_alloc == IntPtr.Zero) return;
			Memory.Writer.UnsafeWrite(_alloc, (uint) 1002u, false);

			try
			{
				Memory.Functions.ExecuteAssembly(new List<string>()
				{
					"use32",
					$"push {_alloc}",
					$"mov ecx, {fishingWndAddress}",
					$"call {Main.StopFishing}",
					"retn"
				}, true);
			}
			finally
			{
				bool _free = Memory.FreeMemory(_alloc);
				if (!_free)
					Console.WriteLine($"[WARNING] StopFishing - Freeing of _alloc failed");
			}
		}

		public static void CancelFishingAnimation()
		{
			if (Main.FishingAnimation_1 == 0 || Main.FishingAnimation_2 == 0) return;

			Memory.Functions.ExecuteAssembly(new List<string>()
			{
				"use32",
				$"call {Main.FishingAnimation_1}",
				"test al, al",
				"sete al",
				"push 1",
				"push eax",
				$"call {Main.FishingAnimation_2}",
				"add esp, 8",
				"retn"
			}, true);
		}

		public static List<Tuple<IntPtr, string>> GetAllWindowBases()
		{
			if (Main.WndInterfaceBase == 0) return null;
			List<Tuple<IntPtr, string>> results = new List<Tuple<IntPtr, string>>();

			if (Main.WndInterfaceBase == 0)
			{
				Main.WndInterfaceBase = HelperMethods.AlainFindPattern("8B 35 ? ? ? ? 74 08 81 FF ? ? ? ? 74 05 E8 ? ? ? ? 3B DE 0F 84 ? ? ? ? ", 1,
					1, HelperMethods.MemorySearchEntry.RT_READNEXT4_BYTES_RAW);
				if (Main.WndInterfaceBase == 0) return null;
			}

			IntPtr intPtr = Memory.Reader.UnsafeRead<IntPtr>((IntPtr)Main.WndInterfaceBase);
			IntPtr intPtr2 = intPtr;
			do
			{
				IntPtr intPtr3 = Memory.Reader.UnsafeRead<IntPtr>(intPtr2 + 36);
				int num = intPtr3.ToInt32();
				int num2 = 8;
				Memory.Reader.UnsafeRead<int>(new IntPtr(num + num2));
				if (intPtr3 != IntPtr.Zero)
				{
					results.Add(new Tuple<IntPtr, string>(intPtr3, Marshal.PtrToStringAnsi(intPtr3 + 8)));
				}
				intPtr2 = Memory.Reader.UnsafeRead<IntPtr>(intPtr2);
			}
			while (intPtr2 != IntPtr.Zero && intPtr2 != intPtr);

			return results;
		}

		public static List<Tuple<IntPtr, string>> GetAllWnds()
		{
			List<Tuple<IntPtr, string>> returnList = new List<Tuple<IntPtr, string>>();

			if (Main.WndInterfaceBase == 0)
			{
				Main.WndInterfaceBase = HelperMethods.AlainFindPattern("8B 35 ? ? ? ? 74 08 81 FF ? ? ? ? 74 05 E8 ? ? ? ? 3B DE 0F 84 ? ? ? ? ", 1,
					1, HelperMethods.MemorySearchEntry.RT_READNEXT4_BYTES_RAW);
				if (Main.WndInterfaceBase == 0) return returnList;
			}

			IntPtr intPtr = Memory.Reader.UnsafeRead<IntPtr>(new IntPtr(Main.WndInterfaceBase)); 
			//IntPtr intPtr = Mem.ReadMemory<IntPtr>(0x0603ECF0); // Ak.TO

			IntPtr intPtr2 = intPtr;

			do
			{
				IntPtr intPtr3 = Memory.Reader.UnsafeRead<IntPtr>(intPtr2 + 36);
				if (intPtr3 != IntPtr.Zero)
				{
					// list.Add(new MemoryProvider(intPtr3));
					string w = Memory.Reader.UnsafeReadString(intPtr3 + 0x08, Encoding.UTF7, 265);
					returnList.Add(new Tuple<IntPtr, string>(intPtr3, w));

				}
				intPtr2 = Memory.Reader.UnsafeRead<IntPtr>(intPtr2);
				if (!(intPtr2 != IntPtr.Zero))
				{
					break;
				}
			} while (intPtr2 != intPtr);

			return returnList;
		}

		public static unsafe uint GetWindowByName(string wndName, bool sloppySearch = true)
		{
			if (Main.WndInterfaceBase == 0)
			{
				Main.WndInterfaceBase = HelperMethods.AlainFindPattern("8B 35 ? ? ? ? 74 08 81 FF ? ? ? ? 74 05 E8 ? ? ? ? 3B DE 0F 84 ? ? ? ? ", 1,
					1, HelperMethods.MemorySearchEntry.RT_READNEXT4_BYTES_RAW);
				if (Main.WndInterfaceBase == 0) return UInt32.MinValue;
			}

			if (wndName == "") return uint.MinValue;
			IntPtr intPtr = *(IntPtr*)Main.WndInterfaceBase;
			IntPtr intPtr2 = intPtr;

			do
			{
				IntPtr intPtr3 = *(IntPtr*) (intPtr2 + 36);
				if (intPtr3 != IntPtr.Zero)
				{
					string currWndName = Memory.Reader.UnsafeReadString(intPtr3 + 0x08, Encoding.UTF7, 265);
					if (!sloppySearch)
						if (string.Equals(currWndName, wndName, StringComparison.CurrentCultureIgnoreCase)) return (uint)intPtr3.ToInt32();
					else
						if (currWndName.ToLower().Contains(wndName.ToLower())) return (uint)intPtr3.ToInt32();
				}

				intPtr2 = *(IntPtr*) (intPtr2.ToInt32());
				if (!(intPtr2 != IntPtr.Zero))
					break;

			} while (intPtr2 != intPtr);

			return uint.MinValue;
		}

		public static unsafe Entity* GenerateCrashAK()
		{
			Console.WriteLine($"[FORCE_EXCEPTION] ForceException was called!");

			IntPtr alloc = Memory.AllocateMemory(0x10000);
			if (alloc == IntPtr.Zero) throw new Exception("failed alloc");

			byte[] get_local_player_asm = Memory.Assembler.Assemble(new List<string>()
			{
				"use32",
				$"org {alloc.ToInt32()}", // automatically recalculate any absolute addresses to relative (for call instructions or jumps)
			    $"mov eax, {6969}",
				"mov ecx, [ds:eax]",
				"test ecx, ecx",
				"jz @out",
				$"call {1337}",
				"@out:",
				"retn",
			});

			Memory.Writer.UnsafeWriteBytes(alloc, get_local_player_asm);

			IntPtr t = PInvoke.CreateThread(IntPtr.Zero, 0, alloc, IntPtr.Zero, 0, out IntPtr threadID);
			if (t == IntPtr.Zero) throw new Exception("failed create thread");

			var result = PInvoke.WaitForSingleObject(t, 0xFFFFFFFF);
			bool res = PInvoke.GetExitCodeThread(t, out uint resultPtr);
			if (!res) throw new Exception("failed get exit code");

			PInvoke.VirtualFree(alloc, 0, PInvoke.FreeType.Release);
			PInvoke.CloseHandle(t);

			Entity* localPlayer = (Entity*)resultPtr;
			return localPlayer->EntityID == 0 ? null : localPlayer;
		}
		public static unsafe MainPlayerInfo* GetLocalPlayerInfo()
		{
			if (Main.LocalPlayerBase == 0x0) return null;
			return (MainPlayerInfo*)Main.LocalPlayerBase;
		}

		[StructLayout(LayoutKind.Explicit)]
		public unsafe struct HookedFish
		{
			[FieldOffset(0x14)]
			public readonly uint* ActualFishInfo;
		}

		public enum FishingState : byte
		{
			FISHING_IDLE = 0,
			FISHING_NO_FISH_ON_LINE = 1,
			FISHING_CAN_HOOK_FISH = 2,
			FISHING_FISH_HOOKED = 3,
			FISHING_HOOKED_FISH_CATCH_SUCCESS = 4,
			POST_FISHING_ANIMATION = 5, // Cannot determine wether the fishing was successful or not from this
		}
		#endregion

		#region AK Specific Hooks 
		public static bool Module32Next_Hook(IntPtr hSnapshot, ref MODULEENTRY32 lpme)
		{
			HookDelegates.MODULEENTRY32 entry = lpme;
			entry.dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>();

			if (!HookDelegates.origModule32Next(hSnapshot, ref entry))
			{
				//WarningMessage("Module32Next : no more entries.");
				//PInvoke.SetLastError(0x00000012); // ERROR_NO_MORE_FILES error code
				return false;
			}

			if (entry.modBaseAddr == Main.ThisModule.BaseAddress)
			{
				//WarningMessage("Module32Next tried to access this module.");
				//WarningMessage(StringFormat("Function returns to %p", _ReturnAddress()).c_str());
				Console.WriteLine($"[MODULE32NEXT_Hook] Module32Next tried accessing our injected module ...");
				return false;
			}
			return true;
		}

		public static unsafe int GetLocalPlayer_Hook(uint* _this)
		{
			int localPlayerAddressReturn = HookDelegates.origGet_Local_Player(_this); // eax
			if (localPlayerAddressReturn > 0)
			{
				if (localPlayerAddressReturn != Main.LocalPlayerBase)
				{
					Main.LocalPlayerBase = (uint)localPlayerAddressReturn;
					Console.WriteLine($"[GET_LOCAL_PLAYER] LocalPlayer address has been updated (0x{localPlayerAddressReturn:X8})");
				}
			}
			else
			{
				Main.LocalPlayerBase = 0;
				Console.WriteLine($"[GET_LOCAL_PLAYER] LocalPlayer address has been set to 0");
			}
			return HookDelegates.origGet_Local_Player(_this);
		}

		public static unsafe IntPtr CreateMutexA_Hook(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName)
		{
			Console.WriteLine($"CreateMutexA was called with \"{lpName}\" as the mutex object name");

			// TYPE - PARAMETER NAME - PARAMETER VALUE (as object)
			/*
			var parameters = new List<Tuple<Type, string, object>>
			{
				new Tuple<Type, string, object>(typeof(IntPtr), "lpMutexAttributes", lpMutexAttributes),
				new Tuple<Type, string, object>(typeof(bool), "bInitialOwner", bInitialOwner),
				new Tuple<Type, string, object>(typeof(string), "lpName", lpName)
			};
			HookDelegates.PrintHookParametersAndValues("CreateMutexA_Hook", ref parameters);
			*/


			if (lpName == "FFClientTag")
			{
				Console.WriteLine("[CreateMutexA - Hook] FFClientTag creation was intercepted and prevented!");
				return IntPtr.Zero;
			}
			return HookDelegates.origCreateMutexA(lpMutexAttributes, bInitialOwner, lpName);
		}

		public static IntPtr InternetOpenA_Hook(string lpszAgent, uint dwAccessType, string lpszProxy, string lpszProxyBypass, ushort dwFlags)
		{
			//Console.WriteLine(
			//	$"InternetConnectA_Hook Parameters\n" +
			//			$"* lpszAgent = {lpszAgent}\n" +
			//	$"* lpszProxy = {lpszProxy}\n" +
			//	$"* lpszProxyBypass = {lpszProxyBypass}\n" +
			//	$"* dwFlags = 0x{dwFlags:X}");
			return IntPtr.Zero;
		}
		#endregion

		#region AK Specific Offsets
		public const uint TARGET_ID_OFFSET = 0x154; // TargetInfoWnd

		#endregion

		public static bool SetHook(IntPtr origFuncBaseAddress, Delegate hookDelegate, int[] InclusiveACLValues, int[] ExclusiveACLValues, out EasyHook.LocalHook hookObject)
		{
			if (origFuncBaseAddress == IntPtr.Zero)
			{
				hookObject = null;
				return false;
			}

			EasyHook.LocalHook retObj;
			try
			{
				retObj = EasyHook.LocalHook.Create(origFuncBaseAddress, hookDelegate, null);
			}
			catch
			{

				hookObject = null;
				return false;
			}

			if (InclusiveACLValues != null && InclusiveACLValues.Length > 0)
				retObj.ThreadACL.SetInclusiveACL(InclusiveACLValues);

			if (ExclusiveACLValues != null && ExclusiveACLValues.Length > 0)
				retObj.ThreadACL.SetExclusiveACL(ExclusiveACLValues);

			hookObject = retObj;
			return true;
		}

		public static void PrintHookParametersAndValues(string functionName, ref List<Tuple<Type,string,object>> parameters)
		{
			Console.WriteLine($"[Hooked Method Parameters - {(functionName.Length < 1 ? "NO_FUNCTION_NAME_SPECIFIED" : functionName)}]");
			if (parameters != null && parameters.Count < 0)
			{
				int n = 0;
				foreach (Tuple<Type, string, object> paramObj in parameters)
				{
					Console.WriteLine(n == parameters.Count - 1
						? $"	* {paramObj.Item1.ToString()} {paramObj.Item2} = {(Type) paramObj.Item3}\n"
						: $"	* {paramObj.Item1.ToString()} {paramObj.Item2} = {(Type) paramObj.Item3}");
					n++;
				}
			}
			else
				Console.WriteLine($"	*=== NO PARAMETERS AVAILABLE / PASSED TO PrintHookParametersAndValues() METHOD ===*\n");
		}
	}
}
