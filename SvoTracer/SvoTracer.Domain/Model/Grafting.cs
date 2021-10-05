using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Grafting
    {
        public uint GraftDataAddress;
        public uint GraftTotalSize;
        public byte Depth;
        public uint GraftAddress;
    }
}
