using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
	public struct BlockData
	{
		public short NormalPitch { get; set; }
		public short NormalYaw { get; set; }
		public byte ColourR { get; set; }
		public byte ColourB { get; set; }
		public byte ColourG { get; set; }
		public byte Opacity { get; set; }
		public ushort Properties { get; set; }

		public const int Size = 10;

		public void Serialize(BinaryWriter writer)
		{
			writer.Write(NormalPitch);
			writer.Write(NormalYaw);
			writer.Write(ColourR);
			writer.Write(ColourB);
			writer.Write(ColourG);
			writer.Write(Opacity);
			writer.Write(Properties);
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
