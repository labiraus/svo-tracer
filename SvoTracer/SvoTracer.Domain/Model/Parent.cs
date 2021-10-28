using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Parent
    {
        public uint ParentAddress { get; set; }
        public uint NextElement { get; set; }
    }
}
