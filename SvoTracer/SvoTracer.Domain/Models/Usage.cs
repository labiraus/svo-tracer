using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
    public struct Usage
    {
        public ushort Count { get; set; }
        public uint Parent { get; set; }
    }
}
