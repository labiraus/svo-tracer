using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Domain
{
	public interface ITreeBuilder
	{
		Octree BuildTree(byte N, ushort maxDepth, uint maxSize = 0);
	}
}
