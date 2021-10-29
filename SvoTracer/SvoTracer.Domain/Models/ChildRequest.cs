using System;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace SvoTracer.Domain.Models
{
	public struct ChildRequest
	{
		public uint Address;
		public ushort Tick;
		public byte Depth;
		public Location Location;
		public byte TreeSize;
		public const int Size = 32;
	}
}
