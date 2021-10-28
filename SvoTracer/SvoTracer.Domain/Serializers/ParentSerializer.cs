using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Serializers
{
	public static class ParentSerializer
	{
		public static byte[] Serialize(this Parent[] parents)
		{
			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			foreach (var parent in parents)
				parent.Serialize(writer);
			return ms.ToArray();
		}

		public static void Serialize(this Parent parent, BinaryWriter writer)
		{
			writer.Write(parent.ParentAddress);
			writer.Write(parent.NextElement);
		}
	}
}
