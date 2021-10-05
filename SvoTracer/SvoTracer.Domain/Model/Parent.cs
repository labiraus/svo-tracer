using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Parent
    {
        public uint ParentAddress;
        public uint NextElement;
    }
}
