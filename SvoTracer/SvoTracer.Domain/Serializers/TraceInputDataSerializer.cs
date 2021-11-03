﻿using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Serializers
{
	public static class TraceInputDataSerializer
	{
		public static byte[] Serialize(this TraceInputData data)
		{
			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			writer.Write(data.Origin.X);
			writer.Write(data.Origin.Y);
			writer.Write(data.Origin.Z);
			writer.Write(data.Facing.X);
			writer.Write(data.Facing.Y);
			writer.Write(data.Facing.Z);
			writer.Write(data.FoV.X);
			writer.Write(data.FoV.Y);
			writer.Write(data.DoF.X);
			writer.Write(data.DoF.Y);
			writer.Write(data.ScreenSize.X);
			writer.Write(data.ScreenSize.Y);
			writer.Write(data.MaxOpacity);
			writer.Write(data.N);
			writer.Write(data.Tick);
			writer.Write(data.MaxChildRequestId);
			return ms.ToArray();
		}
	}
}