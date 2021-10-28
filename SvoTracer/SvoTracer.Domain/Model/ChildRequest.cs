using System;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChildRequest
    {
        public uint Address { get; set; }
        public ushort Tick { get; set; }
        public byte Depth { get; set; }
        public Location Location { get; set; }
    }
}
