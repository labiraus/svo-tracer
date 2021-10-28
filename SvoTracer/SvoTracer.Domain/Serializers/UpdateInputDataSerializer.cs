using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Serializers
{
	public static class UpdateInputDataSerializer
	{
		public static byte[] Serialize(this UpdateInputData data)
		{
			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			writer.Write(data.N);
			writer.Write(data.Tick);
			writer.Write(data.MaxChildRequestId);
			writer.Write(data.MemorySize);
			writer.Write(data.Offset);
			writer.Write(data.GraftSize);
			return ms.ToArray();
		}
	}
}
