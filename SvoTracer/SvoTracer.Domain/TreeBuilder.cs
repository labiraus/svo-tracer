using SvoTracer.Domain.Interfaces;
using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SvoTracer.Domain
{
	public class TreeBuilder : ITreeBuilder
	{
		protected const ulong nearMax = ulong.MaxValue - (ulong.MaxValue >> 1);
		protected readonly IList<IGeometryDefinition> geometryDefinitions = new List<IGeometryDefinition>();

		public TreeBuilder()
		{

		}

		public TreeBuilder(IEnumerable<IGeometryDefinition> geometry)
		{
			this.geometryDefinitions = geometry.ToList();
		}

		public void AddGeometry(IGeometryDefinition geometryDefinition)
		{
			geometryDefinitions.Add(geometryDefinition);
		}

		protected virtual bool ContainsAir(BoundingVolume volume)
		{
			foreach (var definition in geometryDefinitions.Where(g => g.WithinBounds(volume)))
				if (!definition.ContainsAir(volume))
					return false;
			return true;
		}

		protected virtual bool ContainsGeo(BoundingVolume volume)
		{
			foreach (var definition in geometryDefinitions.Where(g => g.WithinBounds(volume)))
				if (definition.ContainsGeo(volume))
					return true;
			return false;
		}

		protected virtual (short pitch, short yaw) GetNormal(BoundingVolume volume)
		{
			short pitch = 0, yaw = 0;
			foreach (var definition in geometryDefinitions.Where(g => g.WithinBounds(volume)))
			{
				if (definition.ContainsGeo(volume))
					(pitch, yaw) = definition.Normal(volume);
			}
			return (pitch, yaw);
		}

		protected virtual byte[] GetColour(BoundingVolume volume)
		{
			var colour = new byte[3];
			foreach (var definition in geometryDefinitions.Where(g => g.WithinBounds(volume)))
			{
				if (definition.ContainsGeo(volume))
					colour = definition.Colour(volume);
			}
			return colour;
		}

		/// <summary>
		/// Builds a Block for a set of binary coordinates
		/// </summary>
		/// <param name="coordinates"></param>
		/// <param name="depth"></param>
		/// <returns></returns>
		protected virtual Block MakeBlock(Location coordinates, byte depth)
		{
			var volume = new BoundingVolume(coordinates, depth);
			var normal = GetNormal(volume);
			var colour = GetColour(volume);

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

		/// <summary>
		/// Builds an octree using inherited class's MakeBlock method
		/// </summary>
		/// <param name="baseDepth">Depth of inviolate tree</param>
		/// <param name="maxDepth">Initial tree depth</param>
		/// <param name="maxSize">Maximum number of blocks available</param>
		/// <returns>Fully build Octree</returns>
		public Octree BuildTree(byte baseDepth, byte maxDepth, uint maxSize = 0)
		{
			// Increases MaxSize to allow at least 1 layer of block data
			maxSize = maxSize > (uint)(1 << (3 * baseDepth + 6)) ? maxSize : (uint)(1 << (3 * baseDepth + 6));
			Octree tree = new()
			{
				BaseDepth = baseDepth,
				BaseBlocks = new ushort[PowSum(baseDepth)],
				Blocks = new Block[maxSize]
			};
			BuildBaseChunks(ref tree);
			(bool[] childrenNeeded, Queue<uint> freeAddresses) = PopulateInitialBlocks(ref tree);

			//Push all addresses from BaseDepth+3 and over into free address stack
			if ((maxSize >> 3) > (uint)(1 << (3 * (tree.BaseDepth + 1))))
				for (uint i = (uint)(1 << (3 * (tree.BaseDepth + 1))); i < (maxSize >> 3); i++)
					freeAddresses.Enqueue(i);

			if (maxDepth > (ushort)(baseDepth + 2))
			{
				//Iterates over level BaseDepth+2 and builds the requested child blocks
				for (uint x = 0; x < (uint)(1 << baseDepth + 2); x++)
				{
					var xcoord = IntToBinaryCoordinate(x, (byte)(baseDepth + 2));
					for (uint y = 0; y < (uint)(1 << baseDepth + 2); y++)
					{
						var ycoord = IntToBinaryCoordinate(y, (byte)(baseDepth + 2));
						for (uint z = 0; z < (uint)(1 << baseDepth + 2); z++)
						{
							var initialAddress = Interleave(x, y, z);
							if (childrenNeeded[initialAddress])
							{
								var zcoord = IntToBinaryCoordinate(z, (byte)(baseDepth + 2));
								uint address = freeAddresses.Dequeue() << 3;
								tree.Blocks[initialAddress].Child = address;
								BuildBlocks(ref tree, freeAddresses, address, new Location(xcoord, ycoord, zcoord), (byte)(baseDepth + 3), maxDepth);
							}
						}
					}
				}
			}

			// Trim final tree size down to populated volume
			tree.Blocks = tree.Blocks.Take((int)tree.BlockCount).ToArray();
			if (!checkTree(tree, maxDepth))
				Console.WriteLine("bad tree");
			//throw new Exception("Bad tree");
			return tree;
		}

		private void BuildBaseChunks(ref Octree tree)
		{
			//Creates base levels 1 to BaseDepth
			for (byte depth = 1; depth <= tree.BaseDepth; depth++)
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
							var address = PowSum((byte)(depth - 1)) + Interleave(x, y, z);
							tree.BaseBlocks[address] = MakeChunk(new Location(xcoord, ycoord, zcoord), depth);
						}
					}
				}
			}
		}

		/// <summary>
		/// Iterates over level BaseDepth+1 base chunks and creates Blocks at the BaseDepth+2 level
		/// </summary>
		/// <param name="tree"></param>
		/// <param name="freeAddresses"></param>
		/// <returns>Whether blocks require children to be populated</returns>
		private (bool[] childrenNeeded, Queue<uint> freeAddresses) PopulateInitialBlocks(ref Octree tree)
		{
			byte blockDepth = (byte)(tree.BaseDepth + 2);
			bool[] childrenNeeded = new bool[(uint)(1 << (3 * blockDepth))];
			uint baseStart = PowSum((byte)(tree.BaseDepth - 1));
			byte scanDepth = (byte)(tree.BaseDepth + 1);
			uint scanWidth = (uint)1 << scanDepth;
			var empytAddresses = new List<uint>();
			for (uint z = 0; z < scanWidth; z++)
			{
				// Populates the first BaseDepth+1 coordinates with 
				var zcoord = IntToBinaryCoordinate(z, scanDepth);
				for (uint y = 0; y < scanWidth; y++)
				{
					var ycoord = IntToBinaryCoordinate(y, scanDepth);
					for (uint x = 0; x < scanWidth; x++)
					{
						var xcoord = IntToBinaryCoordinate(x, scanDepth);
						var basePosition = Interleave(x, y, z);
						if ((tree.BaseBlocks[baseStart + (basePosition >> 3)] >> (int)((basePosition & 7) * 2) & 0b11) == 0b11)
							//If there's children to be created, create 8 BaseDepth+2 blocks
							for (uint z2 = 0; z2 < 2; z2++)
								for (uint y2 = 0; y2 < 2; y2++)
									for (uint x2 = 0; x2 < 2; x2++)
									{
										var localAddress = (basePosition << 3) + Interleave(x2, y2, z2);
										var block = MakeBlock(new Location
										(
											xcoord + IntToBinaryCoordinate(x2, blockDepth),
											ycoord + IntToBinaryCoordinate(y2, blockDepth),
											zcoord + IntToBinaryCoordinate(z2, blockDepth)
										), blockDepth);
										tree.Blocks[localAddress] = block;
										childrenNeeded[localAddress] = CanHaveChildren(block.Chunk);

										//Increase maxBlock to reflect maximum address residency
										if (tree.BlockCount < localAddress + 1) tree.BlockCount = localAddress + 1;
									}
						else
							//Push unused address into stack
							empytAddresses.Add(basePosition);

					}
				}
			}
			empytAddresses.Sort();
			return (childrenNeeded, new Queue<uint>(empytAddresses));
		}

		/// <summary>
		/// Adds 8 consecutive Blocks into tree and recursively adds children
		/// </summary>
		/// <param name="tree">Octree to insert into</param>
		/// <param name="freeAddresses">Queue of unallocated addresses</param>
		/// <param name="address"></param>
		/// <param name="location"></param>
		/// <param name="currentDepth"></param>
		protected void BuildBlocks(ref Octree tree, Queue<uint> freeAddresses, uint address, Location location, byte currentDepth, byte maxDepth)
		{
			for (byte i = 0; i < 8; i++)
			{
				ulong edge = nearMax >> (currentDepth - 1);
				var newCoordinates = new Location(
					location[0] + ((i & 0b001) == 0b001 ? edge : 0),
					location[1] + ((i & 0b010) == 0b010 ? edge : 0),
					location[2] + ((i & 0b100) == 0b100 ? edge : 0));
				var block = MakeBlock(newCoordinates, currentDepth);
				if (currentDepth < maxDepth && CanHaveChildren(block.Chunk) && freeAddresses.Any())
				{
					var newAddress = freeAddresses.Dequeue() << 3;
					block.Child = newAddress;
					BuildBlocks(ref tree, freeAddresses, newAddress, newCoordinates, (byte)(currentDepth + 1), maxDepth);
				}
				tree.Blocks[address + i] = block;
				if (tree.BlockCount < address + i) tree.BlockCount = address + i + 1;
			}
		}

		/// <summary>
		/// Creates chunk data from coordinates
		/// </summary>
		/// <param name="coordinates"></param>
		/// <param name="depth"></param>
		/// <returns></returns>
		protected ushort MakeChunk(Location coordinates, byte depth)
		{
			(var x, var y, var z) = coordinates.CoordinateRanges(depth);
			return (ushort)(0
			+ (ContainsAir(new BoundingVolume(x.min, x.midpoint, y.min, y.midpoint, z.min, z.midpoint)) ? 1 << 0 : 0)
			+ (ContainsGeo(new BoundingVolume(x.min, x.midpoint, y.min, y.midpoint, z.min, z.midpoint)) ? 1 << 1 : 0)
			+ (ContainsAir(new BoundingVolume(x.midpoint, x.max, y.min, y.midpoint, z.min, z.midpoint)) ? 1 << 2 : 0)
			+ (ContainsGeo(new BoundingVolume(x.midpoint, x.max, y.min, y.midpoint, z.min, z.midpoint)) ? 1 << 3 : 0)
			+ (ContainsAir(new BoundingVolume(x.min, x.midpoint, y.midpoint, y.max, z.min, z.midpoint)) ? 1 << 4 : 0)
			+ (ContainsGeo(new BoundingVolume(x.min, x.midpoint, y.midpoint, y.max, z.min, z.midpoint)) ? 1 << 5 : 0)
			+ (ContainsAir(new BoundingVolume(x.midpoint, x.max, y.midpoint, y.max, z.min, z.midpoint)) ? 1 << 6 : 0)
			+ (ContainsGeo(new BoundingVolume(x.midpoint, x.max, y.midpoint, y.max, z.min, z.midpoint)) ? 1 << 7 : 0)
			+ (ContainsAir(new BoundingVolume(x.min, x.midpoint, y.min, y.midpoint, z.midpoint, z.max)) ? 1 << 8 : 0)
			+ (ContainsGeo(new BoundingVolume(x.min, x.midpoint, y.min, y.midpoint, z.midpoint, z.max)) ? 1 << 9 : 0)
			+ (ContainsAir(new BoundingVolume(x.midpoint, x.max, y.min, y.midpoint, z.midpoint, z.max)) ? 1 << 10 : 0)
			+ (ContainsGeo(new BoundingVolume(x.midpoint, x.max, y.min, y.midpoint, z.midpoint, z.max)) ? 1 << 11 : 0)
			+ (ContainsAir(new BoundingVolume(x.min, x.midpoint, y.midpoint, y.max, z.midpoint, z.max)) ? 1 << 12 : 0)
			+ (ContainsGeo(new BoundingVolume(x.min, x.midpoint, y.midpoint, y.max, z.midpoint, z.max)) ? 1 << 13 : 0)
			+ (ContainsAir(new BoundingVolume(x.midpoint, x.max, y.midpoint, y.max, z.midpoint, z.max)) ? 1 << 14 : 0)
			+ (ContainsGeo(new BoundingVolume(x.midpoint, x.max, y.midpoint, y.max, z.midpoint, z.max)) ? 1 << 15 : 0));
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

		protected static ulong IntToBinaryCoordinate(uint x, byte depth)
		{
			return (ulong)x << 64 - depth;
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

		public static uint PowSum(byte depth)
		{
			uint output = 0;
			for (int i = 1; i <= depth; i++)
				output += (uint)(1 << (3 * i));
			return output;
		}

		private bool checkTree(Octree tree, byte maxDepth)
		{
			uint baseStart = PowSum((byte)(tree.BaseDepth - 1));
			for (uint x = 0; x < (uint)(1 << tree.BaseDepth + 1); x++)
				for (uint y = 0; y < (uint)(1 << tree.BaseDepth + 1); y++)
					for (uint z = 0; z < (uint)(1 << tree.BaseDepth + 1); z++)
					{
						var basePosition = Interleave(x, y, z);
						int baseChunkLocation = (int)basePosition & 7;
						if (!CanHaveChildren(tree.BaseBlocks[baseStart + (basePosition >> 3)]))
							continue;

						if (((tree.BaseBlocks[baseStart + (basePosition >> 3)] >> (baseChunkLocation * 2) & 3) != 1)
							&& !anyChild((basePosition << 3), tree))
							return false;

						for (uint position = 0; position < 8; position++)
						{
							var address = (basePosition << 3) + position;

							if (!checkChildren(address, tree, (byte)(tree.BaseDepth + 2), maxDepth))
								return false;
						}
					}
			return true;
		}

		private bool anyChild(uint address, Octree tree)
		{
			for (int chunkLocation = 0; chunkLocation <= 7; chunkLocation++)
			{
				// If chunk has no gap but the child does
				if (tree.Blocks[address + chunkLocation].Chunk != 0b0101010101010101)
					return true;
			}
			return false;
		}

		private bool checkChildren(uint address, Octree tree, byte depth, byte maxDepth)
		{
			if (tree.Blocks[address].Child == uint.MaxValue || tree.Blocks[address].Child == 0 || depth > maxDepth)
				return true;

			for (int chunkLocation = 0; chunkLocation <= 7; chunkLocation++)
			{
				// If chunk has no gap but the child does
				if (((tree.Blocks[address].Chunk >> (chunkLocation * 2) & 3) != 1)
					&& tree.Blocks[tree.Blocks[address].Child + chunkLocation].Chunk == 0b0101010101010101)
					return false;
			}

			return checkChildren(tree.Blocks[address].Child, tree, (byte)(depth + 1), maxDepth);
		}
	}
}
