using SvoTracer.Domain.Interfaces;
using System;
using System.Numerics;
using SvoTracer.Domain.Models;

namespace SvoTracer.Domain.Geometry
{
	public class SphereDefinition : IGeometryDefinition
	{
		private readonly Vector3 origin;
		private readonly float r;
		private readonly Vector3 min;
		private readonly Vector3 max;

		public SphereDefinition(Vector3 origin, float r)
		{
			this.origin = origin;
			this.r = r;
			min = new Vector3(origin.X - r, origin.Y - r, origin.Z - r);
			max = new Vector3(origin.X + r, origin.Y + r, origin.Z + r);
		}

		float squared(float v) { return v * v; }

		public bool ContainsGeo(BoundingVolume volume)
		{
			float dist_squared = r * r;
			/* assume C1 and C2 are element-wise sorted, if not, do that now */
			if (origin.X < volume.XMin) dist_squared -= squared(origin.X - volume.XMin);
			else if (origin.X > volume.XMax) dist_squared -= squared(origin.X - volume.XMax);
			if (origin.Y < volume.YMin) dist_squared -= squared(origin.Y - volume.YMin);
			else if (origin.Y > volume.YMax) dist_squared -= squared(origin.Y - volume.YMax);
			if (origin.Z < volume.ZMin) dist_squared -= squared(origin.Z - volume.ZMin);
			else if (origin.Z > volume.ZMax) dist_squared -= squared(origin.Z - volume.ZMax);
			return dist_squared > 0;
		}

		public bool ContainsAir(BoundingVolume volume)
		{
			return
				(new Vector3(volume.XMin, volume.YMin, volume.ZMin) - origin).Length() > r ||
				(new Vector3(volume.XMax, volume.YMin, volume.ZMin) - origin).Length() > r ||
				(new Vector3(volume.XMin, volume.YMax, volume.ZMin) - origin).Length() > r ||
				(new Vector3(volume.XMax, volume.YMax, volume.ZMin) - origin).Length() > r ||
				(new Vector3(volume.XMin, volume.YMin, volume.ZMax) - origin).Length() > r ||
				(new Vector3(volume.XMax, volume.YMin, volume.ZMax) - origin).Length() > r ||
				(new Vector3(volume.XMin, volume.YMax, volume.ZMax) - origin).Length() > r ||
				(new Vector3(volume.XMax, volume.YMax, volume.ZMax) - origin).Length() > r;
		}

		public bool WithinBounds(BoundingVolume volume) =>
			(volume.XMin < max.X && volume.XMax > min.X &&
			 volume.YMin < max.Y && volume.YMax > min.Y &&
			 volume.ZMin < max.Z && volume.ZMax > min.Z);

		public (short pitch, short yaw) Normal(BoundingVolume volume)
		{
			Vector3 finalNormal = new Vector3(volume.XMax - volume.XMin, volume.YMax - volume.YMin, volume.ZMax - volume.ZMin) - origin;
			finalNormal /= finalNormal.Length();
			return ((short)(short.MaxValue * Math.Asin(finalNormal.Z)), (short)(short.MaxValue * Math.Atan2(finalNormal.X, -finalNormal.Y)));
		}

		public byte[] Colour(BoundingVolume volume)
		{
			return new byte[3] { 1, 1, 1 };
		}
	}
}
