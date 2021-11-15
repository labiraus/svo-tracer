using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using SvoTracer.Domain.Models;

namespace SvoTracer.Domain
{
	public class CubeBuilder : TreeBuilder
	{
		(float min, float max) x0, y0, z0;
		Vector3 normal1;
		(float min, float max) x1, y1, z1;
		Vector3 normal2;
		(float min, float max) x2, y2, z2;
		Vector3 normal3;
		(float min, float max) x3, y3, z3;
		Vector3 normal4;
		(float min, float max) x4, y4, z4;
		Vector3 normal5;
		(float min, float max) x5, y5, z5;
		Vector3 normal6;
		(float min, float max) x6, y6, z6;

		public CubeBuilder(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e, Vector3 f, Vector3 g, Vector3 h)
		{
			x0 = (new[] { a.X, b.X, c.X, d.X, e.X, f.X, g.X, h.X }.Min(), new[] { a.X, b.X, c.X, d.X, e.X, f.X, g.X, h.X }.Max());
			y0 = (new[] { a.Y, b.Y, c.Y, d.Y, e.Y, f.Y, g.Y, h.Y }.Min(), new[] { a.Y, b.Y, c.Y, d.Y, e.Y, f.Y, g.Y, h.Y }.Max());
			z0 = (new[] { a.Z, b.Z, c.Z, d.Z, e.Z, f.Z, g.Z, h.Z }.Min(), new[] { a.Z, b.Z, c.Z, d.Z, e.Z, f.Z, g.Z, h.Z }.Max());
			x1 = (new[] { a.X, b.X, c.X, d.X }.Min(), new[] { a.X, b.X, c.X, d.X }.Max());
			y1 = (new[] { a.Y, b.Y, c.Y, d.Y }.Min(), new[] { a.Y, b.Y, c.Y, d.Y }.Max());
			z1 = (new[] { a.Z, b.Z, c.Z, d.Z }.Min(), new[] { a.Z, b.Z, c.Z, d.Z }.Max());
			x2 = (new[] { a.X, b.X, e.X, f.X }.Min(), new[] { a.X, b.X, e.X, f.X }.Max());
			y2 = (new[] { a.Y, b.Y, e.Y, f.Y }.Min(), new[] { a.Y, b.Y, e.Y, f.Y }.Max());
			z2 = (new[] { a.Z, b.Z, e.Z, f.Z }.Min(), new[] { a.Z, b.Z, e.Z, f.Z }.Max());
			x3 = (new[] { b.X, c.X, f.X, g.X }.Min(), new[] { b.X, c.X, f.X, g.X }.Max());
			y3 = (new[] { b.Y, c.Y, f.Y, g.Y }.Min(), new[] { b.Y, c.Y, f.Y, g.Y }.Max());
			z3 = (new[] { b.Z, c.Z, f.Z, g.Z }.Min(), new[] { b.Z, c.Z, f.Z, g.Z }.Max());
			x4 = (new[] { c.X, d.X, g.X, h.X }.Min(), new[] { c.X, d.X, g.X, h.X }.Max());
			y4 = (new[] { c.Y, d.Y, g.Y, h.Y }.Min(), new[] { c.Y, d.Y, g.Y, h.Y }.Max());
			z4 = (new[] { c.Z, d.Z, g.Z, h.Z }.Min(), new[] { c.Z, d.Z, g.Z, h.Z }.Max());
			x5 = (new[] { a.X, d.X, e.X, h.X }.Min(), new[] { a.X, d.X, e.X, h.X }.Max());
			y5 = (new[] { a.Y, d.Y, e.Y, h.Y }.Min(), new[] { a.Y, d.Y, e.Y, h.Y }.Max());
			z5 = (new[] { a.Z, d.Z, e.Z, h.Z }.Min(), new[] { a.Z, d.Z, e.Z, h.Z }.Max());
			x6 = (new[] { e.X, f.X, g.X, h.X }.Min(), new[] { e.X, f.X, g.X, h.X }.Max());
			y6 = (new[] { e.Y, f.Y, g.Y, h.Y }.Min(), new[] { e.Y, f.Y, g.Y, h.Y }.Max());
			z6 = (new[] { e.Z, f.Z, g.Z, h.Z }.Min(), new[] { e.Z, f.Z, g.Z, h.Z }.Max());

			normal1 = Plane.CreateFromVertices(a, b, c).Normal;
			normal2 = Plane.CreateFromVertices(a, b, e).Normal;
			normal3 = Plane.CreateFromVertices(b, c, f).Normal;
			normal4 = Plane.CreateFromVertices(c, d, g).Normal;
			normal5 = Plane.CreateFromVertices(a, d, e).Normal;
			normal6 = Plane.CreateFromVertices(e, f, g).Normal;
		}

		protected override bool ContainsGeo((float min, float max) x, (float min, float max) y, (float min, float max) z) =>
				(x.min < x0.max && x.max > x0.min &&
				 y.min < y0.max && y.max > y0.min &&
				 z.min < z0.max && z.max > z0.min);

		protected override bool ContainsAir((float min, float max) x, (float min, float max) y, (float min, float max) z) =>
			  !ContainsGeo(x, y, z) || intersections(x, y, z) > 0;

