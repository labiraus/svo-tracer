using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
	public struct Octree
	{
		public byte N;
		public uint BlockCount;
		public ushort[] BaseBlocks;
		public Block[] Blocks;
	}
}
