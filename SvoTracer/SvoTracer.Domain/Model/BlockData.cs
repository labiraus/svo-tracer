using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct BlockData
    {
        public short NormalPitch;
        public short NormalYaw;
        public byte ColourR;
        public byte ColourB;
        public byte ColourG;
        public byte Opacity;
        public ushort Properties;
    }
}