		protected override Block MakeBlock(Location coordinates, byte depth)
		{
			var range = coordinates.CoordinateRanges(depth);
			var normal = getNormal((range.x.min, range.x.max), (range.y.min, range.y.max), (range.z.min, range.z.max));
			var colour = getColour((range.x.min, range.x.max), (range.y.min, range.y.max), (range.z.min, range.z.max));
			Color c = Color.Black;
			switch (depth)
			{
				case 5:
					c = Color.Yellow;
					break;
				case 6:
					c = Color.Red;
					break;
				case 7:
					c = Color.Blue;
					break;
				case 8:
					c = Color.Green;
					break;
				case 9:
					c = Color.Purple;
					break;
				case 10:
					c = Color.Orange;
					break;
			}
			colour = new byte[] { c.R, c.B, c.G };
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

		private (short pitch, short yaw) getNormal((float, float) x, (float, float) y, (float, float) z)
		{
			var intersect = intersections(x, y, z);
			switch (intersect)
			{
				case Face.Red:
					return ((short)(short.MaxValue * Math.Asin(normal1.Z)), (short)(short.MaxValue * Math.Atan2(normal1.X, -normal1.Y)));
				case Face.Green:
					return ((short)(short.MaxValue * Math.Asin(normal2.Z)), (short)(short.MaxValue * Math.Atan2(normal2.X, -normal2.Y)));
				case Face.Blue:
					return ((short)(short.MaxValue * Math.Asin(normal3.Z)), (short)(short.MaxValue * Math.Atan2(normal3.X, -normal3.Y)));
				case Face.Yellow:
					return ((short)(short.MaxValue * Math.Asin(normal4.Z)), (short)(short.MaxValue * Math.Atan2(normal4.X, -normal4.Y)));
				case Face.Cyan:
					return ((short)(short.MaxValue * Math.Asin(normal5.Z)), (short)(short.MaxValue * Math.Atan2(normal5.X, -normal5.Y)));
				case Face.Magenta:
					return ((short)(short.MaxValue * Math.Asin(normal6.Z)), (short)(short.MaxValue * Math.Atan2(normal6.X, -normal6.Y)));
			}
			return (0, 0);
		}

		private byte[] getColour((float, float) x, (float, float) y, (float, float) z)
		{
			var intersect = intersections(x, y, z);
			var edge = ((int)intersect >> 5 & 1) + ((int)intersect >> 4 & 1) + ((int)intersect >> 3 & 1) + ((int)intersect >> 2 & 1) + ((int)intersect >> 1 & 1) + ((int)intersect & 1);
			if (x.Item2 - x.Item1 < 0.0005 && edge > 1)
			{
				int colour = Color.Black.ToArgb();
				return new byte[] { (byte)(colour >> 24), (byte)(colour >> 16), (byte)(colour >> 8) };
			}

			return faceColour(intersect);
		}

		private Face intersections((float min, float max) x, (float min, float max) y, (float min, float max) z)
		{
			int intersections = 0;
			intersections += (x.min < x1.max && x.max > x1.min &&
				y.min < y1.max && y.max > y1.min &&
				z.min < z1.max && z.max > z1.min) ? 1 : 0;

			intersections += (x.min < x2.max && x.max > x2.min &&
				y.min < y2.max && y.max > y2.min &&
				z.min < z2.max && z.max > z2.min) ? 2 : 0;

			intersections += (x.min < x3.max && x.max > x3.min &&
				y.min < y3.max && y.max > y3.min &&
				z.min < z3.max && z.max > z3.min) ? 4 : 0;

			intersections += (x.min < x4.max && x.max > x4.min &&
				y.min < y4.max && y.max > y4.min &&
				z.min < z4.max && z.max > z4.min) ? 8 : 0;

			intersections += (x.min < x5.max && x.max > x5.min &&
				y.min < y5.max && y.max > y5.min &&
				z.min < z5.max && z.max > z5.min) ? 16 : 0;

			intersections += (x.min < x6.max && x.max > x6.min &&
				y.min < y6.max && y.max > y6.min &&
				z.min < z6.max && z.max > z6.min) ? 32 : 0;
			return (Face)intersections;
		}

		private byte[] faceColour(Face intersect)
		{
			var colour = new Vector3(0f, 0f, 0f);
			float i = 0;
			foreach (Enum value in Enum.GetValues(intersect.GetType()))
				if (intersect.HasFlag(value))
				{
					i++;
					switch (value)
					{
						case Face.Red:
							colour = new Vector3(colour.X + 1f, colour.Y + 0f, colour.Z + 0f);
							break;
						case Face.Green:
							colour = new Vector3(colour.X + 0f, colour.Y + 1f, colour.Z + 0f);
							break;
						case Face.Blue:
							colour = new Vector3(colour.X + 0f, colour.Y + 0f, colour.Z + 1f);
							break;
						case Face.Yellow:
							colour = new Vector3(colour.X + 1f, colour.Y + 1f, colour.Z + 0f);
							break;
						case Face.Cyan:
							colour = new Vector3(colour.X + 0f, colour.Y + 1f, colour.Z + 1f);
							break;
						case Face.Magenta:
							colour = new Vector3(colour.X + 1f, colour.Y + 0f, colour.Z + 1f);
							break;
					}
				}

			if (i == 0)
				colour = new Vector3(1f, 1f, 1f);

			return new byte[] { (byte)(byte.MaxValue / colour.X), (byte)(byte.MaxValue / colour.Y), (byte)(byte.MaxValue / colour.Z) };
		}

		[Flags]
		enum Face : ushort
		{
			Black = 0,
			Red = 1,
			Green = 2,
			Blue = 4,
			Yellow = 8,
			Cyan = 16,
			Magenta = 32,
		}
	}
}
