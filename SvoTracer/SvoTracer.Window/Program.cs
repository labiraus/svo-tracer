using SvoTracer.Domain;
using SvoTracer.Domain.Models;
using System;
using System.Numerics;

namespace SvoTracer.Window
{
	class Program
	{
		[STAThread]
		private static void Main(string[] args)
		{
			byte N = 4;
			byte maxDepth = 9;
			var treeName = "test" + maxDepth;
			var builder = new CubeBuilder(
				new Vector3(0.3f, 0.3f, 0.3f),
				new Vector3(0.3f, 0.3f, 0.6f),
				new Vector3(0.3f, 0.6f, 0.6f),
				new Vector3(0.3f, 0.6f, 0.3f),
				new Vector3(0.6f, 0.3f, 0.3f),
				new Vector3(0.6f, 0.3f, 0.6f),
				new Vector3(0.6f, 0.6f, 0.6f),
				new Vector3(0.6f, 0.6f, 0.3f));
			var treeManager = new TreeManager($"{Environment.CurrentDirectory}\\trees");

			//treeManager.DeleteTree(treeName);

			if (!treeManager.TreeExists(treeName))
				treeManager.SaveTree(treeName, builder.BuildTree(N, maxDepth, uint.MaxValue / 64));
			var tree = treeManager.LoadTree(treeName);


			var input = new TraceInputData()
			{
				Origin = new(-2f, 0.5f, 0.5f),
				//Origin = new(0.2f, 0.59f, 0.59f),
				Facing = OpenTK.Mathematics.Matrix3.Identity,
				FoV = new((float)Math.PI / 4f, (float)Math.PI / 4f),
				DoF = new(0, 0.169f),
				MaxOpacity = 200,
				MaxChildRequestId = 6000,
				ScreenSize = new(100, 100),
				N = tree.N,
			};

			//new TestRun(tree).Run(input);
			using MainWindow win = new(1000, 1000, "SVO Tracer", tree, input);
			win.Run();
		}
	}
}
