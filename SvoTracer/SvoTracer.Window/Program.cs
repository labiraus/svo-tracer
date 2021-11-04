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
			//new TestRun().Run();

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
			using MainWindow win = new MainWindow(1000, 1000, "SVO Tracer", builder, treeManager);
			win.Run();
		}
    }
}
