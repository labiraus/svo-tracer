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
			MinX = xMin;
			MaxX = xMax;
			MinY = yMin;
			MaxY = yMax;
			MinZ = zMin;
			MaxZ = zMax;
		}
		public BoundingVolume(Location location, byte depth)
		{
			float division = 1;
			MaxX = 1;
			MinX = 0;
			MaxY = 1;
			MinY = 0;
			MaxZ = 1;
			MinZ = 0;
			const ulong nearMax = ulong.MaxValue - (ulong.MaxValue >> 1);
			for (ushort i = 0; i < depth; i++)
			{
				division /= 2;
				if ((location.X & (nearMax >> i)) > 0)
					MinX += division;
				else
					MaxX -= division;

				if ((location.Y & (nearMax >> i)) > 0)
					MinY += division;
				else
					MaxY -= division;

				if ((location.Z & (nearMax >> i)) > 0)
					MinZ += division;
				else
					MaxZ -= division;
			}
		}
		public float MinX { get; set; }
		public float MaxX { get; set; }
		public float MinY { get; set; }
		public float MaxY { get; set; }
		public float MinZ { get; set; }
		public float MaxZ { get; set; }
	}
}
