using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
	public struct Block
	{
		public uint Child { get; set; }
		public ushort Chunk { get; set; }
		public BlockData Data { get; set; }

		public const int Size = 16;

		public void Serialize(BinaryWriter writer)
		{
			writer.Write(Child);
			writer.Write(Chunk);
			Data.Serialize(writer);
		}

		public static Block Deserialize(BinaryReader reader) => new()
		{
			Child = reader.ReadUInt32(),
			Chunk = reader.ReadUInt16(),
			Data = BlockData.Deserialize(reader)
		};

		public static Block Deserialize(byte[] data) => new()
		{
			Child = BitConverter.ToUInt32(data, 0),
			Chunk = BitConverter.ToUInt16(data, 4),
			Data = BlockData.Deserialize(data[(Size - BlockData.Size)..Size])
		};
	}
}
