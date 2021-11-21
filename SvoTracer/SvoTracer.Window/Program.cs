using OpenTK.Mathematics;
using SvoTracer.Domain;
using SvoTracer.Domain.Geometry;
using SvoTracer.Domain.Interfaces;
using SvoTracer.Domain.Models;
using System;

namespace SvoTracer.Window
{
	class Program
	{
		[STAThread]
		private static void Main(string[] args)
		{
			byte BaseDepth = 4;
			byte maxDepth = 9;
			//var treeName = "test" + maxDepth;
			var treeName = "sphere" + maxDepth;
			//ITreeBuilder treeBuilder = new TreeBuilder(new[] {
			//	new SphereDefinition(new Vector3(0.3f, 0.3f, 0.3f), 0.1f),
			//	new SphereDefinition(new Vector3(0.7f, 0.7f, 0.7f), 0.1f),
			//});
			ITreeBuilder treeBuilder = new TreeBuilder(new[] {
				new CubeDefinition(new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0.6f, 0.6f, 0.6f)),
			});
			ITreeManager treeManager = new TreeManager($"{Environment.CurrentDirectory}\\trees");

			treeManager.DeleteTree(treeName);

			if (!treeManager.TreeExists(treeName))
				treeManager.SaveTree(treeName, treeBuilder.BuildTree(BaseDepth, maxDepth, uint.MaxValue / 64));
			var tree = treeManager.LoadTree(treeName);

			var input = new TraceInputData()
			{
				Origin = new(-2f, 0.5f, 0.5f),
				Facing = Matrix3.Identity,
				FoV = new((float)Math.PI / 4f, (float)Math.PI / 4f),
				DoF = new(0, 0.169f),
				MaxOpacity = 200,
				MaxChildRequestId = 6000,
				ScreenSize = new(100, 100),
				BaseDepth = tree.BaseDepth,
			};

			//new TestRun(tree).Run(input);
			using MainWindow win = new(1000, 1000, "SVO Tracer", tree, input);
			win.Run();
		}
	}
}
