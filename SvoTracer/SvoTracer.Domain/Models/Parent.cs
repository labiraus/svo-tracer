using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
    public struct Parent
    {
        public uint ParentAddress { get; set; }
        public uint NextElement { get; set; }
        public const int Size = 8;
    }
}
