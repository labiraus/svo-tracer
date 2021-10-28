using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
	public struct BlockData
	{
		public short NormalPitch { get; set; }
		public short NormalYaw { get; set; }
		public byte ColourR { get; set; }
		public byte ColourB { get; set; }
		public byte ColourG { get; set; }
		public byte Opacity { get; set; }
		public ushort Properties { get; set; }

		public const int Size = 10;
	}
}
