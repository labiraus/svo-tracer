using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
    public struct Usage
    {
        public ushort Tick;
        public uint Parent;
    }
}
