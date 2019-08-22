using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using RGiesecke.DllExport;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Reloaded.Assembler;
using static MyInjectableLibrary.PInvoke;

namespace MyInjectableLibrary
{
    public static class Main
    {
	    public static Process ThisProcess = Process.GetCurrentProcess();
	    public static ProcessModule ThisModule = ThisProcess.FindProcessModule("MyInjectableLibrary.dll");

	    public static ExampleForm DebugForm;
	    public static Thread DebugFormThread;

	    public static int LocalPlayerBase = 0;
	    public static uint TargetingCollectionsBase = 0;
	    public static uint InventoryAccessFunction = 0;
	    public static uint DetourMainLoopOffset = 0;


	    [DllExport("DllMain", CallingConvention.Cdecl)] // Mark your members with this attribute to export them; if you don't - they won't get exported!
	    public static unsafe void EntryPoint() // Note, member name does not have to match export name ("DllMain" in this case).
	    {
		    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			ThisProcess = Process.GetCurrentProcess();
		    ThisModule = ThisProcess.FindProcessModule("MyInjectableLibrary.dll");
			//DebugFormThread = new Thread(InitiateDebugForm) {IsBackground = true};
			//DebugFormThread.Start();
			
			if (!DebugConsole.InitiateDebugConsole())
			{
				MessageBox.Show("Failed initiating the debugging console!", "Debugging Console Exception", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
		    }

			TargetingCollectionsBase = HelperMethods.AlainFindPattern("68 50 06 00 00", 0x30, 1);
			InventoryAccessFunction = HelperMethods.AlainFindPattern("FF 5E 5D C2 14", -0xD, 2, HelperMethods.MemorySearchEntry.RT_REL_ADDRESS, true);
			DetourMainLoopOffset = HelperMethods.AlainFindPattern("FF 80 BE 08 01", 0x1, 1, HelperMethods.MemorySearchEntry.RT_LOCATION, true, new byte[] { 0x80, 0xBE, 0x08, 0x01, 0x00, 0x00, 0x00 });

	
			bool enableModule32Hook = false;
			bool enableCreateMutexAHook = false;
			bool enableGetLocalPlayerHook = false; // AK Specific
			bool enableTopLevelExceptionFilterHook = false;
			bool enableSub_597950Hook = false; // AK Specific
			bool enableInternetOpenAHook = false; // AK Specific
			bool enableIsDebuggerPresentHook = false;
			bool enableCheckRemoteDebuggerPresentHook = false;

			#region Module32_Next Hook
			if (enableModule32Hook)
			{
				IntPtr module32_NextBaseAddress = PInvoke.GetProcAddress(PInvoke.GetModuleHandle("Kernel32.dll"), "Module32Next");
				if (module32_NextBaseAddress != IntPtr.Zero)
				{
					HookDelegates.origModule32Next = Marshal.GetDelegateForFunctionPointer<HookDelegates.MODULE32NEXT_DELEGATE>(module32_NextBaseAddress);
					bool Module32NextHook = HookDelegates.SetHook(module32_NextBaseAddress, new HookDelegates.MODULE32NEXT_DELEGATE(HookDelegates.Module32Next_Hook), null, new[] { 0 }, out var hookObject_Module32NextHook);
					Console.WriteLine(Module32NextHook ? $"[Hook Manager] Successfully hooked function Module32Next (0x{module32_NextBaseAddress.ToInt32():X8})" : "[Hook Manager] Could not hook function Module32Next");
				}
				else
					Console.WriteLine("[Pattern] Pattern for Module32Next function is outdated!");
			}
			#endregion

			#region CreateMutexA Hook
			if (enableCreateMutexAHook)
			{
				IntPtr createMutexABaseAddress = PInvoke.GetProcAddress(PInvoke.GetModuleHandle("Kernel32.dll"), "CreateMutexA");
				if (createMutexABaseAddress != IntPtr.Zero)
				{
					HookDelegates.origCreateMutexA = Marshal.GetDelegateForFunctionPointer<HookDelegates.CreateMutexDelegate>(createMutexABaseAddress);
					bool createMutexA_Hook = HookDelegates.SetHook(createMutexABaseAddress, new HookDelegates.CreateMutexDelegate(HookDelegates.CreateMutexA_Hook), null, new[] { 0 }, out var hookObject_CreateMutexAHook);
					Console.WriteLine(createMutexA_Hook ? $"[Hook Manager] Successfully hooked function CreateMutexA (0x{createMutexABaseAddress.ToInt32():X8})" : "[Hook Manager] Could not hook function CreateMutexA");
				}
				else
					Console.WriteLine("[Pattern] Pattern for CreateMutexA function is outdated!");
			}
			#endregion

			#region GetLocalPlayer Hook
			if (enableGetLocalPlayerHook)
			{
				var GetLocalPlayerOffset = HelperMethods.AlainFindPattern("C0 89 87 04 01", -0x5, 2, HelperMethods.MemorySearchEntry.RT_REL_ADDRESS);
				if (GetLocalPlayerOffset != 0)
				{
					HookDelegates.origGet_Local_Player = Marshal.GetDelegateForFunctionPointer<HookDelegates.GET_LOCAL_PLAYER_DELEGATE>(new IntPtr(GetLocalPlayerOffset));
					bool getLocalPlayer_HookResult = HookDelegates.SetHook(new IntPtr(GetLocalPlayerOffset), new HookDelegates.GET_LOCAL_PLAYER_DELEGATE(HookDelegates.GetLocalPlayer_Hook), null, new[] { 0 }, out var hookObject_GetLocalPlayerHook);
					Console.WriteLine(getLocalPlayer_HookResult ? $"[Hook Manager] Successfully hooked function GET_LOCAL_PLAYER (0x{GetLocalPlayerOffset:X8})" : "[Hook Manager] Could not hook function GET_LOCAL_PLAYER");
				}
				else
					Console.WriteLine("[Pattern] Pattern for GetLocalPlayer function is outdated!");
			}
			#endregion

			#region TopLevelExceptionFilter Hook
			if (enableTopLevelExceptionFilterHook)
			{
				var DetourCrashHandlerOffset = Memory.Pattern.FindPatternSingle(ThisProcess.MainModule, "68 d4 2f 41 01 ff ?? 30 ?? ?? ?? 8d") - 0x21;
				if (DetourCrashHandlerOffset != IntPtr.Zero)
				{
					Console.Title = $"Top_Level_Exception_Filter: 0x{DetourCrashHandlerOffset.ToInt32():X8}";
					HookDelegates.origTop_Level_Exception_Filter = Marshal.GetDelegateForFunctionPointer<HookDelegates.TOP_LEVEL_EXCEPTION_FILDER_DELEGATE>(DetourCrashHandlerOffset);
					bool topLevelExceptionFilter_Hookresult = HookDelegates.SetHook(DetourCrashHandlerOffset, new HookDelegates.TOP_LEVEL_EXCEPTION_FILDER_DELEGATE(HookDelegates.Top_Level_Exception_Filter_Hook), null, new[] { 0 },
						out var obj);

					Console.WriteLine(topLevelExceptionFilter_Hookresult ? $"[Hook Manager] Successfully hooked function Top_Level_Exception_Filter (0x{DetourCrashHandlerOffset.ToInt32():X8})" : "[Hook Manager] Could not hook function Top_Level_Exception_Filter");

					// Remove this later
					if (topLevelExceptionFilter_Hookresult) HookDelegates.GenerateCrashAK();
				}
				else
					Console.WriteLine($"[Pattern] Pattern for TopLevelExceptionFilter function is outdated!");
			}
			#endregion

			#region sub_597950 Hook
			if (enableSub_597950Hook)
			{
				var sub_597950Offset = Memory.Pattern.FindPatternSingle(ThisProcess.MainModule, "55 8B EC 81 EC 00 08 00 00 68 FF 07 00 00 8D 85 01 F8 FF FF");
				if (sub_597950Offset != IntPtr.Zero)
				{
					Console.WriteLine($"sub_597950 location: 0x{sub_597950Offset.ToInt32():X8}");
					HookDelegates.origSUB_597950 = Marshal.GetDelegateForFunctionPointer<HookDelegates.SUB_597950_DELEGATE>(sub_597950Offset);
					bool sub_597950_HookResult = HookDelegates.SetHook(sub_597950Offset, new HookDelegates.SUB_597950_DELEGATE(HookDelegates.sub_597950_Hook), null, new[] { 0 }, out var hookObject_sub_597950Hook);
					Console.WriteLine(sub_597950_HookResult ? $"[Hook Manager] Successfully hooked function sub_597950" : "[Hook Manager] Could not hook function sub_597950");
				}
				else
					Console.WriteLine("[Pattern] Pattern for sub_597950 function is outdated!");
			}
			#endregion

			#region InternetOpenA Hook
			if (enableInternetOpenAHook)
			{
				IntPtr InternetConnectABase = GetProcAddress(PInvoke.GetModuleHandle("wininet.dll"), "InternetConnectA");
				if (InternetConnectABase != IntPtr.Zero)
				{
					HookDelegates.origInternetOpenA = Marshal.GetDelegateForFunctionPointer<HookDelegates.INTERNET_OPEN_A>(InternetConnectABase);
					bool InternetOpenA_HookResult = HookDelegates.SetHook(InternetConnectABase, new HookDelegates.INTERNET_OPEN_A(HookDelegates.InternetOpenA_Hook), null, new[] { 0 }, out var hookObject_InternetOpenAHook);
					Console.WriteLine(InternetOpenA_HookResult ? $"[Hook Manager] Successfully hooked function InternetOpenA" : "[Hook Manager] Could not hook function InternetOpenA");
				}
				else
					Console.WriteLine("[Pattern] Pattern for InternetOpenA function is outdated!");
			}
			#endregion

			#region IsDebuggerPresent_Hook
			if (enableIsDebuggerPresentHook)
			{
				IntPtr _isDebuggerPresentAddress = GetProcAddress(PInvoke.GetModuleHandle("Kernel32.dll"), "IsDebuggerPresent");
				if (_isDebuggerPresentAddress != IntPtr.Zero)
				{
					HookDelegates.origIsDebuggerPresent = Marshal.GetDelegateForFunctionPointer<HookDelegates.ISDEBUGGERPRESENT_DELEGATE>(_isDebuggerPresentAddress);
					bool IsDebuggerPresent_HookResult = HookDelegates.SetHook(_isDebuggerPresentAddress, new HookDelegates.ISDEBUGGERPRESENT_DELEGATE(HookDelegates.IsDebuggerPresent_Hook), null, new[] { 0 }, out var hookObject_IsDebuggerPresentHook);
					Console.WriteLine(IsDebuggerPresent_HookResult ? $"[Hook Manager] Successfully hooked function IsDebuggerPresent" : "[Hook Manager] Could not hook function IsDebuggerPresent");
				}
				else
					Console.WriteLine("[Pattern] Pattern for IsDebuggerPresent function is outdated!");
			}
			#endregion

			#region CheckRemoteDebuggerPresent_Hook
			if (enableCheckRemoteDebuggerPresentHook)
			{
				IntPtr address = GetProcAddress(PInvoke.GetModuleHandle("Kernel32.dll"), "CheckRemoteDebuggerPresent");
				if (address != IntPtr.Zero)
				{
					HookDelegates.origCheckRemoteDebuggerPresent = Marshal.GetDelegateForFunctionPointer<HookDelegates.CHECKREMOTEDEBUGGERPRESENT_DELEGATE>(address);
					bool CheckRemoteDebuggerPresent_HookResult = HookDelegates.SetHook(address, new HookDelegates.CHECKREMOTEDEBUGGERPRESENT_DELEGATE(HookDelegates.CheckRemoteDebuggerPresent_Hook), null, new[] { 0 }, out var hookObject_CheckRemoteDebuggerPresentHook);
					Console.WriteLine(CheckRemoteDebuggerPresent_HookResult ? $"[Hook Manager] Successfully hooked function CheckRemoteDebuggerPresent" : "[Hook Manager] Could not hook function CheckRemoteDebuggerPresent");
				}
				else
				{
					uint peb = Memory.Functions.ExecuteAssembly(new List<string>()
					{
						"use32",
						"mov eax, [fs:0x30]",
						"retn",
					});
					if (peb == 0)
						Console.WriteLine("[Pattern] Pattern for CheckRemoteDebuggerPresent function is outdated!");
					else
					{
						*(byte*)(peb + 2) = 0;
						Console.WriteLine("[Pattern] Pattern for CheckRemoteDebuggerPresent function is outdated but PEB+2 has been patched!");
					}
				}
			}

			#endregion
	    }

		public static void InitiateDebugForm()
	    {
		    if (DebugForm != null) return;
		    DebugForm = new ExampleForm();
		    DebugForm.ShowDialog();
	    }
		static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			HelperMethods.PrintExceptionData(e?.ExceptionObject, true);
			if (!Debugger.IsAttached) return;

			Exception obj = (Exception)e?.ExceptionObject;
			PInvoke.SetLastError((uint)obj.HResult);
			Debugger.Break();
		}
    }
}
