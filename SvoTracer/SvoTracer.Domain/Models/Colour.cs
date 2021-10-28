using System;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
    public struct Colour
    {
        public byte ColourR { get; set; }
        public byte ColourB { get; set; }
        public byte ColourG { get; set; }
        public byte Opacity { get; set; }
        public byte Specularity { get; set; }
        public byte Gloss { get; set; }
        public byte Dielectricity { get; set; }
        public byte Refractivity { get; set; }
    }   
}
