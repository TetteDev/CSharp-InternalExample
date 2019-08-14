using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MyInjectableLibrary
{
	public class HelperMethods
	{
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
}
