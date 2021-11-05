using SvoTracer.Domain.Models;
using SvoTracer.Domain.Serializers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;
using System.Threading.Tasks;

namespace SvoTracer.Domain
{
	public abstract class TreeBuilder : ITreeBuilder
	{
		protected const ulong nearMax = ulong.MaxValue - (ulong.MaxValue >> 1);

		/// <summary>
		/// Determines whether the volume bounded within the x/y/z range contains geometry
		/// </summary>
		/// <param name="a">x min/maximum</param>
		/// <param name="b">y min/maximum</param>
		/// <param name="c">z min/maximum</param>
		/// <returns></returns>
		protected abstract bool ContainsGeometry((float, float) a, (float, float) b, (float, float) c);
		/// <summary>
		/// Determines whether the volume bounded within the x/y/z range contains empty air
		/// </summary>
		/// <param name="a">x min/maximum</param>
		/// <param name="b">y min/maximum</param>
		/// <param name="c">z min/maximum</param>
		/// <returns></returns>
		protected abstract bool ContainsAir((float, float) a, (float, float) b, (float, float) c);
		/// <summary>
		/// Builds a Block for a set of binary coordinates
		/// </summary>
		/// <param name="coordinates"></param>
		/// <param name="depth"></param>
		/// <returns></returns>
		protected abstract Block MakeBlock(Location coordinates, ushort depth);

		/// <summary>
		/// Builds an octree using inherited class's MakeBlock method
		/// </summary>
		/// <param name="N">Depth of inviolate tree</param>
		/// <param name="maxDepth">Initial tree depth</param>
		/// <param name="maxSize">Maximum number of blocks available</param>
		/// <returns>Fully build Octree</returns>
		public Octree BuildTree(byte N, ushort maxDepth, uint maxSize = 0)
		{
			// Increases MaxSize to allow at least 1 layer of block data
			maxSize = maxSize > (uint)(1 << (3 * N + 6)) ? maxSize : (uint)(1 << (3 * N + 6));
			Octree tree = new()
			{
				N = N,
				BaseBlocks = new ushort[PowSum(N)],
				Blocks = new Block[maxSize]
			};
			buildBaseChunks(ref tree);
			Queue<uint> freeAddresses = new();
			bool[] childrenNeeded = populateInitialBlocks(ref tree, freeAddresses);

			//Push all addresses from N+3 and over into free address stack
			if ((maxSize >> 3) > (uint)(1 << (3 * (tree.N + 1))))
				for (uint i = (uint)(1 << (3 * (tree.N + 1))); i < (maxSize >> 3); i++)
					freeAddresses.Enqueue(i);

			//Iterates over level N+2
			for (uint x = 0; x < (uint)(1 << N + 2); x++)
			{
				var xcoord = IntToBinaryCoordinate(x, N + 2);
				for (uint y = 0; y < (uint)(1 << N + 2); y++)
				{
					var ycoord = IntToBinaryCoordinate(y, N + 2);
					for (uint z = 0; z < (uint)(1 << N + 2); z++)
					{
						var i = Interleave(x, y, z);
						if (childrenNeeded[i])
						{
							var zcoord = IntToBinaryCoordinate(z, N + 2);
							uint address = freeAddresses.Dequeue() << 3;
							tree.Blocks[i].Child = address;
							BuildBlocks(ref tree, freeAddresses, address, new Location(xcoord, ycoord, zcoord), (ushort)(N + 2), maxDepth);
						}
					}
				}
			}

			// Trim final tree size down to populated volume
			tree.Blocks = tree.Blocks.Take((int)tree.BlockCount).ToArray();
			return tree;
		}

