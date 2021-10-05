using System;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace SvoTracer.Domain.Model
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct TraceInputData
    {
		public TraceInputData(
            System.Numerics.Vector3 origin, 
            System.Numerics.Vector3 facing,
            System.Numerics.Vector2 foV,
            System.Numerics.Vector2 doF,
            int screenSizeX,
            int screenSizeY,
            byte maxOpacity,
            byte n,
            ushort tick,
            uint maxChildRequestId)
        {
            Origin = new Vector3(origin.X, origin.Y, origin.Z);
            Facing = new Vector3(facing.X, facing.Y, facing.Z);
            FoV = new Vector2(foV.X, foV.Y);
            DoF = new Vector2(doF.X, doF.Y);
            ScreenSize = new Vector2i(screenSizeX, screenSizeY);
            MaxOpacity = maxOpacity;
            N = n;
            Tick = tick;
            MaxChildRequestId = maxChildRequestId;
        }

        //Position
        public Vector3 Origin;
        //Direction faced
        public Vector3 Facing;
        //Horizonal/vertical FoV angle of the screen
        public Vector2 FoV;
        //Depth of field made up of focal depth(the angle of the forced depth) and focal point(how deep the minimum is)
        public Vector2 DoF;
        //Screen size
        public Vector2i ScreenSize;
        public byte MaxOpacity;
        //Depth of inviolate memory(Specific to voxels)
        public byte N;
        public ushort Tick;
        public uint MaxChildRequestId;
    }
}
