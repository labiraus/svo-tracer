using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
	public struct Location : IEquatable<Location>
	{
		public ulong X;
		public ulong Y;
		public ulong Z;
		public Location(ulong value)
		{
			X = value;
			Y = value;
			Z = value;
		}

		public Location(ulong x, ulong y, ulong z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public ulong this[int index]
		{
			get
			{
				if (index == 0)
				{
					return X;
				}

				if (index == 1)
				{
					return Y;
				}

				if (index == 2)
				{
					return Z;
				}

				throw new IndexOutOfRangeException("You tried to access this location vector at index: " + index);
			}

			set
			{
				if (index == 0)
				{
					X = value;
				}
				else if (index == 1)
				{
					Y = value;
				}
				else if (index == 2)
				{
					Z = value;
				}
				else
				{
					throw new IndexOutOfRangeException("You tried to set this location vector at index: " + index);
				}
			}
		}

		private static (float min, float midpoint, float max) CoordinateRange(ulong coordinate, ushort depth)
		{
			float start = 0, end = 1, division = 1;
			const ulong nearMax = ulong.MaxValue - (ulong.MaxValue >> 1);
			for (ushort i = 0; i < depth; i++)
			{
				division /= 2;
				if ((coordinate & (nearMax >> i)) > 0)
					start += division;
				else
					end -= division;
			}
			return (start, (start + end) / 2, end);
		}

		public ((float min, float midpoint, float max) x, (float min, float midpoint, float max) y, (float min, float midpoint, float max) z) CoordinateRanges(ushort depth) => (CoordinateRange(X, depth), CoordinateRange(Y, depth), CoordinateRange(Z, depth));

		/// <inheritdoc />
		public override string ToString()
		{
			return string.Format("({0}{3} {1}{3} {2})", Convert.ToString((long)X, 2).PadLeft(64, '0'), Convert.ToString((long)Y, 2).PadLeft(64, '0'), Convert.ToString((long)Z, 2).PadLeft(64, '0'), CultureInfo.CurrentCulture.TextInfo.ListSeparator);
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			return obj is Location && Equals((Location)obj);
		}

		/// <inheritdoc />
		public bool Equals(Location other)
		{
			return X == other.X &&
				   Y == other.Y &&
				   Z == other.Z;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(X, Y, Z);
		}

		[Pure]
		public void Deconstruct(out ulong x, out ulong y, out ulong z)
		{
			x = X;
			y = Y;
			z = Z;
		}
	}
}
