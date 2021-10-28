using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
	public struct Block
	{
		public uint Child { get; set; }
		public ushort Chunk { get; set; }
		public BlockData Data { get; set; }

		public const int Size = 16;
	}
}
