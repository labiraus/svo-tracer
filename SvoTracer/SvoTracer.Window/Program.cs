using SvoTracer.Domain;
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
			ushort maxDepth = 9;
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

			//new TestRun(tree).Run();
			using MainWindow win = new MainWindow(1000, 1000, "SVO Tracer", tree);
			win.Run();
		}
	}
}
