using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
	public struct Block
	{
		public uint Child;
		public ushort Chunk;
		public SurfaceData Data;

		public const int Size = 16;
	}
}