		/// <summary>
		/// Iterates over level N+1 base chunks and creates Blocks at the N+2 level
		/// </summary>
		/// <param name="tree"></param>
		/// <param name="freeAddresses"></param>
		/// <returns>Whether blocks require children to be populated</returns>
		private bool[] populateInitialBlocks(ref Octree tree, Queue<uint> freeAddresses)
		{
			bool[] childrenNeeded = new bool[(uint)(1 << (3 * tree.N + 6))];
			uint baseStart = PowSum((byte)(tree.N - 1));
			ushort scanDepth = (ushort)(tree.N + 1);
			uint scanWidth = (uint)1 << scanDepth;
			ushort blockDepth = (ushort)(tree.N + 2);

			for (uint z = 0; z < scanWidth; z++)
			{
				var zcoord = IntToBinaryCoordinate(z, scanDepth);
				for (uint y = 0; y < scanWidth; y++)
				{
					var ycoord = IntToBinaryCoordinate(y, scanDepth);
					for (uint x = 0; x < scanWidth; x++)
					{
						var xcoord = IntToBinaryCoordinate(x, scanDepth);
						var i = Interleave(x, y, z);
						if ((tree.BaseBlocks[baseStart + (i >> 3)] >> (int)((i & 7) * 2) & 0b11) == 0b11)
							//If there's children to be created, create 8 N+2 blocks
							for (uint z2 = 0; z2 < 2; z2++)
								for (uint y2 = 0; y2 < 2; y2++)
									for (uint x2 = 0; x2 < 2; x2++)
									{
										var j = Interleave(x2, y2, z2);
										var block = MakeBlock(new Location
										(
											xcoord + IntToBinaryCoordinate(x2, blockDepth),
											ycoord + IntToBinaryCoordinate(y2, blockDepth),
											zcoord + IntToBinaryCoordinate(z2, blockDepth)
										), blockDepth);
										tree.Blocks[(i << 3) + j] = block;
										childrenNeeded[(i << 3) + j] = CanHaveChildren(block.Chunk);

										//Increase maxBlock to reflect maximum address residency
										if (tree.BlockCount < (i << 3) + j + 1) tree.BlockCount = (i << 3) + j + 1;
									}
						else
							//Push unused address into stack
							freeAddresses.Enqueue(i);

					}
				}
			}
			return childrenNeeded;
		}

		private void buildBaseChunks(ref Octree tree)
		{
			//Creates base levels 1 to N
			for (ushort depth = 1; depth <= tree.N; depth++)
			{
				for (uint z = 0; z < 1 << depth; z++)
				{
					ulong zcoord = IntToBinaryCoordinate(z, depth);
					for (uint y = 0; y < 1 << depth; y++)
					{
						ulong ycoord = IntToBinaryCoordinate(y, depth);
						for (uint x = 0; x < 1 << depth; x++)
						{
							ulong xcoord = IntToBinaryCoordinate(x, depth);
							var address = PowSum((ushort)(depth - 1)) + Interleave(x, y, z);
							tree.BaseBlocks[address] = MakeBaseChunk(new Location(xcoord, ycoord, zcoord), depth);
						}
					}
				}
			}
		}

		/// <summary>
		/// Adds 8 consecutive Blocks into tree and recursively adds children
		/// </summary>
		/// <param name="tree">Octree to insert into</param>
		/// <param name="freeAddresses">Queue of unallocated addresses</param>
		/// <param name="address"></param>
		/// <param name="coordinates"></param>
		/// <param name="currentDepth"></param>
		protected void BuildBlocks(ref Octree tree, Queue<uint> freeAddresses, uint address, Location coordinates, ushort currentDepth, ushort maxDepth)
		{
			for (byte i = 0; i < 8; i++)
			{
				ulong edge = nearMax >> (currentDepth - 1);
				var newCoordinates = new Location(
					coordinates[0] + ((i & 0b001) == 0b001 ? edge : 0),
					coordinates[1] + ((i & 0b010) == 0b010 ? edge : 0),
					coordinates[2] + ((i & 0b100) == 0b100 ? edge : 0));
				var block = MakeBlock(newCoordinates, currentDepth);
				tree.Blocks[address + i] = block;
				if (tree.BlockCount < address + i) tree.BlockCount = address + i + 1;

				if (CanHaveChildren(block.Chunk) && currentDepth < maxDepth && freeAddresses.Any())
				{
					var newAddress = freeAddresses.Dequeue() << 3;
					block.Child = newAddress;
					BuildBlocks(ref tree, freeAddresses, newAddress, newCoordinates, (ushort)(currentDepth + 1), maxDepth);
				}
			}
		}

		protected ushort MakeBaseChunk(Location coordinates, ushort depth)
		{
			return BuildChunk(CoordinateRange(coordinates[0], depth), CoordinateRange(coordinates[1], depth), CoordinateRange(coordinates[2], depth));
		}

