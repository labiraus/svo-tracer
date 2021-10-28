using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
	public struct Octree
	{
		public byte N { get; set; }
		public uint BlockCount { get; set; }
		public ushort[] BaseBlocks { get; set; }
		public Block[] Blocks { get; set; }
	}
}
