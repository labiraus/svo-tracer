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
			x0 = (new float[] { a.X, b.X, c.X, d.X, e.X, f.X, g.X, h.X }.Min(), new float[] { a.X, b.X, c.X, d.X, e.X, f.X, g.X, h.X }.Max());
			y0 = (new float[] { a.Y, b.Y, c.Y, d.Y, e.Y, f.Y, g.Y, h.Y }.Min(), new float[] { a.Y, b.Y, c.Y, d.Y, e.Y, f.Y, g.Y, h.Y }.Max());
			z0 = (new float[] { a.Z, b.Z, c.Z, d.Z, e.Z, f.Z, g.Z, h.Z }.Min(), new float[] { a.Z, b.Z, c.Z, d.Z, e.Z, f.Z, g.Z, h.Z }.Max());
			x1 = (new float[] { a.X, b.X, c.X, d.X }.Min(), new float[] { a.X, b.X, c.X, d.X }.Max());
			y1 = (new float[] { a.Y, b.Y, c.Y, d.Y }.Min(), new float[] { a.Y, b.Y, c.Y, d.Y }.Max());
			z1 = (new float[] { a.Z, b.Z, c.Z, d.Z }.Min(), new float[] { a.Z, b.Z, c.Z, d.Z }.Max());
			x2 = (new float[] { a.X, b.X, e.X, f.X }.Min(), new float[] { a.X, b.X, e.X, f.X }.Max());
			y2 = (new float[] { a.Y, b.Y, e.Y, f.Y }.Min(), new float[] { a.Y, b.Y, e.Y, f.Y }.Max());
			z2 = (new float[] { a.Z, b.Z, e.Z, f.Z }.Min(), new float[] { a.Z, b.Z, e.Z, f.Z }.Max());
			x3 = (new float[] { b.X, c.X, f.X, g.X }.Min(), new float[] { b.X, c.X, f.X, g.X }.Max());
			y3 = (new float[] { b.Y, c.Y, f.Y, g.Y }.Min(), new float[] { b.Y, c.Y, f.Y, g.Y }.Max());
			z3 = (new float[] { b.Z, c.Z, f.Z, g.Z }.Min(), new float[] { b.Z, c.Z, f.Z, g.Z }.Max());
			x4 = (new float[] { c.X, d.X, g.X, h.X }.Min(), new float[] { c.X, d.X, g.X, h.X }.Max());
			y4 = (new float[] { c.Y, d.Y, g.Y, h.Y }.Min(), new float[] { c.Y, d.Y, g.Y, h.Y }.Max());
			z4 = (new float[] { c.Z, d.Z, g.Z, h.Z }.Min(), new float[] { c.Z, d.Z, g.Z, h.Z }.Max());
			x5 = (new float[] { a.X, d.X, e.X, h.X }.Min(), new float[] { a.X, d.X, e.X, h.X }.Max());
			y5 = (new float[] { a.Y, d.Y, e.Y, h.Y }.Min(), new float[] { a.Y, d.Y, e.Y, h.Y }.Max());
			z5 = (new float[] { a.Z, d.Z, e.Z, h.Z }.Min(), new float[] { a.Z, d.Z, e.Z, h.Z }.Max());
			x6 = (new float[] { e.X, f.X, g.X, h.X }.Min(), new float[] { e.X, f.X, g.X, h.X }.Max());
			y6 = (new float[] { e.Y, f.Y, g.Y, h.Y }.Min(), new float[] { e.Y, f.Y, g.Y, h.Y }.Max());
			z6 = (new float[] { e.Z, f.Z, g.Z, h.Z }.Min(), new float[] { e.Z, f.Z, g.Z, h.Z }.Max());

			normal1 = Plane.CreateFromVertices(a, b, c).Normal;
			normal2 = Plane.CreateFromVertices(a, b, e).Normal;
			normal3 = Plane.CreateFromVertices(b, c, f).Normal;
			normal4 = Plane.CreateFromVertices(c, d, g).Normal;
			normal5 = Plane.CreateFromVertices(a, d, e).Normal;
			normal6 = Plane.CreateFromVertices(e, f, g).Normal;
		}

		protected override bool ContainsGeometry((float min, float max) x, (float min, float max) y, (float min, float max) z) =>
				x.min <= x0.max && x.max >= x0.min &&
				y.min <= y0.max && y.max >= y0.min &&
				z.min <= z0.max && z.max >= z0.min;

		protected override bool ContainsAir((float min, float max) x, (float min, float max) y, (float min, float max) z) =>
			  !ContainsGeometry(x, y, z) || intersections(x, y, z) > 0;

		protected override Block MakeBlock(Location coordinates, ushort depth)
		{
			var xRange = CoordinateRange(coordinates[0], depth);
			var yRange = CoordinateRange(coordinates[1], depth);
			var zRange = CoordinateRange(coordinates[2], depth);
			var normal = getNormal((xRange.min, xRange.max), (yRange.min, yRange.max), (zRange.min, zRange.max));
			var colour = getColour((xRange.min, xRange.max), (yRange.min, yRange.max), (zRange.min, zRange.max));
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

		private (short pitch, short yaw) getNormal((float, float) a, (float, float) b, (float, float) c)
		{
			var intersect = intersections(a, b, c);
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

		private byte[] getColour((float, float) a, (float, float) b, (float, float) c)
		{
			var intersect = intersections(a, b, c);
			var edge = ((int)intersect >> 5 & 1) + ((int)intersect >> 4 & 1) + ((int)intersect >> 3 & 1) + ((int)intersect >> 2 & 1) + ((int)intersect >> 1 & 1) + ((int)intersect & 1);
			if (a.Item2 - a.Item1 < 0.0005 && edge > 1)
			{
				int colour = Color.Black.ToArgb();
				return new byte[] { (byte)(colour >> 24), (byte)(colour >> 16), (byte)(colour >> 8) };
			}

			return faceColour(intersect);
		}

		private Face intersections((float min, float max) a, (float min, float max) b, (float min, float max) c)
		{
			int intersections = 0;
			intersections += (a.min <= x1.max && a.max >= x1.min &&
				b.min <= y1.max && b.max >= y1.min &&
				c.min <= z1.max && c.max >= z1.min) ? 1 : 0;

			intersections += (a.min <= x2.max && a.max >= x2.min &&
				b.min <= y2.max && b.max >= y2.min &&
				c.min <= z2.max && c.max >= z2.min) ? 2 : 0;

			intersections += (a.min <= x3.max && a.max >= x3.min &&
				b.min <= y3.max && b.max >= y3.min &&
				c.min <= z3.max && c.max >= z3.min) ? 4 : 0;

			intersections += (a.min <= x4.max && a.max >= x4.min &&
				b.min <= y4.max && b.max >= y4.min &&
				c.min <= z4.max && c.max >= z4.min) ? 8 : 0;

			intersections += (a.min <= x5.max && a.max >= x5.min &&
				b.min <= y5.max && b.max >= y5.min &&
				c.min <= z5.max && c.max >= z5.min) ? 16 : 0;

			intersections += (a.min <= x6.max && a.max >= x6.min &&
				b.min <= y6.max && b.max >= y6.min &&
				c.min <= z6.max && c.max >= z6.min) ? 32 : 0;
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
