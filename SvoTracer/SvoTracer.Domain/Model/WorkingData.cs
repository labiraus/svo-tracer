using System;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WorkingData
    {
        //Current location
        public Location Location;
        public Vector3 Incidence;
        //Origin Position
        public Vector3 Origin;
        //Vector direction
        public Vector3 Direction;
        //Inverse vector direction
        public Vector3 InvDirection;
        //Depth of field made up of focal depth(the angle of the forced depth) and focal point(how deep the minimum is)
        public Vector2 DoF;
        public Vector2i Coord;
        public float PixelFoV;
        //Maximum Opacity
        public byte MaxOpacity;
        public byte[] Colour;
        public byte Opacity;
        //Depth of inviolate memory(Specific to voxels)
        public byte N;
        //Signs of the vector direction
        public bool DirectionSignX;
        public bool DirectionSignY;
        public bool DirectionSignZ;
        public ushort Tick;
        public uint MaxChildRequestId;
    }
}
