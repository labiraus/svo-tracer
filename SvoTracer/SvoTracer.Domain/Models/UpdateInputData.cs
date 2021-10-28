using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UpdateInputData
    {
        //Depth of inviolate memory(Specific to voxels)
        public byte N { get; set; }
        public ushort Tick { get; set; }
        public uint MaxChildRequestId { get; set; }
        public uint MemorySize { get; set; }
        public uint Offset { get; set; }
        public uint GraftSize { get; set; }
    }
}
