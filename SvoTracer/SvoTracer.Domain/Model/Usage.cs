using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Usage
    {
        public ushort Count { get; set; }
        public uint Parent { get; set; }
    }
}
