using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MyInjectableLibrary
{
	public static class IntExtensions
	{
		
	}

	public static class DelegateExtensions
	{
		public static IntPtr GetFunctionPointer(this Delegate obj)
		{
			return Marshal.GetFunctionPointerForDelegate(obj);
		}
	}
}
