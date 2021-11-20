using SvoTracer.Domain.Models;

namespace SvoTracer.Domain.Interfaces
{
	public interface ITreeBuilder
	{
		void AddGeometry(IGeometryDefinition geometryDefinition);
		Octree BuildTree(byte baseDepth, byte maxDepth, uint maxSize = 0);
	}
}
