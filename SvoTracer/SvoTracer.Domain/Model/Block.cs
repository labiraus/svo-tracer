using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Block
    {
        public uint Child;
        public ushort Chunk;
        public BlockData Data;
    }
}
