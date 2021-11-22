using SvoTracer.Domain.Interfaces;
using System;
using SvoTracer.Domain.Models;
using OpenTK.Mathematics;

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
			if (origin.X < volume.MinX) dist_squared -= squared(origin.X - volume.MinX);
			else if (origin.X > volume.MaxX) dist_squared -= squared(origin.X - volume.MaxX);
			if (origin.Y < volume.MinY) dist_squared -= squared(origin.Y - volume.MinY);
			else if (origin.Y > volume.MaxY) dist_squared -= squared(origin.Y - volume.MaxY);
			if (origin.Z < volume.MinZ) dist_squared -= squared(origin.Z - volume.MinZ);
			else if (origin.Z > volume.MaxZ) dist_squared -= squared(origin.Z - volume.MaxZ);
			return dist_squared > 0;
		}

		public bool ContainsAir(BoundingVolume volume)
		{
			return
				(new Vector3(volume.MinX, volume.MinY, volume.MinZ) - origin).Length > r ||
				(new Vector3(volume.MaxX, volume.MinY, volume.MinZ) - origin).Length > r ||
				(new Vector3(volume.MinX, volume.MaxY, volume.MinZ) - origin).Length > r ||
				(new Vector3(volume.MaxX, volume.MaxY, volume.MinZ) - origin).Length > r ||
				(new Vector3(volume.MinX, volume.MinY, volume.MaxZ) - origin).Length > r ||
				(new Vector3(volume.MaxX, volume.MinY, volume.MaxZ) - origin).Length > r ||
				(new Vector3(volume.MinX, volume.MaxY, volume.MaxZ) - origin).Length > r ||
				(new Vector3(volume.MaxX, volume.MaxY, volume.MaxZ) - origin).Length > r;
		}

		public bool WithinBounds(BoundingVolume volume) =>
			(volume.MinX < max.X && volume.MaxX > min.X &&
			 volume.MinY < max.Y && volume.MaxY > min.Y &&
			 volume.MinZ < max.Z && volume.MaxZ > min.Z);

		public Vector3 Normal(BoundingVolume volume)
		{
			float x = ((volume.MaxX - volume.MinX) / 2) + volume.MinX;
			float y = ((volume.MaxY - volume.MinY) / 2) + volume.MinY;
			float z = ((volume.MaxZ - volume.MinZ) / 2) + volume.MinZ;
			return new Vector3(x - origin.X, y - origin.Y, z - origin.Z);
		}

		public byte[] Colour(BoundingVolume volume)
		{
			return new byte[3] { 255, 0, 0 };
		}
	}
}
