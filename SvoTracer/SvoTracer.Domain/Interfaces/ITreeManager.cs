using SvoTracer.Domain.Models;

namespace SvoTracer.Domain.Interfaces
{
	public interface ITreeManager
	{
		bool TreeExists(string fileName);
		Octree LoadTree(string fileName);
		void SaveTree(string fileName, Octree tree);
		void DeleteTree(string fileName);
	}
}
