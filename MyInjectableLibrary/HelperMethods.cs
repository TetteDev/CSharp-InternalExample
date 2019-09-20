using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyInjectableLibrary
{
	public class HelperMethods
	{
		public static unsafe uint AlainFindPattern(string pattern, int offset = 0, int occurenceIdx = 1, HelperMethods.MemorySearchEntry type = HelperMethods.MemorySearchEntry.RT_ADDRESS, bool checkResult = false, byte[] OpCodeCheck = null)
		{
			if (pattern == "") return 0;
			IntPtr moduleBase = PInvoke.GetModuleHandle("game.bin");
			if (moduleBase == IntPtr.Zero) return 0;
			var len = PInvoke.VirtualQuery(moduleBase, out var info, (uint)sizeof(PInvoke.MEMORY_BASIC_INFORMATION));
			byte* processBase = (byte*)info.BaseAddress;
			byte* address = processBase;

			UIntPtr size = UIntPtr.Zero;
			uint count = 0;
			for (; ; ++count)
			{
				len = PInvoke.VirtualQuery(new IntPtr(address), out info, (uint)sizeof(PInvoke.MEMORY_BASIC_INFORMATION));
				if (info.AllocationBase != (IntPtr)processBase)
					break;
				address = (byte*)(info.BaseAddress.ToInt32() + info.RegionSize.ToInt32());
				size = UIntPtr.Add(size, info.RegionSize.ToInt32());
			}

			List<IntPtr> results = Memory.Pattern.FindPattern((IntPtr)processBase, (int)size, pattern, true);
			if (results.Count < 1) return 0;

			IntPtr dwAddy = occurenceIdx > results.Count + 1 ? results.Last() : (occurenceIdx == 0 ? results[0] : results[occurenceIdx - 1]); // Original code occurence was 1 for the first item, 2 for the second item, 3 for the third etc ...
			dwAddy = IntPtr.Add(dwAddy, offset);

			switch (type)
			{
				case MemorySearchEntry.RT_ADDRESS:
					return *(uint*)dwAddy.ToInt32();
				case MemorySearchEntry.RT_REL_ADDRESS:
					if (checkResult)
						if (!CheckOpcode((byte*)(dwAddy - 1), new byte[] { 0xE8 }))
							return 0;
					uint addr = *(uint*)(dwAddy.ToInt32());
					addr += ((uint)dwAddy.ToInt32() + 4);
					return addr;
				case MemorySearchEntry.RT_LOCATION:
					if (!checkResult) return (uint)dwAddy.ToInt32();
					if (!CheckOpcode((byte*)(dwAddy - 1), OpCodeCheck ?? new byte[] { 0x55, 0x8B, 0xEC }))
						return 0;
					return (uint)dwAddy.ToInt32();
				case MemorySearchEntry.RT_READNEXT4_BYTES_RAW:
					byte[] array6 = new byte[4];
					Array.Copy(Memory.Reader.UnsafeReadBytes(IntPtr.Add(dwAddy, 1), 4), 0, array6, 0, 4);
					return (uint) BitConverter.ToInt32(array6, 0);
				case MemorySearchEntry.RT_READNEXT4_BYTES:
					byte[] array5 = new byte[4];
					Array.Copy(Memory.Reader.UnsafeReadBytes(IntPtr.Add(dwAddy, 1), 4), 0, array5, 0, 4);
					return (uint)(Main.ThisProcess.MainModule.BaseAddress.ToInt32() + BitConverter.ToInt32(array5, 0) - (Main.ThisProcess.MainModule.BaseAddress.ToInt32() - dwAddy.ToInt32() - 5));
				default:
					return 0;
					/*
					case MemorySearchEntry.RT_LOCATION:
					case MemorySearchEntry.RT_REL_ADDRESS:
						if (!checkResult) return type == MemorySearchEntry.RT_LOCATION
							? (uint)dwAddy.ToInt32()
							: *(uint*)dwAddy.ToInt32() + (uint)dwAddy.ToInt32() + 4;
						if (!CheckOpcode((byte*) (dwAddy - 1), 
							type == MemorySearchEntry.RT_REL_ADDRESS 
								? (new byte[] {0xE8}) 
								: OpCodeCheck ?? new byte[] { 0x55, 0x8B, 0xEC }))
							return 0;
						return type == MemorySearchEntry.RT_LOCATION 
							? (uint) dwAddy.ToInt32() 
							: *(uint*)dwAddy.ToInt32() + (uint)dwAddy.ToInt32() + 4;
					case MemorySearchEntry.RT_ADDRESS:
						return (uint)dwAddy.ToInt32();
					default:
						return 0;
						*/
			}
		}

		public static unsafe bool CheckOpcode(byte* pData, byte[] check)
		{
			if (check == null)
			{
				if (Debugger.IsAttached) Debugger.Break();
				return false;
			}
			if (check[0].ToString("X") == "") return true;
			foreach (var t in check)
			{
				++pData;
				if (*pData != t)
					return false;
			}
			return true;
		}
		public enum MemorySearchEntry
		{
			RT_ADDRESS = 0,
			RT_REL_ADDRESS = 1,
			RT_LOCATION = 2,
			RT_READNEXT4_BYTES_RAW = 3,
			RT_READNEXT4_BYTES = 4,
		}

		public static unsafe int FindPattern(byte* body, int bodyLength, byte[] pattern, byte[] masks, int start = 0)
		{
			int foundIndex = -1;

			if (bodyLength <= 0 || pattern.Length <= 0 || start > bodyLength - pattern.Length ||
			    pattern.Length > bodyLength) return foundIndex;

			for (int index = start; index <= bodyLength - pattern.Length; index++)
			{
				if (((body[index] & masks[0]) != (pattern[0] & masks[0]))) continue;

				var match = true;
				for (int index2 = 1; index2 <= pattern.Length - 1; index2++)
				{
					if ((body[index + index2] & masks[index2]) == (pattern[index2] & masks[index2])) continue;
					match = false;
					break;

				}

				if (!match) continue;

				foundIndex = index;
				break;
			}

			return foundIndex;
		}

		public static void PrintExceptionData(object exceptionObj, bool writeToFile = false)
		{
			if (exceptionObj == null) return;
			Type actualType = exceptionObj.GetType();

			Exception exceptionObject = exceptionObj as Exception;

			var s = new StackTrace(exceptionObject);
			var thisasm = Assembly.GetExecutingAssembly();

			var methodName = s.GetFrames().Select(f => f.GetMethod()).First(m => m.Module.Assembly == thisasm).Name;
			var parameterInfo = s.GetFrames().Select(f => f.GetMethod()).First(m => m.Module.Assembly == thisasm).GetParameters();
			var methodReturnType = s.GetFrame(1).GetMethod().GetType();

			var lineNumber = s.GetFrame(0).GetFileLineNumber();

			// string formatedMethodNameAndParameters = $"{methodReturnType} {methodName}(";
			string formatedMethodNameAndParameters = $"{methodName}(";

			if (parameterInfo.Length < 1)
			{
				formatedMethodNameAndParameters += ")";
			}
			else
			{
				for (int n = 0; n < parameterInfo.Length; n++)
				{
					ParameterInfo param = parameterInfo[n];
					string parameterName = param.Name;

					if (n == parameterInfo.Length - 1)
						formatedMethodNameAndParameters += $"{param.ParameterType} {parameterName})";
					else
						formatedMethodNameAndParameters += $"{param.ParameterType} {parameterName},";
				}
			}

			string formattedContent = $"[UNHANDLED_EXCEPTION] Caught Exception of type {actualType}\n\n" +
			                          $"Exception Message: {exceptionObject.Message}\n" +
			                          $"Exception Origin File/Module: {exceptionObject.Source}\n" +
			                          $"Method that threw the Exception: {formatedMethodNameAndParameters}\n";

			Console.WriteLine(formattedContent);

			if (exceptionObject.Data.Count > 0)
			{
				Console.WriteLine($"Exception Data Dictionary Results:");
				foreach (DictionaryEntry pair in exceptionObject.Data)
				{
					Console.WriteLine("	* {0} = {1}", pair.Key, pair.Value);
				}
			}
			
			if (writeToFile)
				WriteToFile(formattedContent);

		}

		public static void WriteToFile(string contents)
		{
			if (contents.Length < 1) return;
			File.WriteAllText($"session_logs.txt", contents);
		}
	}

	public static class ProcessExtensions
	{
		public static ProcessModule FindProcessModule(this Process obj, string moduleName)
		{
			foreach (ProcessModule pm in obj.Modules)
				if (string.Equals(pm.ModuleName, moduleName, StringComparison.CurrentCultureIgnoreCase))
					return pm;

			return null;
		}
	}

	public static class ListPatternEntryExtensions
	{
		public static uint GetItemByName(this List<PatternEntry> obj, string name)
		{
			if (obj == null || obj.Count < 1) return UInt32.MinValue;

			return obj.FirstOrDefault(x => x.Identifier != null && x.Identifier.ToLower() == name.ToLower()).ScanResult;
		}

		public static void RescanAll(this List<PatternEntry> obj, bool printResults = false)
		{
			if (obj == null || obj.Count < 1) return;

			Parallel.ForEach(obj, (currentPattern) =>
			{
				switch (currentPattern.Identifier)
				{
					case "StopFishing":
						currentPattern.Scan(ref Main.StopFishing);
						break;
					case "CatchProcess":
						currentPattern.Scan(ref Main.CatchProcess);
						break;
					case "OpenUIFunction":
						currentPattern.Scan(ref Main.OpenUIFunction);
						break;
					case "WndInterfaceBase":
						currentPattern.Scan(ref Main.WndInterfaceBase);
						break;
					case "GetLocalPlayer":
						currentPattern.Scan(ref Main.GetLocalPlayer);
						break;
					case "CurrentMapBase":
						currentPattern.Scan(ref Main.CurrentMapBase);
						break;
					case "DetourMainLoopOffset":
						currentPattern.Scan(ref Main.DetourMainLoopOffset);
						break;
					case "InventoryAccessFunction":
						currentPattern.Scan(ref Main.InventoryAccessFunction);
						break;
					case "TargetingCollectionsBase":
						currentPattern.Scan(ref Main.TargetingCollectionsBase);
						break;
					default:
						Console.WriteLine($"Encountered a pattern entry with no destination variable, it is only retrievable via GetAddressFromIdentifier(string) now ...");
						break;
				}
			});

			if (printResults)
			{
				foreach (var pattern in obj)
					Console.WriteLine($"{pattern.Identifier}: 0x{pattern.ScanResult:X8}");
			}

			Console.WriteLine($"\n[Pattern Manager] Scanned {obj.Count} patterns!");
		}

		public static void RescanAllFailedEntries(this List<PatternEntry> obj)
		{
			if (obj == null || obj.Count < 1) return;

			int rescannedCount = 0;
			Parallel.ForEach(obj, (currentPattern) =>
			{
				if (currentPattern.ScanResult != 0) return;
				rescannedCount++;
				Console.WriteLine($"Rescanning pattern for identifier \"{currentPattern.Identifier}\" ...");
				switch (currentPattern.Identifier)
				{
					case "StopFishing":
						currentPattern.Scan(ref Main.StopFishing);
						break;
					case "CatchProcess":
						currentPattern.Scan(ref Main.CatchProcess);
						break;
					case "OpenUIFunction":
						currentPattern.Scan(ref Main.OpenUIFunction);
						break;
					case "WndInterfaceBase":
						currentPattern.Scan(ref Main.WndInterfaceBase);
						break;
					case "GetLocalPlayer":
						currentPattern.Scan(ref Main.GetLocalPlayer);
						break;
					case "CurrentMapBase":
						currentPattern.Scan(ref Main.CurrentMapBase);
						break;
					case "DetourMainLoopOffset":
						currentPattern.Scan(ref Main.DetourMainLoopOffset);
						break;
					case "InventoryAccessFunction":
						currentPattern.Scan(ref Main.InventoryAccessFunction);
						break;
					case "TargetingCollectionsBase":
						currentPattern.Scan(ref Main.TargetingCollectionsBase);
						break;
					default:
						Console.WriteLine($"Encountered a pattern entry with no destination variable, it is only retrievable via GetAddressFromIdentifier(string) now ...");
						break;
				}
			});

			Console.WriteLine($"\n[Pattern Manager] Rescanned {rescannedCount} patterns");
		}
	}

	public static class ProcessModuleExtensions
	{
		public static IntPtr FindPatternSingle(this ProcessModule pModObj, string pattern, bool resultAbsolute = true)
		{
			if (pModObj == null || pattern == string.Empty) return IntPtr.Zero;
			return Memory.Pattern.FindPatternSingle(pModObj, pattern, resultAbsolute);
		}

		public static List<IntPtr> FindPattern(this ProcessModule pModObj, string pattern, bool resultAbsolute = true)
		{
			if (pModObj == null || pattern == string.Empty) return new List<IntPtr>();
			return Memory.Pattern.FindPattern(pModObj, pattern, resultAbsolute);
		}
	}

	public static unsafe class PointerExtensions {

		
	}
}
