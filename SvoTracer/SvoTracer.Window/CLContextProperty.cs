using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Window
{
	[StructLayout(LayoutKind.Sequential)]
	public struct CLContextProperty
	{
		public CLContextProperty(IntPtr property, IntPtr value)
		{
			Property = property;
			Value = value;
		}
		public IntPtr Property;
		public IntPtr Value;
	}
}
