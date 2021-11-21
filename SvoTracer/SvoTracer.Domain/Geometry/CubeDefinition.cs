using SvoTracer.Domain.Interfaces;
using System;
using System.Drawing;
using System.Linq;
using SvoTracer.Domain.Models;
using OpenTK.Mathematics;

namespace SvoTracer.Domain.Geometry
{
	public class CubeDefinition : IGeometryDefinition
	{
		private readonly Box3 box;

		public CubeDefinition(Vector3 min, Vector3 max)
		{
			box = new(min, max);
		}

		private Face intersections(BoundingVolume volume)
		{
			Face intersections = Face.None;
			if (ContainsGeo(volume))
			{
				intersections |= (volume.MinX < box.Min.X && volume.MaxX > box.Min.X) ? Face.Front : 0;

				intersections |= (volume.MinX < box.Max.X && volume.MaxX > box.Max.X) ? Face.Back : 0;

				intersections |= (volume.MinY < box.Max.Y && volume.MaxY > box.Max.Y) ? Face.Top : 0;

				intersections |= (volume.MinY < box.Min.Y && volume.MaxY > box.Min.Y) ? Face.Bottom : 0;

				intersections |= (volume.MinZ < box.Min.Z && volume.MaxZ > box.Min.Z) ? Face.Left : 0;

				intersections |= (volume.MinZ < box.Max.Z && volume.MaxZ > box.Max.Z) ? Face.Right : 0;
			}
			return intersections;
		}

		private byte[] faceColour(Face intersect)
		{
			float r = 0, g = 0, b = 0;
			foreach (Enum value in Enum.GetValues(intersect.GetType()))
				if (intersect.HasFlag(value))
					switch (value)
					{
						case Face.Front:
							r++;
							break;
						case Face.Bottom:
							//g++;
							break;
						case Face.Left:
							//b++;
							break;
						case Face.Back:
							//r++;
							//g++;
							break;
						case Face.Top:
							//g++;
							//b++;
							break;
						case Face.Right:
							//r++;
							//b++;
							break;
					}

			float m = Math.Max(Math.Max(r, g), b);
			return m == 0 ?
				new byte[] { 255, 255, 255 } :
				new byte[] { (byte)(byte.MaxValue / (r / m)), (byte)(byte.MaxValue / (g / m)), (byte)(byte.MaxValue / (b / m)) };
		}

		public bool WithinBounds(BoundingVolume volume) => box.Contains(new Box3(volume.MinX, volume.MinY, volume.MinZ, volume.MaxX, volume.MaxY, volume.MaxZ));

		public bool ContainsGeo(BoundingVolume volume) => box.Contains(new Box3(volume.MinX, volume.MinY, volume.MinZ, volume.MaxX, volume.MaxY, volume.MaxZ));

		public bool ContainsAir(BoundingVolume volume) =>
			  !ContainsGeo(volume) || intersections(volume) > 0;

		public (short pitch, short yaw) Normal(BoundingVolume volume)
		{
			var intersect = intersections(volume);
			int i = 0;
			Vector3 finalNormal = Vector3.Zero;
			foreach (Enum value in Enum.GetValues(intersect.GetType()))
				if (intersect.HasFlag(value))
				{
					i++;
					Vector3 normal;
					switch (value)
					{
						case Face.Front:
							normal = new Vector3(-1, 0, 0);
							break;
						case Face.Bottom:
							normal = new Vector3(0, -1, 0);
							break;
						case Face.Left:
							normal = new Vector3(0, 0, -1);
							break;
						case Face.Back:
							normal = new Vector3(1, 0, 0);
							break;
						case Face.Top:
							normal = new Vector3(0, 1, 0);
							break;
						case Face.Right:
							normal = new Vector3(0, 0, 1);
							break;
						default:
							continue;
					}

					if (finalNormal + normal != Vector3.Zero)
						finalNormal += normal;
				}
			if (i == 0)
				return (0, 0);

			finalNormal = finalNormal.Normalized();
			return ((short)(short.MaxValue * Math.Asin(finalNormal.Z)), (short)(short.MaxValue * Math.Atan2(finalNormal.X, -finalNormal.Y)));
		}

		public byte[] Colour(BoundingVolume volume)
		{
			var intersect = intersections(volume);
			var edge = ((int)intersect >> 5 & 1) + ((int)intersect >> 4 & 1) + ((int)intersect >> 3 & 1) + ((int)intersect >> 2 & 1) + ((int)intersect >> 1 & 1) + ((int)intersect & 1);
			if (volume.MaxX - volume.MinX < 0.0005 && edge > 1)
			{
				return new byte[] { 0, 0, 0 };
				//int colour = Color.Black.ToArgb();
				//return new byte[] { (byte)(colour >> 24), (byte)(colour >> 16), (byte)(colour >> 8) };
			}

			return faceColour(intersect);
		}

		[Flags]
		enum Face : ushort
		{
			None = 0,
			Front = 1,  //Red
			Bottom = 2, //Green
			Left = 4,   //Blue
			Back = 8,   //Yellow
			Top = 16,   //Cyan
			Right = 32, //Magenta
		}
	}
}
