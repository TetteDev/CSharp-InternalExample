using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Reloaded.Assembler;
using static MyInjectableLibrary.PInvoke;
using static hazedumper.signatures;
using static hazedumper.netvars;
using static MyInjectableLibrary.Structs;
using static MyInjectableLibrary.Enums;
using System.Numerics;

namespace MyInjectableLibrary
{
    public static unsafe class Main
    {
	    public static Process ThisProcess = Process.GetCurrentProcess();
	    public static ProcessModule ThisModule = ThisProcess.FindProcessModule("MyInjectableLibrary.dll");

		public static ExampleForm DebugForm; // Unused
	    public static Thread DebugFormThread; // Unused

		public delegate int GetPlayerInCrosshair();
		public static GetPlayerInCrosshair GetPlayerEntInCrosshair;


		[StructLayout(LayoutKind.Explicit)]
		public struct PlayerEnt
		{
			[FieldOffset(0x4)]
			public Vector3 HeadPosition;

			[FieldOffset(0xF8)]
			public int Health;

			[FieldOffset(0xFC)]
			public int Armor;

			[FieldOffset(0x34)]
			public Vector3 Position;

			[FieldOffset(0x40)]
			public float Yaw;

			[FieldOffset(0x44)]
			public float Pitch;

			[FieldOffset(0x48)]
			public float Roll;

			[FieldOffset(0x69)]
			public bool IsOnGround;

			[FieldOffset(0x0150)]
			public int Ammo;

			[FieldOffset(0x032C)]
			public int TeamNum;

			[FieldOffset(0x01A0)]
			public int ShotsFired;

			[FieldOffset(0x224)]
			[MarshalAs(UnmanagedType.I1)]
			public bool Attacking;
		}


		public static PlayerEnt* GetLocalPlayer()
		{
			return *(PlayerEnt**)(0x400000 + 0x10F4F4);
		}

	    [DllExport("DllMain", CallingConvention.Cdecl)]
	    public static unsafe void EntryPoint()
	    {
		    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			ThisProcess = Process.GetCurrentProcess();
		    ThisModule = ThisProcess.FindProcessModule("MyInjectableLibrary.dll");

			if (!DebugConsole.InitiateDebugConsole())
				MessageBox.Show("Failed initiating the debugging console, please restart the program as admin!", 
					"Debugging Console Exception", 
					MessageBoxButtons.OK, 
					MessageBoxIcon.Error);

			var t = Memory.Assembler.Assemble(new List<string>()
			{
				"use32",
				"xor eax, eax",
				"nop",
				"retn"
			});

			MessageBox.Show(t.Length.ToString());

			Console.ReadLine();
	    }


		public static void AimbotLoop()
	    {
		    PlayerEnt* locPlayer = GetLocalPlayer();
		    D3DMATRIX* viewMatrix = (D3DMATRIX*)0x501AE8;

			while (true)
			{
				int currentPlayer = GetPlayerEntInCrosshair();
				if (currentPlayer != 0)
				{
					PlayerEnt* targettedEntity = (PlayerEnt*) currentPlayer;
					if (targettedEntity->Health < 1 || locPlayer->Health < 1)
						continue;

					if (targettedEntity->TeamNum == locPlayer->TeamNum)
						continue;

					bool w2s_3dPos = targettedEntity->Position.World2Screen(viewMatrix->AsArray(), out var screenPos);
					if (w2s_3dPos)
					{
						bool w2s_headPos = targettedEntity->HeadPosition.World2Screen(viewMatrix->AsArray(), out var screenPos_head);
						if (w2s_headPos)
						{
							// draw
							string name = Memory.Reader.UnsafeReadString(new IntPtr((uint)targettedEntity + 0x0225), Encoding.UTF8, 16);
							Console.WriteLine($"{name} - X: {screenPos_head.X}, Y: {screenPos_head.Y}");
						}
					}

					Thread.Sleep(500);
				}
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
	    public static void Callback(IntPtr structPtr)
	    {
		    Registers* reg = (Registers*)structPtr;
		    Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] Example Callback was executed!");
			
	    }

	    public static unsafe uint FindPattern(string moduleName, string pattern, string mask, bool absResult = true)
	    {
		    if (pattern.Length < 1) return 0;
		    if (moduleName == "") return 0;

		    pattern = pattern.Trim();
		    mask = mask.Trim();

		    ProcessModule pm = ThisProcess.FindProcessModule(moduleName);
		    if (pm == null) return 0;

		    for (int i = 0; i < pm.ModuleMemorySize - pattern.Length; i++)
		    {
			    bool found = true;
			    for (int j = 0; j < pattern.Length; j++)
					found &= mask[j] == '?' || pattern[j] == *(char*)(pm.BaseAddress + i + j);

			    if (found)
				    return absResult ? (uint) (pm.BaseAddress + i) : (uint) i;
		    }

		    return 0;
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
			SetLastError((uint)obj.HResult);
			Debugger.Break();
		}
    }
}
