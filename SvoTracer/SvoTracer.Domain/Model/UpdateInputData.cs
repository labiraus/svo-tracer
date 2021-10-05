using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct UpdateInputData
    {
        //Depth of inviolate memory(Specific to voxels)
        public byte N;
        public ushort Tick;
        public uint MaxChildRequestId;
        public uint MemorySize;
        public uint Offset;
        public uint GraftSize;
    }
}
