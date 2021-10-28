using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
	[Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Location : IEquatable<Location>
	{
		public ulong X { get; set; }
        public ulong Y { get; set; }
        public ulong Z { get; set; }
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

        public static readonly int SizeInBytes = Unsafe.SizeOf<Location>();

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("({0}{3} {1}{3} {2})", X, Y, Z, CultureInfo.CurrentCulture.TextInfo.ListSeparator);
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
