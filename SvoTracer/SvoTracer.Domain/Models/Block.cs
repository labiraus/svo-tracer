using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
	public struct Block
	{
		public uint Child;
		public ushort Chunk;
		public short NormalPitch;
		public short NormalYaw;
		public byte ColourR;
		public byte ColourG;
		public byte ColourB;
		public byte Opacity;
		public byte Specularity;
		public byte Gloss;

		public const int Size = 16;
	}
}
