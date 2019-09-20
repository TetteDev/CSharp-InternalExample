using System;
using System.Collections.Generic;
using System.Diagnostics;
using RGiesecke.DllExport;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

	    public static uint LocalPlayerBase = 0;

	    public static uint TargetingCollectionsBase = 0;
	    public static uint InventoryAccessFunction = 0;
	    public static uint DetourMainLoopOffset = 0;
	    public static uint DetourCrashHandlerOffset = 0;
	    public static uint CurrentMapBase = 0;
	    public static uint GetLocalPlayer = 0;
	    public static uint WndInterfaceBase = 0;
	    public static uint OpenUIFunction = 0;

		// Fishing
	    public static uint CatchProcess = 0;
	    public static uint StopFishing = 0;
	    public static uint FishingAnimation_1 = 0;
	    public static uint FishingAnimation_2 = 0;

	    public static List<PatternEntry> PatternList = new List<PatternEntry>();

		public static void PopulatePatternListAndScan(bool printResults = false)
		{
			if (PatternList == null) PatternList = new List<PatternEntry>();
			if (PatternList.Count > 0) PatternList.Clear();

			PatternList.Add(new PatternEntry("TargetingCollectionsBase", "68 50 06 00 00", true, 0x30, 1));
			PatternList.Add(new PatternEntry("InventoryAccessFunction", "55 8B EC 8B 55 08 33 C0 83 FA 0D"));
			PatternList.Add(new PatternEntry("DetourMainLoopOffset", "FF 80 BE 08 01", true, 0x1, 1, HelperMethods.MemorySearchEntry.RT_LOCATION, true, new byte[] { 0x80, 0xBE, 0x08, 0x01, 0x00, 0x00, 0x00 }));
			PatternList.Add(new PatternEntry("CurrentMapBase", "C0 74 0D 83 3D", true, -0xA, 1, HelperMethods.MemorySearchEntry.RT_ADDRESS));
			PatternList.Add(new PatternEntry("GetLocalPlayer", "C0 89 87 04 01", true, -0x5, 1, HelperMethods.MemorySearchEntry.RT_REL_ADDRESS));
			PatternList.Add(new PatternEntry("WndInterfaceBase", "8B 35 ? ? ? ? 74 08 81 FF ? ? ? ? 74 05 E8 ? ? ? ? 3B DE 0F 84 ? ? ? ?", true, 1, 1, HelperMethods.MemorySearchEntry.RT_READNEXT4_BYTES_RAW));
			PatternList.Add(new PatternEntry("OpenUIFunction", "E8 ? ? ? ? 83 C4 14 EB 7F", true, 0, 1, HelperMethods.MemorySearchEntry.RT_READNEXT4_BYTES));
			PatternList.Add(new PatternEntry("CatchProcess", "55 8B EC 64 A1 ? ? ? ? 6A FF 68 ? ? ? ? 50 8B 45 08 64 89 25 ? ? ? ? 83 EC 38 53 33 DB 39 58 1C 56 8B F1 0F 85 ? ? ? ? 0F B7 86 ? ? ? ? 2B C3", false));
			PatternList.Add(new PatternEntry("StopFishing", "55 8B EC 64 A1 ? ? ? ? 6A FF 68 ? ? ? ? 50 64 89 25 ? ? ? ? 81 EC ? ? ? ? 56 8B F1 8B 4D 08 8B 01 05 ? ? ? ? 83 F8 49", false));
			PatternList.Add(new PatternEntry("FishingAnimation_1", "E8 ? ? ? ? 84 C0 5E 5B", true, 0, 1, HelperMethods.MemorySearchEntry.RT_READNEXT4_BYTES, false));
			PatternList.Add(new PatternEntry("FishingAnimation_2", "E8 ? ? ? ? 53 E8 ? ? ? ? 8B 4E 50", true, 0, 1, HelperMethods.MemorySearchEntry.RT_READNEXT4_BYTES, false));
			PatternList.Add(new PatternEntry("DetourCrashHandlerOffset", "4D EC 51 6A 05", true, -0x56, 1, HelperMethods.MemorySearchEntry.RT_LOCATION, true, new byte[]{ 0x64, 0xA1, 0x00, 0x00, 0x00, 0x00} ));


			Stopwatch ts = new Stopwatch();
			ts.Start();
			Parallel.ForEach(PatternList, (currentPattern) =>
			{
				switch (currentPattern.Identifier)
				{
					case "StopFishing":
						currentPattern.Scan(ref StopFishing);
						break;
					case "CatchProcess":
						currentPattern.Scan(ref CatchProcess);
						break;
					case "OpenUIFunction":
						currentPattern.Scan(ref OpenUIFunction);
						break;
					case "WndInterfaceBase":
						currentPattern.Scan(ref WndInterfaceBase);
						break;
					case "GetLocalPlayer":
						currentPattern.Scan(ref GetLocalPlayer);
						break;
					case "CurrentMapBase":
						currentPattern.Scan(ref CurrentMapBase);
						break;
					case "DetourMainLoopOffset":
						currentPattern.Scan(ref DetourMainLoopOffset);
						break;
					case "InventoryAccessFunction":
						currentPattern.Scan(ref InventoryAccessFunction);
						break;
					case "TargetingCollectionsBase":
						currentPattern.Scan(ref TargetingCollectionsBase);
						break;
					case "FishingAnimation_1":
						currentPattern.Scan(ref FishingAnimation_1);
						break;
					case "FishingAnimation_2":
						currentPattern.Scan(ref FishingAnimation_2);
						break;
					case "DetourCrashHandlerOffset":
						currentPattern.Scan(ref DetourCrashHandlerOffset);
						break;
					default:
						Console.WriteLine($"Encountered a pattern entry with no destination variable, it is only retrievable via GetAddressFromIdentifier(string) now ...");
						break;
				}
			});

			if (printResults)
			{
				foreach (var pattern in PatternList)
					Console.WriteLine($"* {(pattern.Identifier == "" ? "N/A" : pattern.Identifier)}: 0x{pattern.ScanResult:X8}");
			}

			Console.WriteLine($"\n[Pattern Manager] Scanned {PatternList.Count} patterns in {ts.ElapsedMilliseconds} ms\n");
		}

		[DllExport("DllMain", CallingConvention.Cdecl)] // Mark your members with this attribute to export them; if you don't - they won't get exported!
	    public static unsafe void EntryPoint() // Note, member name does not have to match export name ("DllMain" in this case).
	    {
		    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			ThisProcess = Process.GetCurrentProcess();
		    ThisModule = ThisProcess.FindProcessModule("MyInjectableLibrary.dll");

			if (!DebugConsole.InitiateDebugConsole())
				MessageBox.Show("Failed initiating the debugging console, please restart the program as admin!", "Debugging Console Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);

			PopulatePatternListAndScan(true);

			#region Setting up some function delegates
			if (OpenUIFunction != 0) HookDelegates.origOpenUI = Marshal.GetDelegateForFunctionPointer<HookDelegates.OPENUI_DELEGATE>(new IntPtr(OpenUIFunction));
			#endregion

			bool enableModule32Hook = false;
			bool enableCreateMutexAHook = false;
			bool enableGetLocalPlayerHook = false; // AK Specific
			bool enableInternetOpenAHook = false; // AK Specific;

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
				if (GetLocalPlayer != 0)
				{
					HookDelegates.origGet_Local_Player = Marshal.GetDelegateForFunctionPointer<HookDelegates.GET_LOCAL_PLAYER_DELEGATE>(new IntPtr(GetLocalPlayer));
					bool getLocalPlayer_HookResult = HookDelegates.SetHook(new IntPtr(GetLocalPlayer), new HookDelegates.GET_LOCAL_PLAYER_DELEGATE(HookDelegates.GetLocalPlayer_Hook), null, new[] { 0 }, out var hookObject_GetLocalPlayerHook);
					Console.WriteLine(getLocalPlayer_HookResult ? $"[Hook Manager] Successfully hooked function GET_LOCAL_PLAYER (0x{GetLocalPlayer:X8})" : "[Hook Manager] Could not hook function GET_LOCAL_PLAYER");
				}
				else
					Console.WriteLine("[Pattern] Pattern for GetLocalPlayer function is outdated!");
			}
			else
			{
				if (MessageBox.Show("Hook for GetLocalPlayer was set to false, do you want to update localplayer address manually?\n\n" +
				                    "Please keep in mind if you're going to press YES, only do it when you're ingame!", "Please Read!", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
				{
					if (TargetingCollectionsBase == 0)
						TargetingCollectionsBase = HelperMethods.AlainFindPattern("68 50 06 00 00", 0x30, 1);

					if (GetLocalPlayer == 0)
						GetLocalPlayer = HelperMethods.AlainFindPattern("C0 89 87 04 01", -0x5, 1, HelperMethods.MemorySearchEntry.RT_REL_ADDRESS);

					if (TargetingCollectionsBase == 0 || GetLocalPlayer == 0)
					{
						MessageBox.Show("Patterns for TargetingCollectionsBase and/or GetLocalPlayer is outdated!\n" +
						                "Please contact the developer!", "Exception Encountered", MessageBoxButtons.OK, MessageBoxIcon.Error);
						Environment.Exit(-1);
					}

					LocalPlayerBase = (uint)HookDelegates.GetLocalPlayer(TargetingCollectionsBase, GetLocalPlayer);
					Console.Title = $"Localplayer: 0x{LocalPlayerBase:X8} - Character Name: {((HookDelegates.Entity*) LocalPlayerBase)->ptrEntityInfoStruct->CharacterName}";
				} else 
					Environment.Exit(-1);
				
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

			DebugFormThread = new Thread(InitiateDebugForm) { IsBackground = true };
			DebugFormThread.Start();
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
