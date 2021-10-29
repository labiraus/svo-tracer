using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Serializers
{
	public static class UsageSerializer
	{
		public static byte[] Serialize(this Usage[] usages)
		{
			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			foreach (var usage in usages)
			{
				writer.Write(usage.Tick);
				writer.Write(usage.Parent);
			}
			return ms.ToArray();
		}
	}
}
