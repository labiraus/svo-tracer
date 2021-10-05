using System;
using System.Numerics;
using SvoTracer.Domain;
using SvoTracer.Domain.Model;
using SvoTracer.Kernel;

namespace SvoTracer
{
    public class TestRun
    {
        public void Run()
        {
            var builder = new CubeBuilder(
                new Vector3(0.3f, 0.3f, 0.3f),
                new Vector3(0.3f, 0.3f, 0.6f),
                new Vector3(0.3f, 0.6f, 0.6f),
                new Vector3(0.3f, 0.6f, 0.3f),
                new Vector3(0.6f, 0.3f, 0.3f),
                new Vector3(0.6f, 0.3f, 0.6f),
                new Vector3(0.6f, 0.6f, 0.6f),
                new Vector3(0.6f, 0.6f, 0.3f));
            //var builder = new CubeBuilder(
            //    new Vector3(0.1f, 0.1f, 0.9f),
            //    new Vector3(0.9f, 0.1f, 0.9f),
            //    new Vector3(0.9f, 0.9f, 0.9f),
            //    new Vector3(0.1f, 0.9f, 0.9f),
            //    new Vector3(0.1f, 0.1f, 0.1f),
            //    new Vector3(0.9f, 0.1f, 0.1f),
            //    new Vector3(0.9f, 0.9f, 0.1f),
            //    new Vector3(0.1f, 0.9f, 0.1f));
            var input = new TraceInputData(
                new Vector3(0.5f, 0.5f, -2f),
                new Vector3(0, (float)Math.PI / 2f, 0),
                new Vector2((float)Math.PI / 4f, (float)Math.PI / 4f),
                new Vector2(0, 0.169f),
                235,
                80,
                200,
                5,
                0,
                6000);

            try
            {
                Octree tree;
                tree = builder.BuildTree(input.N, 11, uint.MaxValue / 64);
                builder.SaveTree("test", input.N, tree);
                return;
                //tree = builder.LoadTree("test");
                for (uint i = 0; i < input.ScreenSize.Y; i++)
                {
                    for (uint j = 0; j < input.ScreenSize.X; j++)
                        try
                        {
                            KernelMirror.VoxelTrace($"Success for {j} {i}: ", input, tree.BaseBlocks, tree.Blocks, new uint[] { j, i });
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error for {j} {i}");
                        }
                    Console.WriteLine();
                }
            }
            catch (Exception e)
            {
            }
        }
    }
}
