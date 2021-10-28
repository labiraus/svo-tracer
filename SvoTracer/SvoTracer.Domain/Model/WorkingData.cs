using System;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace SvoTracer.Domain.Model
{
	[Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct WorkingData
	{
		//Current location
		public Location Location { get; set; }
		public Vector3 Incidence { get; set; }
		//Origin Position
		public Vector3 Origin { get; set; }
		//Vector direction
		public Vector3 Direction { get; set; }
		//Inverse vector direction
		public Vector3 InvDirection { get; set; }
		//Depth of field made up of focal depth(the angle of the forced depth) and focal point(how deep the minimum is)
		public Vector2 DoF { get; set; }
		public Vector2i Coord { get; set; }
		public float PixelFoV { get; set; }
		//Maximum Opacity
		public byte MaxOpacity { get; set; }
		public byte[] Colour { get; set; }
		public byte Opacity { get; set; }
		//Depth of inviolate memory(Specific to voxels)
		public byte N { get; set; }
		//Signs of the vector direction
		public bool DirectionSignX { get; set; }
		public bool DirectionSignY { get; set; }
		public bool DirectionSignZ { get; set; }
		public ushort Tick { get; set; }
		public uint MaxChildRequestId { get; set; }
	}
}
