using System;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace SvoTracer.Domain.Models
{
	/// <summary>
	/// In Kernel object only
	/// </summary>
	public struct WorkingData
	{
		//Current location
		public Location Location;
		//Origin Position
		public Vector3 Origin;
		//Vector direction
		public Vector3 Direction;
		//Inverse vector direction
		public Vector3 InvDirection;
		//Depth of field made up of focal depth(the angle of the forced depth) and focal point(how deep the minimum is)
		public Vector2 DoF;
		public Vector2i Coord;
		public Vector2i ScreenSize;
		public float PixelFoV;
		//Maximum Opacity
		public byte MaxOpacity;
		public float ColourR;
		public float ColourB;
		public float ColourG;
		public float Opacity;
		//Depth of inviolate memory(Specific to voxels)
		public byte BaseDepth;
		//Signs of the vector direction
		public bool DirectionSignX;
		public bool DirectionSignY;
		public bool DirectionSignZ;
		public ushort Tick;
		public uint MaxChildRequestId;
		public float ConeDepth;
	}
}
