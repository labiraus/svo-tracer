using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using SvoTracer.Domain.Models;

namespace SvoTracer.Domain
{
	public class SphereBuilder : TreeBuilder
	{
		private readonly Vector3 origin;
		private readonly float r;

		public SphereBuilder(Vector3 origin, float r)
		{
			this.origin = origin;
			this.r = r;
		}
		float squared(float v) { return v * v; }

		protected override bool ContainsGeo((float min, float max) x, (float min, float max) y, (float min, float max) z)
		{
			float dist_squared = r * r;
			/* assume C1 and C2 are element-wise sorted, if not, do that now */
			if (origin.X < x.min) dist_squared -= squared(origin.X - x.min);
			else if (origin.X > x.max) dist_squared -= squared(origin.X - x.max);
			if (origin.Y < y.min) dist_squared -= squared(origin.Y - y.min);
			else if (origin.Y > y.max) dist_squared -= squared(origin.Y - y.max);
			if (origin.Z < z.min) dist_squared -= squared(origin.Z - z.min);
			else if (origin.Z > z.max) dist_squared -= squared(origin.Z - z.max);
			return dist_squared > 0;
		}

		protected override bool ContainsAir((float min, float max) x, (float min, float max) y, (float min, float max) z)
		{
			return
				(new Vector3(x.min, y.min, z.min) - origin).Length() > r ||
				(new Vector3(x.max, y.min, z.min) - origin).Length() > r ||
				(new Vector3(x.min, y.max, z.min) - origin).Length() > r ||
				(new Vector3(x.max, y.max, z.min) - origin).Length() > r ||
				(new Vector3(x.min, y.min, z.max) - origin).Length() > r ||
				(new Vector3(x.max, y.min, z.max) - origin).Length() > r ||
				(new Vector3(x.min, y.max, z.max) - origin).Length() > r ||
				(new Vector3(x.max, y.max, z.max) - origin).Length() > r;
		}

		protected override Block MakeBlock(Location coordinates, byte depth)
		{
			var range = coordinates.CoordinateRanges(depth);
			var normal = getNormal((range.x.min, range.x.max), (range.y.min, range.y.max), (range.z.min, range.z.max));
			var colour = getColour((range.x.min, range.x.max), (range.y.min, range.y.max), (range.z.min, range.z.max));

			return new Block()
			{
				Chunk = MakeChunk(coordinates, depth),
				Child = uint.MaxValue,
				Data = new BlockData()
				{
					NormalPitch = normal.pitch,
					NormalYaw = normal.yaw,
					ColourR = colour[0],
					ColourB = colour[1],
					ColourG = colour[2],
					Opacity = byte.MaxValue,
					Properties = 0
				}
			};
		}

		private (short pitch, short yaw) getNormal((float min, float max) x, (float min, float max) y, (float min, float max) z)
		{
			Vector3 finalNormal = new Vector3(x.max - x.min, y.max - y.min, z.max - z.min) - origin;
			finalNormal /= finalNormal.Length();
			return ((short)(short.MaxValue * Math.Asin(finalNormal.Z)), (short)(short.MaxValue * Math.Atan2(finalNormal.X, -finalNormal.Y)));
		}

		private byte[] getColour((float, float) x, (float, float) y, (float, float) z)
		{
			return new byte[3] { 1, 1, 1 };
		}

	}
}
