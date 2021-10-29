using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
    public struct Parent
    {
        public uint ParentAddress;
        public uint NextElement;
        public const int Size = 8;
    }
}
