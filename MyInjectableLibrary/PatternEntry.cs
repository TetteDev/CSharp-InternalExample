using System;

namespace MyInjectableLibrary
{
	public class PatternEntry
	{
		private readonly string _pattern = "";
		private readonly bool _isAlainTypePattern = false;

		private readonly int _offset = 0;
		private readonly int _index = 0;
		private readonly HelperMethods.MemorySearchEntry _type = HelperMethods.MemorySearchEntry.RT_ADDRESS;
		private readonly bool _checkResult = false;
		private readonly byte[] _opcodeCheck = null;

		public uint ScanResult = 0;
		public string Identifier = "";

		public PatternEntry(string identifier, string pattern, bool isAlainType = false, int offset = 0, int index = 0, 
			HelperMethods.MemorySearchEntry alaintype = HelperMethods.MemorySearchEntry.RT_ADDRESS, 
			bool checkResult = false, byte[] opcodecheck = null)
		{
			_pattern = pattern;
			_isAlainTypePattern = isAlainType;
			Identifier = identifier;

			if (!isAlainType) return;
			_offset = offset;
			_index = index;
			_type = alaintype;
			_checkResult = checkResult;
			_opcodeCheck = opcodecheck;
		}

		public void Scan(ref uint resultContainer)
		{
			uint result = 0;
			if (_isAlainTypePattern)
			{
				result = HelperMethods.AlainFindPattern(_pattern, _offset, _index, _type, _checkResult, _opcodeCheck);
				if (result == 0)
					Console.WriteLine($"[Pattern Manager] Pattern for entry \"{Identifier}\" seems to be outdated!");

				ScanResult = result;
				resultContainer = result;
			}
			else
			{
				result = (uint) Memory.Pattern.FindPatternSingle(Main.ThisProcess.MainModule, _pattern, true).ToInt32();
				if (result == 0)
					Console.WriteLine($"[Pattern Manager] Pattern for entry \"{Identifier}\" seems to be outdated!");

				ScanResult = result;
				resultContainer = result;
			}
			
		}

	}
}
