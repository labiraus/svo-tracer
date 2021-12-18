using SvoTracer.Domain.Models;
using System;
using System.IO;

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
			writer.Write(block.NormalPitch);
			writer.Write(block.NormalYaw);
			writer.Write(block.ColourR);
			writer.Write(block.ColourG);
			writer.Write(block.ColourB);
			writer.Write(block.Opacity);
			writer.Write(block.Specularity);
			writer.Write(block.Gloss);
		}

		public static Block Deserialize(BinaryReader reader) => new()
		{
			Child = reader.ReadUInt32(),
			Chunk = reader.ReadUInt16(),
			NormalPitch = reader.ReadInt16(),
			NormalYaw = reader.ReadInt16(),
			ColourR = reader.ReadByte(),
			ColourG = reader.ReadByte(),
			ColourB = reader.ReadByte(),
			Opacity = reader.ReadByte(),
			Specularity = reader.ReadByte(),
			Gloss = reader.ReadByte(),

		};

		public static Block Deserialize(byte[] data) => new()
		{
			Child = BitConverter.ToUInt32(data, 0),
			Chunk = BitConverter.ToUInt16(data, 4),
			NormalPitch = BitConverter.ToInt16(data, 6),
			NormalYaw = BitConverter.ToInt16(data, 8),
			ColourR = data[10],
			ColourG = data[11],
			ColourB = data[12],
			Opacity = data[13],
			Specularity = data[14],
			Gloss = data[15],
		};
	}
}
