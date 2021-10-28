using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Serializers
{
	public static class PruningSerializer
	{
		public static byte[] Serialize(this Pruning[] pruning)
		{
			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			foreach (var prune in pruning)
				prune.Serialize(writer);
			return ms.ToArray();
		}

		public static void Serialize(this Pruning prune, BinaryWriter writer)
		{
			writer.Write(prune.Properties);
			writer.Write(prune.Depth);
			writer.Write(prune.Address);
			writer.Write(prune.Chunk);
			writer.Write(prune.ColourAddress);
			writer.Write(prune.ChildAddress);
		}

		public static Pruning Deserialize(BinaryReader reader) => new()
		{
			Properties = reader.ReadByte(),
			Depth = reader.ReadByte(),
			Address = reader.ReadUInt32(),
			Chunk = reader.ReadUInt16(),
			ColourAddress = reader.ReadUInt32(),
			ChildAddress = reader.ReadUInt32()
		};

		public static Pruning Deserialize(byte[] data) => new()
		{
			Properties = (byte)BitConverter.ToChar(data, 0),
			Depth = (byte)BitConverter.ToChar(data, 1),
			Address = BitConverter.ToUInt32(data, 2),
			Chunk = BitConverter.ToUInt16(data, 4),
			ColourAddress = BitConverter.ToUInt32(data, 6),
			ChildAddress = BitConverter.ToUInt32(data, 10)
		};
	}
}
