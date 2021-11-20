﻿using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
	public struct BlockData
	{
		public ushort NormalPitch;
		public ushort NormalYaw;
		public byte ColourR;
		public byte ColourB;
		public byte ColourG;
		public byte Opacity;
		public ushort Properties;

		public const int Size = 10;
	}
}
