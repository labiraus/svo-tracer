using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Serializers
{
	public static class OctreeSerializer
	{
		public static void Serialize(this Octree octree, BinaryWriter writer)
		{
			writer.Write(octree.BaseDepth);
			writer.Write(octree.BlockCount);
			foreach (var baseBlock in octree.BaseBlocks)
				writer.Write(baseBlock);
			foreach (var block in octree.Blocks)
				block.Serialize(writer);
		}

		public static Octree Deserialize(BinaryReader reader)
		{
			var baseDepth = reader.ReadByte();
			var blockCount = reader.ReadUInt32();
			var baseCount = TreeBuilder.PowSum(baseDepth);
			var tree = new Octree
			{
				BaseDepth = baseDepth,
				BlockCount = blockCount,
				BaseBlocks = new ushort[baseCount],
				Blocks = new Block[blockCount]
			};

			var bases = reader.ReadBytes(tree.BaseBlocks.Length * 2);
			for (int i = 0; i < baseCount; i++)
				tree.BaseBlocks[i] = BitConverter.ToUInt16(bases, i * 2);

			var blocks = reader.ReadBytes(Block.Size * (int)blockCount);
			for (int i = 0; i < tree.BlockCount; i++)
				tree.Blocks[i] = BlockSerializer.Deserialize(blocks[(i * Block.Size)..((i + 1) * Block.Size)]);

			return tree;
		}
	}
}
