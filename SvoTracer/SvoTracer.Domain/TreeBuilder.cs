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
	public abstract class TreeBuilder
	{
		protected const ulong nearMax = ulong.MaxValue - (ulong.MaxValue >> 1);
		uint maxDepth;
		Queue<uint> stack;
		Octree tree;
		uint maxBlock = 0;

		public abstract bool Full((float, float) a, (float, float) b, (float, float) c);
		public abstract bool Empty((float, float) a, (float, float) b, (float, float) c);
		protected abstract Block MakeBlock(ulong[] coordinates, int depth);

		public bool TreeExists(string fileName)
		{
			return File.Exists($"{Environment.CurrentDirectory}\\trees\\{fileName}.oct");
		}

		public static Octree LoadTree(string fileName)
		{
			using FileStream fs = new FileStream($"{Environment.CurrentDirectory}\\trees\\{fileName}.oct", FileMode.Open);
			using BinaryReader br = new BinaryReader(fs);
			var tree = OctreeSerializer.Deserialize(br);

			br.Close();
			fs.Close();
			return tree;
		}

		public static void SaveTree(string fileName, Octree tree)
		{
			try
			{
				if (!Directory.Exists($"{Environment.CurrentDirectory}\\trees"))
					Directory.CreateDirectory($"{Environment.CurrentDirectory}\\trees");
				using FileStream fs = File.Create($"{Environment.CurrentDirectory}\\trees\\{fileName}.oct", 2048, FileOptions.None);
				using BinaryWriter bw = new BinaryWriter(fs);
				tree.Serialize(bw);

				bw.Close();
				fs.Close();
			}
			catch (Exception e)
			{
				Console.Write(e.Message);
				Console.ReadKey(true);
			}
		}


		public void SaveTree(string fileName, byte N, byte depth, uint maxSize = 0)
		{
			if (tree.BlockCount == 0)
			{
				BuildTree(N, depth, maxSize);
			}
			SaveTree(fileName, tree);
		}

		/// <summary>
		/// Builds an octree using inherited class's MakeBlock method
		/// </summary>
		/// <param name="N">Depth of inviolate tree</param>
		/// <param name="depth">Initial tree depth</param>
		/// <param name="maxSize">Maximum number of blocks available</param>
		/// <returns></returns>
		public Octree BuildTree(byte N, byte depth, uint maxSize = 0)
		{
			bool[] childrenNeeded = new bool[(uint)(1 << (3 * (int)N + 6))];

			uint size = maxSize > (uint)(1 << (3 * (int)N + 6)) ? maxSize : (uint)(1 << (3 * (int)N + 6));
			tree = new Octree()
			{
				N = N,
				BaseBlocks = new ushort[PowSum(N)],
				Blocks = new Block[size]
			};
			maxDepth = depth;
			stack = new Queue<uint>();

			//Creates base levels 1 to N
			for (ushort i = 1; i <= N; i++)
			{
				for (uint z = 0; z < (uint)(1 << i); z++)
				{
					ulong zcoord = IntToUlong(z, i);
					for (uint y = 0; y < (uint)(1 << i); y++)
					{
						ulong ycoord = IntToUlong(y, i);
						for (uint x = 0; x < (uint)(1 << i); x++)
						{
							ulong xcoord = IntToUlong(x, i);
							var address = PowSum((byte)(i - 1)) + Interleave(x, y, z);
							tree.BaseBlocks[address] = BuildChunk(CoordinateRange(xcoord, i), CoordinateRange(ycoord, i), CoordinateRange(zcoord, i));
						}
					}
				}
			}


			uint baseStart = PowSum((byte)(N - 1));
			//Iterates over level N+1 base chunks
			for (uint z = 0; z < (uint)(1 << (int)(N + 1)); z++)
			{
				var zcoord = IntToUlong(z, (ushort)(N + 1));
				for (uint y = 0; y < (uint)(1 << (int)(N + 1)); y++)
				{
					var ycoord = IntToUlong(y, (ushort)(N + 1));
					for (uint x = 0; x < (uint)(1 << (int)(N + 1)); x++)
					{
						var xcoord = IntToUlong(x, (ushort)(N + 1));
						var i = Interleave(x, y, z);
						if ((tree.BaseBlocks[baseStart + (i >> 3)] >> (int)((i & 7) * 2) & 3) == 3)
							//If there's children to be created, create 8 N+2 blocks
							for (uint z2 = 0; z2 < 2; z2++)
								for (uint y2 = 0; y2 < 2; y2++)
									for (uint x2 = 0; x2 < 2; x2++)
									{
										var j = Interleave(x2, y2, z2);
										var block = MakeBlock(new ulong[]
										{
											xcoord + IntToUlong(x2, (ushort)(N + 2)),
											ycoord + IntToUlong(y2, (ushort)(N + 2)),
											zcoord + IntToUlong(z2, (ushort)(N + 2))
										}, (int)N + 2);
										tree.Blocks[(i << 3) + j] = block;
										if (maxBlock < (i << 3) + j) maxBlock = (i << 3) + j + 1;
										childrenNeeded[(i << 3) + j] = CanHaveChildren(block.Chunk);
									}
						else
							//Push unused address into stack
							stack.Enqueue(i);

					}
				}
			}

			//Push left over addresses into stack
			if ((maxSize >> 3) > (uint)(1 << (3 * (int)(N + 1))))
				for (uint i = (uint)(1 << (3 * (int)(N + 1))); i < (maxSize >> 3); i++)
					stack.Enqueue(i);

			//Iterates over level N+2
			for (uint x = 0; x < (uint)(1 << (int)(N + 2)); x++)
			{
				var xcoord = IntToUlong(x, (ushort)(N + 2));
				for (uint y = 0; y < (uint)(1 << (int)(N + 2)); y++)
				{
					var ycoord = IntToUlong(y, (ushort)(N + 2));
					for (uint z = 0; z < (uint)(1 << (int)(N + 2)); z++)
					{
						var i = Interleave(x, y, z);
						if (childrenNeeded[i])
						{
							uint address = stack.Dequeue() << 3;
							tree.Blocks[i].Child = address;
							BuildBlocks(address, new ulong[] { xcoord, ycoord, IntToUlong(z, (ushort)(N + 2)) }, N + 2);
						}
					}
				}
			}
			tree.Blocks = tree.Blocks.Take((int)maxBlock).ToArray();
			tree.BlockCount = maxBlock;
			return tree;
		}

		protected void BuildBlocks(uint address, ulong[] coordinates, int currentDepth)
		{
			for (int i = 0; i < 8; i++)
			{
				var newCoordinates = new ulong[] {
					coordinates[0] + ((i & 1) == 1 ? nearMax >> (currentDepth - 1): 0),
					coordinates[1] + ((i >> 1 & 1) == 1 ? nearMax >> (currentDepth - 1) : 0),
					coordinates[2] + ((i >> 2 & 1) == 1 ? nearMax >> (currentDepth - 1) : 0) };
				var block = MakeBlock(newCoordinates, currentDepth);
				tree.Blocks[address + i] = block;
				if (maxBlock < address + i) maxBlock = address + (uint)i + 1;
				if (CanHaveChildren(block.Chunk) && currentDepth < maxDepth && stack.Any())
				{
					var newAddress = stack.Dequeue() << 3;
					block.Child = newAddress;
					BuildBlocks(newAddress, newCoordinates, currentDepth + 1);
				}
			}
		}

		protected ushort MakeBase(ulong[] coordinates, int depth)
		{
			return BuildChunk(CoordinateRange(coordinates[0], depth), CoordinateRange(coordinates[1], depth), CoordinateRange(coordinates[2], depth));
		}

		protected ushort BuildChunk((float, float, float) x, (float, float, float) y, (float, float, float) z)
		{
			var chunk = (ushort)((Empty((x.Item1, x.Item2), (y.Item1, y.Item2), (z.Item1, z.Item2)) ? 1 : 0)
				+ (Full((x.Item1, x.Item2), (y.Item1, y.Item2), (z.Item1, z.Item2)) ? 1 << 1 : 0)
				+ (Empty((x.Item2, x.Item3), (y.Item1, y.Item2), (z.Item1, z.Item2)) ? 1 << 2 : 0)
				+ (Full((x.Item2, x.Item3), (y.Item1, y.Item2), (z.Item1, z.Item2)) ? 1 << 3 : 0)
				+ (Empty((x.Item1, x.Item2), (y.Item2, y.Item3), (z.Item1, z.Item2)) ? 1 << 4 : 0)
				+ (Full((x.Item1, x.Item2), (y.Item2, y.Item3), (z.Item1, z.Item2)) ? 1 << 5 : 0)
				+ (Empty((x.Item2, x.Item3), (y.Item2, y.Item3), (z.Item1, z.Item2)) ? 1 << 6 : 0)
				+ (Full((x.Item2, x.Item3), (y.Item2, y.Item3), (z.Item1, z.Item2)) ? 1 << 7 : 0)
				+ (Empty((x.Item1, x.Item2), (y.Item1, y.Item2), (z.Item2, z.Item3)) ? 1 << 8 : 0)
				+ (Full((x.Item1, x.Item2), (y.Item1, y.Item2), (z.Item2, z.Item3)) ? 1 << 9 : 0)
				+ (Empty((x.Item2, x.Item3), (y.Item1, y.Item2), (z.Item2, z.Item3)) ? 1 << 10 : 0)
				+ (Full((x.Item2, x.Item3), (y.Item1, y.Item2), (z.Item2, z.Item3)) ? 1 << 11 : 0)
				+ (Empty((x.Item1, x.Item2), (y.Item2, y.Item3), (z.Item2, z.Item3)) ? 1 << 12 : 0)
				+ (Full((x.Item1, x.Item2), (y.Item2, y.Item3), (z.Item2, z.Item3)) ? 1 << 13 : 0)
				+ (Empty((x.Item2, x.Item3), (y.Item2, y.Item3), (z.Item2, z.Item3)) ? 1 << 14 : 0)
				+ (Full((x.Item2, x.Item3), (y.Item2, y.Item3), (z.Item2, z.Item3)) ? 1 << 15 : 0));
			return chunk;
		}


		public static bool CanHaveChildren(ushort chunk)
		{
			return (((chunk & 3) == 3) ||
				((chunk & 12) == 12) ||
				((chunk & 48) == 48) ||
				((chunk & 192) == 192) ||
				((chunk & 768) == 768) ||
				((chunk & 3072) == 3072) ||
				((chunk & 12288) == 12288) ||
				((chunk & 49152) == 49152));
		}

		protected static ulong IntToUlong(uint x, ushort depth)
		{
			ulong output = 0;
			for (int i = 0; i <= depth; i++)
			{
				if ((x >> i & 1) == 1)
					output += (nearMax >> (depth - i - 1));
			}
			return output;
		}

		public static (float, float, float) CoordinateRange(ulong coordinate, int depth)
		{
			float start = 0, end = 1, division = 1;
			for (int i = 0; i < depth; i++)
			{
				division /= 2;
				if ((coordinate & (nearMax >> i)) > 0)
					start += division;
				else
					end -= division;
			}
			return (start, (start + end) / 2, end);
		}

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
	}
}
