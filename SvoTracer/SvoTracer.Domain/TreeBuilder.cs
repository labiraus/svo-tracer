using SvoTracer.Domain.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SvoTracer.Domain
{
    public abstract class TreeBuilder
    {
        protected const ulong nearMax = ulong.MaxValue - (ulong.MaxValue >> 1);
        uint maxDepth;
        Queue<uint> stack;
        Octree tree;

        public abstract bool Full((float, float) a, (float, float) b, (float, float) c);
        public abstract bool Empty((float, float) a, (float, float) b, (float, float) c);
        protected abstract Block MakeBlock(ulong[] coordinates, int depth);

        public bool TreeExists(string fileName) {

            return File.Exists($"{Environment.CurrentDirectory}\\trees\\{fileName}.oct");
        }

        public Octree LoadTree(string fileName)
        {
            Octree tree = null;
            try
            {
                FileStream fs = new FileStream($"{Environment.CurrentDirectory}\\trees\\{fileName}.oct", FileMode.Open);
                BinaryReader br = new BinaryReader(fs);

                byte N = br.ReadByte();
                uint maxSize = br.ReadUInt32();
                uint size = maxSize > (uint)(1 << (3 * (int)N + 6)) ? maxSize : (uint)(1 << (3 * (int)N + 6));

                tree = new Octree()
                {
                    BlockCount = size,
                    N = N,
                    BaseBlocks = new ushort[PowSum(N)],
                    Blocks = new Block[size]
                };

                var bases = br.ReadBytes(tree.BaseBlocks.Count() * 2);
                for (int i = 0; i < tree.BaseBlocks.Count(); i++)
                {
                    tree.BaseBlocks[i] = BitConverter.ToUInt16(bases, i * 2);
                }
                var blocks = br.ReadBytes(16 * tree.Blocks.Count());
                for (int i = 0; i < size; i++)
                {
                    tree.Blocks[i] = new Block()
                    {
                        Child = BitConverter.ToUInt32(blocks, 0 + (i * 16)),
                        Chunk = BitConverter.ToUInt16(blocks, 4 + (i * 16)),
                        Data = new BlockData()
                        {
                            NormalPitch = BitConverter.ToInt16(blocks, 6 + (i * 16)),
                            NormalYaw = BitConverter.ToInt16(blocks, 8 + (i * 16)),
                            ColourR = (byte)BitConverter.ToChar(blocks, 10 + (i * 16)),
                            ColourB = (byte)BitConverter.ToChar(blocks, 11 + (i * 16)),
                            ColourG = (byte)BitConverter.ToChar(blocks, 12 + (i * 16)),
                            Opacity = (byte)BitConverter.ToChar(blocks, 13 + (i * 16)),
                            Properties = BitConverter.ToUInt16(blocks, 14 + (i * 16))
                        }
                    };
                }

                br.Close();
                fs.Close();
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
                Console.ReadKey(true);
            }
            return tree;
        }

        public void SaveTree(string fileName, byte N, Octree tree)
        {
            try
            {
                if (!Directory.Exists($"{Environment.CurrentDirectory}\\trees"))
                    Directory.CreateDirectory($"{Environment.CurrentDirectory}\\trees");
                FileStream fs = File.Create($"{Environment.CurrentDirectory}\\trees\\{fileName}.oct", 2048, FileOptions.None);
                BinaryWriter bw = new BinaryWriter(fs);

                bw.Write(N);
                bw.Write(tree.BlockCount);
                foreach (var baseBlock in tree.BaseBlocks)
                    bw.Write(baseBlock);

                foreach (var block in tree.Blocks)
                {
                    bw.Write(block.Child);
                    bw.Write(block.Chunk);
                    bw.Write(block.Data.NormalPitch);
                    bw.Write(block.Data.NormalYaw);
                    bw.Write(block.Data.ColourR);
                    bw.Write(block.Data.ColourB);
                    bw.Write(block.Data.ColourG);
                    bw.Write(block.Data.Opacity);
                    bw.Write(block.Data.Properties);
                }

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
            if (tree == null){
                BuildTree(N, depth, maxSize);
			}
            SaveTree(fileName, tree.N, tree);
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
                BlockCount = size,
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
