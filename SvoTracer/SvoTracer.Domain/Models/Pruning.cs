using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Pruning
    {
        public byte Properties;
        //0 CullChild
        //1 AlterViolability
        //2 MakeInviolate
        //3 UpdateChunk
        //4 BaseBlock
        //5 ??
        //6 ??
        //7 ??
        public byte Depth;
        public uint Address;
        public ushort Chunk;
        public uint ColourAddress;
        public uint ChildAddress;
    }
}
