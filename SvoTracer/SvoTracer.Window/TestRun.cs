using System;
using System.Numerics;
using SvoTracer.Domain;
using SvoTracer.Domain.Models;
using SvoTracer.Kernel;

namespace SvoTracer
{
	public class TestRun
	{
		public void Run(ITreeBuilder treeBuilder, ITreeManager treeManager)
		{
			var input = new TraceInputData(
				new OpenTK.Mathematics.Vector3(0.5f, 0.5f, -2f),
				new OpenTK.Mathematics.Vector3(0, (float)Math.PI / 2f, 0),
				new OpenTK.Mathematics.Vector2((float)Math.PI / 4f, (float)Math.PI / 4f),
				new OpenTK.Mathematics.Vector2(0, 0.169f),
				80,
				80,
				200,
				5,
				0,
				6000);

			try
			{
				if (!treeManager.TreeExists("test"))
					treeManager.SaveTree("test", treeBuilder.BuildTree(5, 7, uint.MaxValue / 64));
				var tree = treeManager.LoadTree("test");
				var usage = new Usage[tree.BlockCount >> 3];
				var baseStart = TreeBuilder.PowSum((byte)(tree.N - 1));
				var range = TreeBuilder.PowSum(tree.N) << 3;
				//This iterates over the N+1 level
				for (int i = 0; i < range; i++)
				{
					if ((tree.BaseBlocks[baseStart + (i >> 3)] >> ((i & 7) * 2) & 3) != 3)
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
							KernelMirror.x = i;
							KernelMirror.y = j;
							KernelMirror.VoxelTrace(tree.BaseBlocks, tree.Blocks, usage, ref childRequestId, childRequests, $"Success for {j} {i}: ", input);
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
