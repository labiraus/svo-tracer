using OpenTK.Mathematics;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SvoTracer.Domain.Models
{
	public struct DepthMask
	{
		public float RayLength;
		public Vector3 Incidence;
		public Vector3 Position;
		public float ConeDepth;
		public SurfaceData Data;

		public const int Size = 42;
	}
}
