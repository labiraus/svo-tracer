using OpenTK.Mathematics;
using SvoTracer.Domain;
using SvoTracer.Domain.Geometry;
using SvoTracer.Domain.Interfaces;
using SvoTracer.Domain.Models;
using SvoTracer.Kernel;
using System;

namespace SvoTracer.Window
{
	class Program
	{
		[STAThread]
		private static void Main(string[] args)
		{
			//new EnqueueTest();
			byte BaseDepth = 4;
			byte maxDepth = 11;
			var shape = Shapes.Spheres;

			ITreeBuilder treeBuilder;
			string treeName;
			switch (shape)
			{
				case Shapes.Cube:
					treeName = "cube" + maxDepth;
					treeBuilder = new TreeBuilder(new[] {
						new CubeDefinition(new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0.6f, 0.6f, 0.6f)),
					});
					break;
				case Shapes.Spheres:
					treeName = "sphere" + maxDepth;
					treeBuilder = new TreeBuilder(new[] {
						new SphereDefinition(new Vector3(0.3f, 0.3f, 0.3f), 0.1f),
						new SphereDefinition(new Vector3(0.7f, 0.7f, 0.7f), 0.1f),
					});
					break;
				default: return;
			}

			ITreeManager treeManager = new TreeManager($"{Environment.CurrentDirectory}\\trees");
			//treeManager.DeleteTree(treeName);

			if (!treeManager.TreeExists(treeName))
				treeManager.SaveTree(treeName, treeBuilder.BuildTree(BaseDepth, maxDepth, uint.MaxValue / 64));
			var tree = treeManager.LoadTree(treeName);

			var input = new TraceInput()
			{
				Origin = new(-2f, 0.5f, 0.5f),
				Facing = Matrix3.Identity,
				//Origin = new(0.6223052f, 0.67926204f, 0.6145233f),
				//Facing = new(0.96893793f, 0.24715249f, 0.008649882f, -0.24716175f, 0.96897423f, 0.0f, -0.008381513f, -0.00213792f, 0.99996257f),
				FoV = new((float)Math.PI / 4f, (float)Math.PI / 4f),
				DoF = new(0, 0),
				MaxOpacity = 200,
				MaxChildRequestId = 6000,
				BaseDepth = tree.BaseDepth,
				FovMultiplier = 0.2f,
				FovConstant = 0.2f,
				WeightingMultiplier = -0.05f,
				WeightingConstant = 0.5f,
				AmbientLightLevel = 100,
			};

			//new TestRun(tree).Run(input);
			using MainWindow win = new(1000, 1000, "SVO Tracer", tree, input);
			win.Run();
		}
		enum Shapes
		{
			Spheres,
			Cube
		}
	}
}
