using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain.Models
{
	public struct BoundingVolume
	{
		public BoundingVolume(float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
		{
			XMin = xMin;
			XMax = xMax;
			YMin = yMin;
			YMax = yMax;
			ZMin = zMin;
			ZMax = zMax;
		}
		public BoundingVolume(Location location, byte depth)
		{
			float division = 1;
			XMax = 1;
			XMin = 0;
			YMax = 1;
			YMin = 0;
			ZMax = 1;
			ZMin = 0;
			const ulong nearMax = ulong.MaxValue - (ulong.MaxValue >> 1);
			for (ushort i = 0; i < depth; i++)
			{
				division /= 2;
				if ((location.X & (nearMax >> i)) > 0)
					XMax += division;
				else
					XMin -= division;

				if ((location.Y & (nearMax >> i)) > 0)
					YMax += division;
				else
					YMin -= division;

				if ((location.Z & (nearMax >> i)) > 0)
					ZMax += division;
				else
					ZMin -= division;
			}
		}
		public float XMin { get; set; }
		public float XMax { get; set; }
		public float YMin { get; set; }
		public float YMax { get; set; }
		public float ZMin { get; set; }
		public float ZMax { get; set; }
	}
}
