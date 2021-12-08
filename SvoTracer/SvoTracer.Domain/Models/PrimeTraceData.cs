using System;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace SvoTracer.Domain.Models
{
	public struct PrimeTraceData
	{
		public PrimeTraceData(
			Vector3 origin,
			Matrix3 facing,
			Vector2 foV,
			Vector2 doF,
			int screenSizeX,
			int screenSizeY,
			byte maxOpacity,
			byte baseDepth,
			ushort tick,
			uint maxChildRequestId)
		{
			Origin = origin;
			Facing = facing;
			FoV = new Vector2(foV.X, foV.Y);
			DoF = new Vector2(doF.X, doF.Y);
			ScreenSize = new Vector2i(screenSizeX, screenSizeY);
			MaxOpacity = maxOpacity;
			BaseDepth = baseDepth;
			Tick = tick;
			MaxChildRequestId = maxChildRequestId;
		}

		//Position
		public Vector3 Origin;
		//Tait-Bryan angles of direction faced
		public Matrix3 Facing;
		//Horizonal/vertical FoV angle of the screen
		public Vector2 FoV;
		//Depth of field made up of focal depth(the angle of the forced depth) and focal point(how deep the minimum is)
		public Vector2 DoF;
		//Screen size
		public Vector2i ScreenSize;
		public byte MaxOpacity;
		//Depth of inviolate memory(Specific to voxels)
		public byte BaseDepth;
		public ushort Tick;
		public uint MaxChildRequestId;
	}
}
