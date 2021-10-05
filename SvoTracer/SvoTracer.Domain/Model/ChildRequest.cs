using System;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ChildRequest
    {
        public uint Address;
        public ushort Tick;
        public byte Depth;
        public Location Location;
    }
}
