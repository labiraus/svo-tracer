using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
	public struct Octree
	{
		public byte N { get; set; }
		public uint BlockCount { get; set; }
		public ushort[] BaseBlocks { get; set; }
		public Block[] Blocks { get; set; }
		public void Serialize(BinaryWriter writer)
		{
			writer.Write(N);
			writer.Write(BlockCount);
			foreach (var baseBlock in BaseBlocks)
				writer.Write(baseBlock);
			foreach (var block in Blocks)
				block.Serialize(writer);
		}

		public static Octree Deserialize(BinaryReader reader)
		{
			var n = reader.ReadByte();
			var blockCount = reader.ReadUInt32();
			var baseCount = TreeBuilder.PowSum(n);
			var tree = new Octree
			{
				N = n,
				BlockCount = blockCount,
				BaseBlocks = new ushort[baseCount],
				Blocks = new Block[blockCount]
			};

			var bases = reader.ReadBytes(tree.BaseBlocks.Length * 2);
			for (int i = 0; i < baseCount; i++)
				tree.BaseBlocks[i] = BitConverter.ToUInt16(bases, i * 2);

			var blocks = reader.ReadBytes(Block.Size * (int)blockCount);
			for (int i = 0; i < tree.BlockCount; i++)
				tree.Blocks[i] = Block.Deserialize(blocks[(i * Block.Size)..((i + 1) * Block.Size)]);

			return tree;
		}
	}
}
