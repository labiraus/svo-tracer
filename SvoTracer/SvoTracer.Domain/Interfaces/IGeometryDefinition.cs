using OpenTK.Mathematics;
using SvoTracer.Domain.Models;

namespace SvoTracer.Domain.Interfaces
{
	public interface IGeometryDefinition
	{
		bool WithinBounds(BoundingVolume volume);
		bool ContainsGeo(BoundingVolume volume);
		bool ContainsAir(BoundingVolume volume);
		Vector3 Normal(BoundingVolume volume);
		byte[] Colour(BoundingVolume volume);
	}
}
