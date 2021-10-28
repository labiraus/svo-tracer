using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Serializers
{
	public static class BlockDataSerializer
	{
		public static byte[] Serialize(this BlockData[] blocks)
		{
			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			foreach (var block in blocks) 
				block.Serialize(writer);
			return ms.ToArray();
		}

		public static void Serialize(this BlockData blockData, BinaryWriter writer)
		{
			writer.Write(blockData.NormalPitch);
			writer.Write(blockData.NormalYaw);
			writer.Write(blockData.ColourR);
			writer.Write(blockData.ColourB);
			writer.Write(blockData.ColourG);
			writer.Write(blockData.Opacity);
			writer.Write(blockData.Properties);
		}

		public static BlockData Deserialize(BinaryReader reader) => new()
		{
			NormalPitch = reader.ReadInt16(),
			NormalYaw = reader.ReadInt16(),
			ColourR = reader.ReadByte(),
			ColourB = reader.ReadByte(),
			ColourG = reader.ReadByte(),
			Opacity = reader.ReadByte(),
			Properties = reader.ReadUInt16()
		};

		public static BlockData Deserialize(byte[] data) => new()
		{
			NormalPitch = BitConverter.ToInt16(data, 0),
			NormalYaw = BitConverter.ToInt16(data, 2),
			ColourR = (byte)BitConverter.ToChar(data, 4),
			ColourB = (byte)BitConverter.ToChar(data, 5),
			ColourG = (byte)BitConverter.ToChar(data, 6),
			Opacity = (byte)BitConverter.ToChar(data, 7),
			Properties = BitConverter.ToUInt16(data, 8),
		};
	}
}
