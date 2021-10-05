using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Usage
    {
        public ushort Count;
        public uint Parent;
    }
}
