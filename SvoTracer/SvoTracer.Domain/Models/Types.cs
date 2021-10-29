using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Models
{
	public struct uint2
	{
		public uint2(uint x, uint y)
		{
			this.x = x;
			this.y = y;
		}
		public uint x;
		public uint y;
	}
	public struct uint3
	{
		public uint3(uint x, uint y, uint z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}
		public uint x;
		public uint y;
		public uint z;
	}
}
