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

		#region TopLevelException 

		public static TOP_LEVEL_EXCEPTION_FILDER_DELEGATE origTop_Level_Exception_Filter;

		[StructLayout(LayoutKind.Sequential)]
		public unsafe struct _EXCEPTION_POINTERS
		{
			public _EXCEPTION_RECORD* ExceptionRecord;
			public CONTEXT* ContextRecord;
		}

		public unsafe struct _EXCEPTION_RECORD
		{
			public uint ExceptionCode;
			public uint ExceptionFlags;
			public _EXCEPTION_RECORD* ExceptionRecord;
			public void* ExceptionAddress;
			public uint NumberParameters;
			//public ulong* ExceptionInformation[0];
		}

		public unsafe struct CONTEXT
		{
			public uint ContextFlags;
			public uint Cpsr;

			/*
			 union {
				struct {
				  DWORD64 X0;
				  DWORD64 X1;
				  DWORD64 X2;
				  DWORD64 X3;
				  DWORD64 X4;
				  DWORD64 X5;
				  DWORD64 X6;
				  DWORD64 X7;
				  DWORD64 X8;
				  DWORD64 X9;
				  DWORD64 X10;
				  DWORD64 X11;
				  DWORD64 X12;
				  DWORD64 X13;
				  DWORD64 X14;
				  DWORD64 X15;
				  DWORD64 X16;
				  DWORD64 X17;
				  DWORD64 X18;
				  DWORD64 X19;
				  DWORD64 X20;
				  DWORD64 X21;
				  DWORD64 X22;
				  DWORD64 X23;
				  DWORD64 X24;
				  DWORD64 X25;
				  DWORD64 X26;
				  DWORD64 X27;
				  DWORD64 X28;
				  DWORD64 Fp;
				  DWORD64 Lr;
				} DUMMYSTRUCTNAME;
				DWORD64 X[31];
				} DUMMYUNIONNAME;
				DWORD64          Sp;
				DWORD64          Pc;
				ARM64_NT_NEON128 V[32];
				DWORD            Fpcr;
				DWORD            Fpsr;
				DWORD            Bcr[ARM64_MAX_BREAKPOINTS];
				DWORD64          Bvr[ARM64_MAX_BREAKPOINTS];
				DWORD            Wcr[ARM64_MAX_WATCHPOINTS];
				DWORD64          Wvr[ARM64_MAX_WATCHPOINTS];
			 */
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate long TOP_LEVEL_EXCEPTION_FILDER_DELEGATE(_EXCEPTION_POINTERS* ExceptionInfo);
		#endregion

		#region sub_597950
		// Search "System Crash!!" string IDA (game.bin)

		//[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		//public unsafe delegate int SUB_597950_DELEGATE(int a1, char* format, char argList);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		[return: MarshalAs(UnmanagedType.I4)]
		public delegate int SUB_597950_DELEGATE(int a1, [MarshalAs(UnmanagedType.LPStr)] string format, [MarshalAs(UnmanagedType.U1)] byte argList);

		public static unsafe SUB_597950_DELEGATE origSUB_597950;
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

		#region IsDebuggerPresent
		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public delegate bool ISDEBUGGERPRESENT_DELEGATE();

		public static ISDEBUGGERPRESENT_DELEGATE origIsDebuggerPresent;

		[return: MarshalAs(UnmanagedType.Bool)]
		public static bool IsDebuggerPresent_Hook()
		{
			Console.WriteLine("[IS_DEBUGGER_PRESENT_Hook] We're inside IsDebuggerPresent hook, returning false ...");
			return false;
		}
		#endregion

		#region CheckRemoteDebuggerPresent
		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public unsafe delegate bool CHECKREMOTEDEBUGGERPRESENT_DELEGATE(IntPtr hProcess, bool* pbDebuggerPresent);

		public static unsafe CHECKREMOTEDEBUGGERPRESENT_DELEGATE origCheckRemoteDebuggerPresent;

		[return: MarshalAs(UnmanagedType.Bool)]
		public static unsafe bool CheckRemoteDebuggerPresent_Hook(IntPtr hProcess, bool* pbDebuggerPresent)
		{
			Console.WriteLine("[CHECK_REMOTE_DEBUGGER_PRESENT_Hook] We're inside CheckRemoteDebuggerPresent hook");
			return origCheckRemoteDebuggerPresent(hProcess, pbDebuggerPresent);
		}
		#endregion

		#region AK Specific Structs
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
			public HookDelegates.MoveToDelegate MoveTo => Marshal.GetDelegateForFunctionPointer<HookDelegates.MoveToDelegate>(new IntPtr(0x008D1060));

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
					Main.LocalPlayerBase = localPlayerAddressReturn;
					Console.WriteLine($"[GET_LOCAL_PLAYER] LocalPlayer address has been updated!");
					PInvoke.SetWindowText(Main.ThisProcess.MainWindowHandle, $"Localplayer: 0x{Main.LocalPlayerBase:X8}");
				}
			}
			else
			{
				Main.LocalPlayerBase = 0;
				Console.WriteLine($"[GET_LOCAL_PLAYER] LocalPlayer address has been set to 0");
				PInvoke.SetWindowText(Main.ThisProcess.MainWindowHandle, $"Localplayer: 0x0");
			}
			return HookDelegates.origGet_Local_Player(_this);
		}

		public static unsafe IntPtr CreateMutexA_Hook(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName)
		{
			Console.WriteLine($"CreateMutexA was called with \"{lpName}\" as the mutex object name");

			// TYPE - PARAMETER NAME - PARAMETER VALUE (as object)
			var parameters = new List<Tuple<Type, string, object>>
			{
				new Tuple<Type, string, object>(typeof(IntPtr), "lpMutexAttributes", lpMutexAttributes),
				new Tuple<Type, string, object>(typeof(bool), "bInitialOwner", bInitialOwner),
				new Tuple<Type, string, object>(typeof(string), "lpName", lpName)
			};
			HookDelegates.PrintHookParametersAndValues("CreateMutexA_Hook", ref parameters);


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

		public static unsafe long Top_Level_Exception_Filter_Hook(HookDelegates._EXCEPTION_POINTERS* ExceptionInfo)
		{
			MessageBox.Show($"We caught an exception! (Ptr to exception address: 0x{(uint)ExceptionInfo->ExceptionRecord->ExceptionAddress:X8})");
			Console.WriteLine("Hello from inside exception handler");
			if (Debugger.IsAttached) Debugger.Break();
			return HookDelegates.origTop_Level_Exception_Filter(ExceptionInfo);
		}

		public static int sub_597950_Hook(int a1, [MarshalAs(UnmanagedType.LPStr)] string format, [MarshalAs(UnmanagedType.U1)] byte argList)
		{
			Console.WriteLine("inside sub_597950_Hook");
			//Console.WriteLine($"Inside sub_597950_Hook - Parameters\n" +
			//                  $"                         int a1 = {a1} (hex: 0x{a1:X})\n" +
			//                  $"                         string format = {format}\n" +
			//                  $"                         byte argList = {argList.ToString()} (hex: 0x{argList:X})\n");

			return HookDelegates.origSUB_597950(a1, format, argList);
		}
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
