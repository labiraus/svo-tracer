using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Serializers
{
	public static class LocationSerializer
	{
		public static byte[] Serialize(this Location[] locations)
		{
			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			foreach (var location in locations)
			{
				writer.Write(location.X);
				writer.Write(location.Y);
				writer.Write(location.Z);
			}
			return ms.ToArray();
		}
	}
}
