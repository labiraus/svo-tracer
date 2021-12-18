using System;
using OpenTK.Mathematics;
using SvoTracer.Domain;
using SvoTracer.Domain.Models;
using SvoTracer.Kernel;

namespace SvoTracer
{
	public class TestRun
	{
		private readonly Octree _tree;

		public TestRun(Octree tree)
		{
			_tree = tree;
		}
		public void Run(TraceInput input)
		{
			try
			{
				var usage = new Usage[_tree.BlockCount >> 3];
				var baseStart = TreeBuilder.PowSum((byte)(_tree.BaseDepth - 1));
				var range = TreeBuilder.PowSum(_tree.BaseDepth) << 3;
				//This iterates over the BaseDepth+1 level
				for (int i = 0; i < range; i++)
				{
					if ((_tree.BaseBlocks[baseStart + (i >> 3)] >> ((i & 7) * 2) & 3) != 3)
						break;
					usage[i].Tick = ushort.MaxValue;
					usage[i].Parent = uint.MaxValue;
				}
				uint childRequestId = 0;
				var childRequests = new ChildRequest[input.MaxChildRequestId];
				for (uint i = 0; i < input.ScreenSize.Y; i++)
				{
					for (uint j = 0; j < input.ScreenSize.X; j++)
						try
						{
							KernelMirror.y = i;
							KernelMirror.x = j;
							KernelMirror.VoxelTrace(_tree.BaseBlocks, _tree.Blocks, usage, ref childRequestId, childRequests, $"Success for {j} {i}: ", input);
						}
						catch (Exception e)
						{
							Console.WriteLine($"Error for {j} {i}: {e}");
						}
					Console.WriteLine(i);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
		}
	}
}
