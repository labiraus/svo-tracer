﻿using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Colour
    {
        public byte ColourR;
        public byte ColourB;
        public byte ColourG;
        public byte Opacity;
        public byte Specularity;
        public byte Gloss;
        public byte Dielectricity;
        public byte Refractivity;
    }   
}
