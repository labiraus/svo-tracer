using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain
{
	public interface ITreeManager
	{
		bool TreeExists(string fileName);
		Octree LoadTree(string fileName);
		void SaveTree(string fileName, Octree tree);
	}
}
