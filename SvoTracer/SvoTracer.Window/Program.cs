using System;
using System.Collections.Generic;
using OpenTK.Graphics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SvoTracer.Kernel;

namespace SvoTracer.Window
{
	class Program
	{
        [STAThread]
        private static void Main(string[] args)
        {
            var testRun = new TestRun();
            //testRun.Run();

            using (MainWindow win = new MainWindow(1000,1000, "SVO Tracer"))
            {
                win.Run();
            }
        }
    }
}
