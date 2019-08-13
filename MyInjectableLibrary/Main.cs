using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using RGiesecke.DllExport;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using hazedumper;

namespace MyInjectableLibrary
{
    public static class Main
    {
	    public static Process ThisProcess = Process.GetCurrentProcess();

	    public static Form1 ThisForm;
	    public static Thread FormStarter;

	    public static ProcessModule ClientDLL;
	    public static ProcessModule EngineDLL;

	    public static Thread Reader;
	    public static Thread Trigger;

	    public static int LocalPlayer = -1;
	    public static int ClientState = -1;
	    public static int EnemyCrosshairBase = -1;
	    public static int GlobalVars = -1;


	    public static LocalPlayer_t LocalPlayerStruct;
	    public static ClientState_t ClientStateStruct;
	    public static GlobalVars_t GlobalVarsStruct;

	    [DllExport("DllMain", CallingConvention.Cdecl)] // Mark your members with this attribute to export them; if you don't - they won't get exported!
	    public static void EntryPoint() // Note, member name does not have to match export name ("DllMain" in this case).
	    {
			ThisProcess = Process.GetCurrentProcess();
			//FormStarter = new Thread(StartForm);
			//FormStarter.Start();

			if (!DebugConsole.InitiateDebugConsole())
		    {
			    File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "hello_from_" + ThisProcess.ProcessName + ".txt"), $"Failed allocating console!");
				return;
		    }

		    try
		    {
			    ClientDLL = ThisProcess.FindProcessModule("client_panorama.dll");
			    EngineDLL = ThisProcess.FindProcessModule("engine.dll");
			    if (ClientDLL == null || EngineDLL == null)
			    {
					Console.WriteLine("Failed getting client_panorama.dll base or engine.dll base...");
			    }
			    else
			    {
				    Console.WriteLine($"client_panorama.dll: 0x{ClientDLL.BaseAddress.ToInt32():X8}");
					Reader = new Thread(ReaderLoop);
				    Reader.SetApartmentState(ApartmentState.MTA);
				    Reader.Start();

					Trigger = new Thread(TriggerLoop);
					Trigger.Start();
			    }
		    }
		    catch (Exception e)
		    {
				Console.WriteLine(e.Message);
		    }
	    }

	    

		public static unsafe void TriggerLoop()
	    {
		    while (true)
		    {
			    if (ClientState == -1 || ClientState == 0)
			    {
				    Thread.Sleep(25);
				    continue;
			    }

			    if (ClientStateStruct.GameState == GameState.MENU)
			    {
				    Thread.Sleep(25);
				    continue;
				}

			    if (LocalPlayerStruct.Health < 1)
			    {
				    Thread.Sleep(25);
				    continue;
				}

			    EnemyCrosshairBase = *(int*) (ClientDLL.BaseAddress.ToInt32() + signatures.dwEntityList + (LocalPlayerStruct.CrosshairID - 1) * 0x10);

				if (EnemyCrosshairBase == 0 || EnemyCrosshairBase == -1)
			    {
				    Thread.Sleep(25);
				    continue;
				}

			    Enemy_Crosshair_t str = *(Enemy_Crosshair_t*)(EnemyCrosshairBase);
			    
			    if (!str.Dormant && str.Health > 0 && str.Team != LocalPlayerStruct.Team && !LocalPlayerStruct.Dormant && LocalPlayerStruct.LifeState == LifeState.Alive)
					*(int*)(ClientDLL.BaseAddress.ToInt32() + signatures.dwForceAttack) = 6;
			}
	    }

	    public static unsafe void ReaderLoop()
	    {
			Console.WriteLine($"ReaderLoop method has been executed (Thread Started)!");
		    while (true)
		    {
			    try
			    {
				    if (ClientDLL == null || EngineDLL == null)
				    {
					    Console.Title = $"Localplayer: 0x0 (Cannot find 'client_panorama.dll')";
					    Thread.Sleep(25);
					    continue;
				    }

				    ClientState = *(int*) (EngineDLL.BaseAddress.ToInt32() + signatures.dwClientState);
				    ClientStateStruct = *(ClientState_t*) ClientState;

				    GlobalVarsStruct = *(GlobalVars_t*) (EngineDLL.BaseAddress.ToInt32() + signatures.dwGlobalVars);

					if (ClientStateStruct.GameState == GameState.GAME)
					{
						LocalPlayer = *(int*) (ClientDLL.BaseAddress.ToInt32() + signatures.dwLocalPlayer);

						if (LocalPlayer == 0 || LocalPlayer == -1)
						{
							Console.Title = $"Localplayer: 0x0 (LocalPlayer returned zero)";
							Thread.Sleep(25);
							continue;
						}

						LocalPlayerStruct = *(LocalPlayer_t*) (LocalPlayer);

						Console.Title = $"Localplayer: 0x{LocalPlayer:X8}";
						Thread.Sleep(25);
					}
					else
					{
						Console.Title = "Player is not ingame!";
						LocalPlayer = -1;
						Thread.Sleep(25);
					}

			    }
			    catch (Exception e)
			    {
					Console.WriteLine(e.Message);
			    }
		    }
	    }

		public static void StartForm()
	    {
		    ThisForm = new Form1();
		    ThisForm.ShowDialog();
	    }
    }
}
