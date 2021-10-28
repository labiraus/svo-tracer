using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Serializers
{
	public static class BlockSerializer
	{
		public static byte[] Serialize(this Block[] blocks)
		{
			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			foreach (var block in blocks)
				block.Serialize(writer);
			return ms.ToArray();
		}

		public static void Serialize(this Block block, BinaryWriter writer)
		{
			writer.Write(block.Child);
			writer.Write(block.Chunk);
			block.Data.Serialize(writer);
		}

		public static Block Deserialize(BinaryReader reader) => new()
		{
			Child = reader.ReadUInt32(),
			Chunk = reader.ReadUInt16(),
			Data = BlockDataSerializer.Deserialize(reader)
		};

		public static Block Deserialize(byte[] data) => new()
		{
			Child = BitConverter.ToUInt32(data, 0),
			Chunk = BitConverter.ToUInt16(data, 4),
			Data = BlockDataSerializer.Deserialize(data[(Block.Size - BlockData.Size)..Block.Size])
		};
	}
}
