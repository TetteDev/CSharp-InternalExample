using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MyInjectableLibrary
{
	public unsafe partial  class ExampleForm : Form
	{
		public static HookDelegates.Entity* CurrentEntity = null;
		public static float origMovementMultiplier = 0f;
		public static bool IsAKUS = true;

		public static Thread LocalPlayerBaseUpdater;

		public static Thread FishingAutoCenter;

		public ExampleForm()
		{
			InitializeComponent();
		}

		public void OnStartup()
		{
			if (CurrentEntity == null)
			{
				Console.WriteLine($"OnStartup method could not continue as reference to localplayer was null!");
				return;
			}

			Thread walkingSpeedLoop = new Thread(FreezeMovementSpeed);
			walkingSpeedLoop.Start();
			// Set the original movementspeed multiplier as the default value for control numericUpDown1
			origMovementMultiplier = CurrentEntity->ptrEntityInfoStruct->MovementSpeed;
			numericUpDown1.Value = (decimal)origMovementMultiplier;

			label1.Text = CurrentEntity->ptrEntityInfoStruct->IsInGame ? "Is Ingame: True" : "Is Ingame: False";

			if (comboBox1.Items.Count > 0) comboBox1.Items.Clear();
			foreach (WindowType suit in (WindowType[])Enum.GetValues(typeof(WindowType)))
				if (suit != WindowType.ActionSit && suit != WindowType.ActionStand)
					comboBox1.Items.Add($"{suit}");
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			
		}

		private void ExampleForm_Load(object sender, EventArgs e)
		{
			CheckForIllegalCrossThreadCalls = false;

			if (Main.LocalPlayerBase != 0)
			{
				CurrentEntity = (HookDelegates.Entity*)Main.LocalPlayerBase;
				toolStripStatusLabel1.Text = $"Localplayer: 0x{Main.LocalPlayerBase:X8}";
				LocalPlayerBaseUpdater = new Thread(UpdateLocalPlayer);
				LocalPlayerBaseUpdater.Start();

				OnStartup();
			}
			else
			{
				CurrentEntity = null;
				toolStripStatusLabel1.Text = "Localplayer: 0x0";
			}
		}

		private void Button2_Click(object sender, EventArgs e)
		{
			
		}

		private void Button1_Click_1(object sender, EventArgs e)
		{
			
		}

		private void NumericUpDown1_ValueChanged(object sender, EventArgs e)
		{

		}

		public void FreezeMovementSpeed()
		{
			while (true)
			{
				if (CurrentEntity == null || !checkBox1.Checked)
				{
					Thread.Sleep(250);
					continue;
				}

				CurrentEntity->ptrEntityInfoStruct->SetMovementSpeed((float) numericUpDown1.Value);
				Thread.Sleep(25);
			}
		}

		public void UpdateLocalPlayer()
		{
			while (true)
			{
				if (CurrentEntity == null)
				{
					if (Main.LocalPlayerBase != 0)
					{
						CurrentEntity = (HookDelegates.Entity*)Main.LocalPlayerBase;
						Console.WriteLine($"Localplayer address updated from within UpdateLocalPlayer()");
					}
					else
					{
						CurrentEntity = HookDelegates.GetLocalPlayer(Main.TargetingCollectionsBase, Main.GetLocalPlayer);
						if (CurrentEntity == null)
						{
							Console.WriteLine($"Method UpdateLocalPlayer() failed getting new Localplayer baseaddress, waiting for 1 second then trying again ...");
							Thread.Sleep(1000);
						}
						else Console.WriteLine($"Localplayer address updated from within UpdateLocalPlayer()");
					}
				}
				
				Thread.Sleep(500);
			}
		}

		private void CheckBox1_CheckedChanged(object sender, EventArgs e)
		{
			if (!checkBox1.Checked)
			{
				if (CurrentEntity == null) return;
				numericUpDown1.Value = (decimal)origMovementMultiplier;
				CurrentEntity->ptrEntityInfoStruct->MovementSpeedMultiplier = origMovementMultiplier;
			}
		}

		private void ToolStripDropDownButton1_Click(object sender, EventArgs e)
		{

		}

		private void ToolStripDropDownButton1_TextChanged(object sender, EventArgs e)
		{
			IsAKUS = toolStripDropDownButton1.Text == "AKUS";
		}

		private void Button1_Click_2(object sender, EventArgs e)
		{
			numericUpDown1.Value = (decimal)origMovementMultiplier;
		}

		private void Button2_Click_1(object sender, EventArgs e)
		{
			if (CurrentEntity == null) return;
			bool xValid = float.TryParse(textBox1.Text, out float xLocation);
			bool yValid = float.TryParse(textBox2.Text, out float yLocation);

			if (!xValid || !yValid) return;
			HookDelegates.MoveToDelegate moveTo = CurrentEntity->ptrEntityInfoStruct->MoveTo;
			moveTo?.Invoke(xLocation, yLocation);
		}

		private void Button3_Click(object sender, EventArgs e)
		{
			if (CurrentEntity == null) return;
			bool xValid = float.TryParse(textBox4.Text, out float xLocation);
			bool yValid = float.TryParse(textBox3.Text, out float yLocation);
			bool zValid = float.TryParse(textBox5.Text, out float zLocation);

			if (!xValid || !yValid || !zValid) return;
			PInvoke.Vector3 new3DLocation = new PInvoke.Vector3 {x = xLocation, y = yLocation, z = zLocation};
			CurrentEntity->ptrEntityInfoStruct->Location3D = new3DLocation;
		}

		private void Button4_Click(object sender, EventArgs e)
		{
			if (CurrentEntity == null)
			{
				MessageBox.Show("Reference to Localplayer was NULL");
				return;
			}

			label1.Text = CurrentEntity->ptrEntityInfoStruct->IsInGame ? "Is Ingame: True" : "Is Ingame: False";

			var wndTargetInfo = HookDelegates.GetWindowByName("TargetInfo", false);
			label3.Text = wndTargetInfo != 0 ? $"Selected Target ID: 0x{(*(uint*)(wndTargetInfo + HookDelegates.TARGET_ID_OFFSET)):X8}" : $"Selected Target ID: 0x0 (Cannot get TargetInfo window base)";
		}

		private void CheckBox2_CheckedChanged(object sender, EventArgs e)
		{
			if (CurrentEntity == null)
			{
				MessageBox.Show("Reference to Localplayer was NULL");
				return;
			}

			if (checkBox2.Checked)
			{
				uint zoom = HelperMethods.AlainFindPattern("8B 0D ? ? ? ? E8 ? ? ? ? A1 ? ? ? ? 3B C3 75 05 E8 ? ? ? ? 8B C8 E8 ? ? ? ? A1 ? ? ? ? ",
					1, 1, HelperMethods.MemorySearchEntry.RT_READNEXT4_BYTES_RAW);

				if (zoom != 0)
				{
					int num = (int)zoom;
					int num2 = 0;
					int num3 = *(int*) (num + num2);
					float num4 = *(float*) (num3 + 84);
					*(float*) (num3 + 84) = 10000f;
				}
			}
			else
			{
				uint zoom = HelperMethods.AlainFindPattern("8B 0D ? ? ? ? E8 ? ? ? ? A1 ? ? ? ? 3B C3 75 05 E8 ? ? ? ? 8B C8 E8 ? ? ? ? A1 ? ? ? ? ",
					1, 1, HelperMethods.MemorySearchEntry.RT_READNEXT4_BYTES_RAW);

				if (zoom != 0)
				{
					int num = (int)zoom;
					int num2 = 0;
					int num3 = *(int*)(num + num2);
					float num4 = *(float*)(num3 + 84);
					*(float*)(num3 + 84) = 21.25f;
				}
			}
		}

		private void CheckBox3_CheckedChanged(object sender, EventArgs e)
		{
			if (CurrentEntity == null)
			{
				MessageBox.Show("Reference to Localplayer was NULL");
				return;
			}

			if (checkBox3.Checked)
			{
				uint address = (uint) CurrentEntity->ptrEntityInfoStruct;
				if (*(uint*) (address + 236) == 30000) return;
				*(uint*) (address + 236) = 30000;
			}
		}

		private void Button5_Click(object sender, EventArgs e)
		{
			if (textBox6.Text == "") return;
			uint result = HookDelegates.GetWindowByName(textBox6.Text, checkBox4.Checked);
			textBox6.Text = result != uint.MinValue ? $"0x{result:X8}" : "0x0";
		}

		private void Button6_Click(object sender, EventArgs e)
		{
			var results = HookDelegates.GetAllWnds();
			if (results != null && results.Count > 0)
			{
				listBox1.Items.Clear();

				foreach (var pair in results)
				{
					listBox1.Items.Add($"0x{pair.Item1.ToInt32():X8} - {pair.Item2}");
				}
			}
			else
			{
				listBox1.Items.Clear();
				listBox1.Items.Add($"Function GetAllWnds returned null or zero results!");
			}
		}

		private void Button7_Click(object sender, EventArgs e)
		{
			bool res = WindowType.TryParse(comboBox1.Text, out WindowType b);
			if (res)
				HookDelegates.origOpenUI?.Invoke(b, 0, 0);
		}

		public enum WindowType : byte
		{
			// Token: 0x040001CA RID: 458
			ActionSit = 2,
			// Token: 0x040001CB RID: 459
			ActionStand = 5,
			// Token: 0x040001CC RID: 460
			OptimizeInterface = 13,
			// Token: 0x040001CD RID: 461
			FusionInterface,
			// Token: 0x040001CE RID: 462
			BlacksmithInterface = 16,
			// Token: 0x040001CF RID: 463
			TeleportInterface,
			// Token: 0x040001D0 RID: 464
			EnhancementInterface = 19,
			// Token: 0x040001D1 RID: 465
			EidolonFeedInterface = 24,
			// Token: 0x040001D2 RID: 466
			ArcheologyInterface = 26,
			// Token: 0x040001D3 RID: 467
			AlchemyInterface,
			// Token: 0x040001D4 RID: 468
			EidolonOptimisationInterface,
			// Token: 0x040001D5 RID: 469
			HousingCraftInterface = 30,
			// Token: 0x040001D6 RID: 470
			BegleiterWnd
		}

		private void CheckBox5_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox5.Checked)
			{
				if (FishingAutoCenter == null)
				{
					FishingAutoCenter = new Thread(AutoCenter);
					FishingAutoCenter.Start();
				}
			}
		}

		public void AutoCenter()
		{
			while (true)
			{
				if (!checkBox5.Checked)
				{
					if (label4.Text != $"FishingWnd Base: 0x0")
						label4.Text = $"FishingWnd Base: 0x0";

					if (label5.Text != "Fishing State: NOT_FISHING")
						label5.Text = "Fishing State: NOT_FISHING";

					if (label6.Text != "Hooked Fish BaseAddress: 0x0")
						label6.Text = "Hooked Fish BaseAddress: 0x0";

					Thread.Sleep(500);
					continue;
				}

				uint fishingWnd = HookDelegates.GetWindowByName("FishingWnd", false);
				if (fishingWnd == 0)
				{
					if (label4.Text != $"FishingWnd Base: 0x0")
						label4.Text = $"FishingWnd Base: 0x0";

					if (label5.Text != "Fishing State: NOT_FISHING")
						label5.Text = "Fishing State: NOT_FISHING";

					if (label6.Text != "Hooked Fish BaseAddress: 0x0")
						label6.Text = "Hooked Fish BaseAddress: 0x0";

					Thread.Sleep(500);
					continue;
				}

				label4.Text = $"FishingWnd Base: 0x{fishingWnd:X8}"; ;
				HookDelegates.FishingWnd* activeWnd = (HookDelegates.FishingWnd*) fishingWnd;
				label5.Text = $"Fishing State: {Enum.GetName(typeof(HookDelegates.FishingState), activeWnd->FishingState)}";

				if (activeWnd->FishingState == HookDelegates.FishingState.FISHING_CAN_HOOK_FISH ||
				    activeWnd->FishingState == HookDelegates.FishingState.FISHING_IDLE)
				{
					HookDelegates.SetNextState(fishingWnd);
					Console.WriteLine("Started fishing, and waiting 1 second(s) ...");
					Thread.Sleep(1000);
				} else if (activeWnd->FishingState == HookDelegates.FishingState.FISHING_FISH_HOOKED)
				{
					uint fishPtrAddress = (uint)activeWnd->hookedFishPtr;
					uint actualHookedFishBase = Memory.Reader.UnsafeRead<uint>(new IntPtr(fishPtrAddress) + 0x14);

					label6.Text = $"Hooked Fish BaseAddress: 0x{actualHookedFishBase:X8}";

					if (checkBox6.Checked)
						*(float*)&(activeWnd->LineValue) = (activeWnd->RangeMin + activeWnd->RangeMax) / 2f;

					if (checkBox7.Checked)
						*(float*)&(activeWnd->CurrentDurability) = 30000f;

					Thread.Sleep(25);
				} else if (activeWnd->FishingState == HookDelegates.FishingState.POST_FISHING_ANIMATION)
				{
					if (!checkBox8.Checked) continue;
					HookDelegates.CancelFishingAnimation();
					button8.PerformClick();
					Console.WriteLine("Canceled fishing animation, waiting 1 second(s) ...");
					Thread.Sleep(1000);
				}
			}
		}

		private void Label5_Click(object sender, EventArgs e)
		{

		}

		private void Label6_Click(object sender, EventArgs e)
		{
			string addr = "0x" + label6.Text.Split(':')[1].TrimStart(' ');
			Thread STAThread = new Thread(
				delegate ()
				{
					System.Windows.Forms.Clipboard.SetText(addr);
				});
			STAThread.SetApartmentState(ApartmentState.STA);
			STAThread.Start();
			STAThread.Join();
			Console.WriteLine($"Hooked Fish base address copied to clipboard!");
		}

		private void Button8_Click(object sender, EventArgs e)
		{
			if (checkBox5.Checked)
			{
				uint fishingWnd = HookDelegates.GetWindowByName("FishingWnd", false);
				if (fishingWnd == 0) return;

				HookDelegates.StopFishing(fishingWnd);
				label5.Text = "Fishing State: NOT_FISHING";
				checkBox5.Checked = false;
			}
		}
	}
}
