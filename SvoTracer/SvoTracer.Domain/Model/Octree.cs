using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public class Octree
    {
        public uint BlockCount;
        public byte N;
        public ushort[] BaseBlocks;
        public Block[] Blocks;
    }
}