		/// <summary>
		/// Creates chunk data from coordinates
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		/// <returns></returns>
		protected ushort BuildChunk((float min, float midpoint, float max) x, (float min, float midpoint, float max) y, (float min, float midpoint, float max) z)
		{
			var chunk = (ushort)((ContainsAir((x.min, x.midpoint), (y.min, y.midpoint), (z.min, z.midpoint)) ? 1 : 0)
				+ (ContainsGeometry((x.min, x.midpoint), (y.min, y.midpoint), (z.min, z.midpoint)) ? 1 << 1 : 0)
				+ (ContainsAir((x.midpoint, x.max), (y.min, y.midpoint), (z.min, z.midpoint)) ? 1 << 2 : 0)
				+ (ContainsGeometry((x.midpoint, x.max), (y.min, y.midpoint), (z.min, z.midpoint)) ? 1 << 3 : 0)
				+ (ContainsAir((x.min, x.midpoint), (y.midpoint, y.max), (z.min, z.midpoint)) ? 1 << 4 : 0)
				+ (ContainsGeometry((x.min, x.midpoint), (y.midpoint, y.max), (z.min, z.midpoint)) ? 1 << 5 : 0)
				+ (ContainsAir((x.midpoint, x.max), (y.midpoint, y.max), (z.min, z.midpoint)) ? 1 << 6 : 0)
				+ (ContainsGeometry((x.midpoint, x.max), (y.midpoint, y.max), (z.min, z.midpoint)) ? 1 << 7 : 0)
				+ (ContainsAir((x.min, x.midpoint), (y.min, y.midpoint), (z.midpoint, z.max)) ? 1 << 8 : 0)
				+ (ContainsGeometry((x.min, x.midpoint), (y.min, y.midpoint), (z.midpoint, z.max)) ? 1 << 9 : 0)
				+ (ContainsAir((x.midpoint, x.max), (y.min, y.midpoint), (z.midpoint, z.max)) ? 1 << 10 : 0)
				+ (ContainsGeometry((x.midpoint, x.max), (y.min, y.midpoint), (z.midpoint, z.max)) ? 1 << 11 : 0)
				+ (ContainsAir((x.min, x.midpoint), (y.midpoint, y.max), (z.midpoint, z.max)) ? 1 << 12 : 0)
				+ (ContainsGeometry((x.min, x.midpoint), (y.midpoint, y.max), (z.midpoint, z.max)) ? 1 << 13 : 0)
				+ (ContainsAir((x.midpoint, x.max), (y.midpoint, y.max), (z.midpoint, z.max)) ? 1 << 14 : 0)
				+ (ContainsGeometry((x.midpoint, x.max), (y.midpoint, y.max), (z.midpoint, z.max)) ? 1 << 15 : 0));
			return chunk;
		}

		/// <summary>
		/// Determines if chunk data indicates a child that contains both geometry and empty air
		/// </summary>
		/// <param name="chunk">Bit pairs representing </param>
		/// <returns>true if any child contains an interface</returns>
		protected static bool CanHaveChildren(ushort chunk)
		{
			return (
				((chunk & 0b0000000000000011) == 0b0000000000000011) ||
				((chunk & 0b0000000000001100) == 0b0000000000001100) ||
				((chunk & 0b0000000000110000) == 0b0000000000110000) ||
				((chunk & 0b0000000011000000) == 0b0000000011000000) ||
				((chunk & 0b0000001100000000) == 0b0000001100000000) ||
				((chunk & 0b0000110000000000) == 0b0000110000000000) ||
				((chunk & 0b0011000000000000) == 0b0011000000000000) ||
				((chunk & 0b1100000000000000) == 0b1100000000000000));
		}

		protected static ulong IntToBinaryCoordinate(uint x, int depth)
		{
			return IntToBinaryCoordinate(x, (ushort)depth);
		}

		protected static ulong IntToBinaryCoordinate(uint x, ushort depth)
		{
			ulong output = 0;
			for (ushort i = 0; i <= depth; i++)
			{
				if ((x >> i & 1) == 1)
					output += (nearMax >> (depth - i - 1));
			}
			return output;
		}

		protected static (float, float, float) CoordinateRange(ulong binaryCoordinate, ushort depth)
		{
			float start = 0, end = 1, division = 1;
			for (ushort i = 0; i < depth; i++)
			{
				division /= 2;
				if ((binaryCoordinate & (nearMax >> i)) > 0)
					start += division;
				else
					end -= division;
			}
			return (start, (start + end) / 2, end);
		}

		/// <summary>
		/// Combines 3 values one bit at a time to create a predictable address
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		/// <returns></returns>
		protected static uint Interleave(uint x, uint y, uint z)
		{
			uint output = 0;
			for (int i = 0; x >= (uint)(1 << i) || y >= (uint)(1 << i) || z >= (uint)(1 << i); i++)
			{
				output += (x >> i & 1) << (i * 3);
				output += (y >> i & 1) << ((i * 3) + 1);
				output += (z >> i & 1) << ((i * 3) + 2);
			}
			return output;
		}

		public static uint PowSum(ushort depth)
		{
			uint output = 0;
			for (int i = 1; i <= depth; i++)
				output += (uint)(1 << (3 * i));
			return output;
		}
	}
}
