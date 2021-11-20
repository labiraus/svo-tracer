using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
    public struct UpdateInputData
    {
        //Depth of inviolate memory(Specific to voxels)
        public byte BaseDepth;
        public ushort Tick;
        public uint MaxChildRequestId;
        public uint MemorySize;
        public uint Offset;
        public uint GraftSize;
    }
}
