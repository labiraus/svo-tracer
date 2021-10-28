using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Serializers
{
	public static class GraftingSerializer
	{
		public static byte[] Serialize(this Grafting[] grafts)
		{
			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			foreach (var graft in grafts)
				graft.Serialize(writer);
			return ms.ToArray();
		}

		public static void Serialize(this Grafting graft, BinaryWriter writer)
		{
			writer.Write(graft.GraftDataAddress);
			writer.Write(graft.GraftTotalSize);
			writer.Write(graft.Depth);
			writer.Write(graft.GraftAddress);
		}

		public static Grafting Deserialize(BinaryReader reader) => new()
		{
			GraftDataAddress = reader.ReadUInt32(),
			GraftTotalSize = reader.ReadUInt32(),
			Depth = reader.ReadByte(),
			GraftAddress = reader.ReadUInt32(),
		};

		public static Grafting Deserialize(byte[] data) => new()
		{
			GraftDataAddress = BitConverter.ToUInt32(data, 0),
			GraftTotalSize = BitConverter.ToUInt32(data, 4),
			Depth = (byte)BitConverter.ToChar(data, 8),
			GraftAddress = BitConverter.ToUInt32(data, 9),
		};
	}
}
