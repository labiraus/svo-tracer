using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Grafting
    {
        public uint GraftDataAddress { get; set; }
        public uint GraftTotalSize { get; set; }
        public byte Depth { get; set; }
        public uint GraftAddress { get; set; }
    }
}
