using SvoTracer.Kernel;
using SvoTracer.Domain;
using SvoTracer.Domain.Models;
using System;
using System.Linq;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Compute.OpenCL;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using OpenTK.Windowing.Common;
using System.Runtime.InteropServices;
using SvoTracer.Domain.Serializers;

namespace SvoTracer.Window
{
	class MainWindow : GameWindow
	{
		#region //Local Variables
		private uint parentMaxSize = 6000;
		private uint blockCount = 0;
		private bool initialized = false;

		private readonly ComputeManager _computeManager;
		private readonly WorldManager _worldManager = new();
		private readonly StateManager _stateManager;
		private RenderbufferHandle glRenderbuffer = RenderbufferHandle.Zero;
		private FramebufferHandle framebuffer = FramebufferHandle.Zero;
		#endregion

		#region //Constructor
		public MainWindow(int width, int height, string title, Octree tree, TraceInput input)
			: base(GameWindowSettings.Default, new NativeWindowSettings()
			{
				Title = title,
				Size = new Vector2i(width, height)
			})
		{
			_stateManager = new(input);
			_computeManager = buildComputeManager(new[] { "kernel.cl" });
			setupKernels(tree);
		}

		unsafe private ComputeManager buildComputeManager(string[] programFiles)
		{
			return ComputeManagerFactory.Build(GLFW.GetWGLContext(WindowPtr), GLFW.GetWin32Window(base.WindowPtr), programFiles);
		}

		private void setupKernels(Octree octree)
		{
			_stateManager.TraceInput.BaseDepth = octree.BaseDepth;
			_stateManager.UpdateInput.BaseDepth = octree.BaseDepth;
			_stateManager.UpdateInput.MemorySize = octree.BlockCount;
			blockCount = octree.BlockCount;
			parentMaxSize = 6000;

			// Usage contains one element for every block for recording when it was last used
			var usage = new Usage[octree.BlockCount >> 3];
			var baseStart = (int)TreeBuilder.PowSum((byte)(octree.BaseDepth - 1));
			var range = TreeBuilder.PowSum(octree.BaseDepth);
			//This iterates over the BaseDepth+2 level and makes blocks inviolate
			for (int i = 0; i < range - baseStart; i++)
				for (int j = 3; j <= byte.MaxValue; j <<= 2)
					if ((octree.BaseBlocks[i + baseStart] & j) == j)
					{
						usage[i].Tick = ushort.MaxValue;
						usage[i].Parent = uint.MaxValue;
						break;
					}

			_computeManager.InitReadBuffer(BufferName.Bases, octree.BaseBlocks);
			_computeManager.InitReadBuffer(BufferName.Blocks, octree.Blocks.Serialize());
			_computeManager.InitReadBuffer(BufferName.Usages, usage.Serialize());
			_computeManager.InitBuffer(BufferName.ChildRequestID, new uint[1]);
			_computeManager.InitBuffer(BufferName.ChildRequests, new byte[_stateManager.TraceInput.MaxChildRequestId * ChildRequest.Size]);
			_computeManager.InitBuffer(BufferName.ParentSize, new uint[1]);
			_computeManager.InitBuffer(BufferName.ParentResidency, new bool[parentMaxSize]);
			_computeManager.InitBuffer(BufferName.Parents, new byte[parentMaxSize * Parent.Size]);
			_computeManager.InitBuffer(BufferName.DereferenceQueue, new ulong[octree.BlockCount]);
			_computeManager.InitBuffer(BufferName.DereferenceRemaining, new uint[1]);
			_computeManager.InitBuffer(BufferName.Semaphor, new uint[1]);

			_computeManager.InitDeviceBuffer<float>(BufferName.Origins, Size.X * Size.Y * 3);
			_computeManager.InitDeviceBuffer<float>(BufferName.Directions, Size.X * Size.Y * 3);
			_computeManager.InitDeviceBuffer<float>(BufferName.FoVs, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<ulong>(BufferName.Locations, Size.X * Size.Y * 3);
			_computeManager.InitDeviceBuffer<byte>(BufferName.Depths, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<byte>(BufferName.Weightings, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.Parents, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BaseTraces, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BlockTraces, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BlockTraceQueue, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BlockTraceQueueID, 1);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BaseTraceQueue, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BaseTraceQueueID, 1);
			_computeManager.InitDeviceBuffer<uint>(BufferName.ColourRs, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.ColourGs, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.ColourBs, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.FinalColourRs, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.FinalColourGs, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.FinalColourBs, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.FinalWeightings, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.Luminosities, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<float>(BufferName.RayLengths, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BackgroundQueue, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BackgroundQueueID, 1);
			_computeManager.InitDeviceBuffer<uint>(BufferName.MaterialQueue, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.MaterialQueueID, 1);
			_computeManager.InitDeviceBuffer<uint>(BufferName.AccumulatorID, 1);

			_computeManager.SetArg(KernelName.Prune, "bases", BufferName.Bases);
			_computeManager.SetArg(KernelName.Prune, "blocks", BufferName.Blocks);
			_computeManager.SetArg(KernelName.Prune, "usage", BufferName.Usages);
			_computeManager.SetArg(KernelName.Prune, "childRequestId", BufferName.ChildRequestID);
			_computeManager.SetArg(KernelName.Prune, "childRequests", BufferName.ChildRequests);
			_computeManager.SetArg(KernelName.Prune, "parentSize", BufferName.ParentSize);
			_computeManager.SetArg(KernelName.Prune, "parentResidency", BufferName.ParentResidency);
			_computeManager.SetArg(KernelName.Prune, "parents", BufferName.Parents);
			_computeManager.SetArg(KernelName.Prune, "dereferenceQueue", BufferName.DereferenceQueue);
			_computeManager.SetArg(KernelName.Prune, "dereferenceRemaining", BufferName.DereferenceRemaining);
			_computeManager.SetArg(KernelName.Prune, "semaphor", BufferName.Semaphor);

			_computeManager.SetArg(KernelName.Graft, "blocks", BufferName.Blocks);
			_computeManager.SetArg(KernelName.Graft, "usage", BufferName.Usages);
			_computeManager.SetArg(KernelName.Graft, "childRequestId", BufferName.ChildRequestID);
			_computeManager.SetArg(KernelName.Graft, "childRequests", BufferName.ChildRequests);
			_computeManager.SetArg(KernelName.Graft, "parentSize", BufferName.ParentSize);
			_computeManager.SetArg(KernelName.Graft, "parentResidency", BufferName.ParentResidency);
			_computeManager.SetArg(KernelName.Graft, "parents", BufferName.Parents);
			_computeManager.SetArg(KernelName.Graft, "dereferenceQueue", BufferName.DereferenceQueue);
			_computeManager.SetArg(KernelName.Graft, "dereferenceRemaining", BufferName.DereferenceRemaining);
			_computeManager.SetArg(KernelName.Graft, "semaphor", BufferName.Semaphor);

			_computeManager.SetArg(KernelName.Trace, "bases", BufferName.Bases);
			_computeManager.SetArg(KernelName.Trace, "blocks", BufferName.Blocks);
			_computeManager.SetArg(KernelName.Trace, "usage", BufferName.Usages);
			_computeManager.SetArg(KernelName.Trace, "childRequestId", BufferName.ChildRequestID);
			_computeManager.SetArg(KernelName.Trace, "childRequests", BufferName.ChildRequests);



			_computeManager.SetArg(KernelName.Init, "Origins", BufferName.Origins);
			_computeManager.SetArg(KernelName.Init, "Directions", BufferName.Directions);
			_computeManager.SetArg(KernelName.Init, "FoVs", BufferName.FoVs);
			_computeManager.SetArg(KernelName.Init, "Locations", BufferName.Locations);
			_computeManager.SetArg(KernelName.Init, "Depths", BufferName.Depths);
			_computeManager.SetArg(KernelName.Init, "BaseTraceQueue", BufferName.BaseTraceQueue);
			_computeManager.SetArg(KernelName.Init, "BaseTraceQueueID", BufferName.BaseTraceQueueID);

			_computeManager.SetArg(KernelName.RunBaseTrace, "Bases", BufferName.Bases);
			_computeManager.SetArg(KernelName.RunBaseTrace, "Origins", BufferName.Origins);
			_computeManager.SetArg(KernelName.RunBaseTrace, "Directions", BufferName.Directions);
			_computeManager.SetArg(KernelName.RunBaseTrace, "FoVs", BufferName.FoVs);
			_computeManager.SetArg(KernelName.RunBaseTrace, "Locations", BufferName.Locations);
			_computeManager.SetArg(KernelName.RunBaseTrace, "Weightings", BufferName.Weightings);
			_computeManager.SetArg(KernelName.RunBaseTrace, "Depths", BufferName.Depths);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BaseTraces", BufferName.BaseTraces);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BlockTraces", BufferName.BlockTraces);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BlockTraceQueue", BufferName.BlockTraceQueue);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BlockTraceQueueID", BufferName.BlockTraceQueueID);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BaseTraceQueue", BufferName.BaseTraceQueue);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BaseTraceQueueID", BufferName.BaseTraceQueueID);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BackgroundQueue", BufferName.BackgroundQueue);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BackgroundQueueID", BufferName.BackgroundQueueID);

			_computeManager.SetArg(KernelName.RunBlockTrace, "Blocks", BufferName.Blocks);
			_computeManager.SetArg(KernelName.RunBlockTrace, "Usages", BufferName.Usages);
			_computeManager.SetArg(KernelName.RunBlockTrace, "ChildRequestID", BufferName.ChildRequestID);
			_computeManager.SetArg(KernelName.RunBlockTrace, "ChildRequests", BufferName.ChildRequests);
			_computeManager.SetArg(KernelName.RunBlockTrace, "Origins", BufferName.Origins);
			_computeManager.SetArg(KernelName.RunBlockTrace, "Directions", BufferName.Directions);
			_computeManager.SetArg(KernelName.RunBlockTrace, "FoVs", BufferName.FoVs);
			_computeManager.SetArg(KernelName.RunBlockTrace, "Locations", BufferName.Locations);
			_computeManager.SetArg(KernelName.RunBlockTrace, "Weightings", BufferName.Weightings);
			_computeManager.SetArg(KernelName.RunBlockTrace, "Depths", BufferName.Depths);
			_computeManager.SetArg(KernelName.RunBlockTrace, "BlockTraces", BufferName.BlockTraces);
			_computeManager.SetArg(KernelName.RunBlockTrace, "BlockTraceQueue", BufferName.BlockTraceQueue);
			_computeManager.SetArg(KernelName.RunBlockTrace, "BlockTraceQueueID", BufferName.BlockTraceQueueID);
			_computeManager.SetArg(KernelName.RunBlockTrace, "BaseTraceQueue", BufferName.BaseTraceQueue);
			_computeManager.SetArg(KernelName.RunBlockTrace, "BaseTraceQueueID", BufferName.BaseTraceQueueID);
			_computeManager.SetArg(KernelName.RunBlockTrace, "BackgroundQueue", BufferName.BackgroundQueue);
			_computeManager.SetArg(KernelName.RunBlockTrace, "BackgroundQueueID", BufferName.BackgroundQueueID);
			_computeManager.SetArg(KernelName.RunBlockTrace, "MaterialQueue", BufferName.MaterialQueue);
			_computeManager.SetArg(KernelName.RunBlockTrace, "MaterialQueueID", BufferName.MaterialQueueID);

			_computeManager.SetArg(KernelName.EvaluateBackground, "Directions", BufferName.Directions);
			_computeManager.SetArg(KernelName.EvaluateBackground, "Weightings", BufferName.Weightings);
			_computeManager.SetArg(KernelName.EvaluateBackground, "BackgroundQueue", BufferName.BackgroundQueue);
			_computeManager.SetArg(KernelName.EvaluateBackground, "Luminosities", BufferName.Luminosities);
			_computeManager.SetArg(KernelName.EvaluateBackground, "ColourRs", BufferName.ColourRs);
			_computeManager.SetArg(KernelName.EvaluateBackground, "ColourGs", BufferName.ColourGs);
			_computeManager.SetArg(KernelName.EvaluateBackground, "ColourBs", BufferName.ColourBs);

			_computeManager.SetArg(KernelName.EvaluateMaterial, "Blocks", BufferName.Blocks);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "Origins", BufferName.Origins);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "Directions", BufferName.Directions);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "FoVs", BufferName.FoVs);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "Locations", BufferName.Locations);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "Depths", BufferName.Depths);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "Weightings", BufferName.Weightings);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "ColourRs", BufferName.ColourRs);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "ColourGs", BufferName.ColourGs);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "ColourBs", BufferName.ColourBs);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "RayLengths", BufferName.RayLengths);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "Luminosities", BufferName.Luminosities);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "ParentTraces", BufferName.ParentTraces);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "RootDirections", BufferName.RootDirections);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "RootLocations", BufferName.RootLocations);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "RootDepths", BufferName.RootDepths);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "RootWeightings", BufferName.RootWeightings);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "RootParentTraces", BufferName.RootParentTraces);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "BaseTraceQueue", BufferName.BaseTraceQueue);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "AccumulatorID", BufferName.AccumulatorID);

			_computeManager.SetArg(KernelName.ResolveAccumulators, "FinalColourRs", BufferName.FinalColourRs);
			_computeManager.SetArg(KernelName.ResolveAccumulators, "FinalColourGs", BufferName.FinalColourGs);
			_computeManager.SetArg(KernelName.ResolveAccumulators, "FinalColourBs", BufferName.FinalColourBs);
			_computeManager.SetArg(KernelName.ResolveAccumulators, "FinalWeightings", BufferName.FinalWeightings);
			_computeManager.SetArg(KernelName.ResolveAccumulators, "ColourRs", BufferName.ColourRs);
			_computeManager.SetArg(KernelName.ResolveAccumulators, "ColourGs", BufferName.ColourGs);
			_computeManager.SetArg(KernelName.ResolveAccumulators, "ColourBs", BufferName.ColourBs);
			_computeManager.SetArg(KernelName.ResolveAccumulators, "Weightings", BufferName.Weightings);
			_computeManager.SetArg(KernelName.ResolveAccumulators, "Luminosities", BufferName.Luminosities);
			_computeManager.SetArg(KernelName.ResolveAccumulators, "ParentTraces", BufferName.ParentTraces);

			_computeManager.SetArg(KernelName.DrawTrace, "FinalColourRs", BufferName.FinalColourRs);
			_computeManager.SetArg(KernelName.DrawTrace, "FinalColourGs", BufferName.FinalColourGs);
			_computeManager.SetArg(KernelName.DrawTrace, "FinalColourBs", BufferName.FinalColourBs);
			_computeManager.SetArg(KernelName.DrawTrace, "FinalWeightings", BufferName.FinalWeightings);
		}

		#endregion

		#region //Load

		protected override void OnLoad()
		{
			base.OnLoad();
			Stopwatch watch = new Stopwatch();
			watch.Start();
			setDebug();

			try
			{
				// Set up the buffers
				framebuffer = GL.CreateFramebuffer();
				GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
				initScreenBuffers();
				initialized = true;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
				Console.ReadLine();
				base.Close();
			}
			finally
			{
				watch.Stop();
				Console.WriteLine(watch.Elapsed);
			}
		}

		private void initScreenBuffers()
		{
			// Remove buffers if they already exist
			if (glRenderbuffer != RenderbufferHandle.Zero)
				GL.DeleteRenderbuffer(glRenderbuffer);

			// Initializes the render buffer
			glRenderbuffer = GL.CreateRenderbuffer();
			GL.NamedRenderbufferStorage(glRenderbuffer, InternalFormat.Rgba32f, Size.X, Size.Y);
			GL.NamedFramebufferRenderbuffer(framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, glRenderbuffer);
			var err = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
			if (err != FramebufferStatus.FramebufferComplete)
				throw new Exception($"CheckFramebufferStatus returned: {err}");
			GL.Flush();

			// Setup CL renderbuffer
			_computeManager.InitRenderbuffer((uint)glRenderbuffer.Handle);
		}

		#endregion

		#region //Data Processing

		private void runKernels()
		{
			runPrune();
			runGraft();
			runTrace();
		}

		private void runPrune()
		{
			var pruningData = _worldManager.GetPruningData();
			if (pruningData == null) return;

			_computeManager.InitBuffer(BufferName.Pruning, pruningData.Pruning.Serialize());
			_computeManager.SetArg(KernelName.Prune, "pruning", BufferName.Pruning);

			_computeManager.InitBuffer(BufferName.PruningBlockData, pruningData.PruningBlockData.Serialize());
			_computeManager.SetArg(KernelName.Prune, "pruningBlockData", BufferName.Bases);

			_computeManager.InitBuffer(BufferName.PruningAddresses, pruningData.PruningAddresses.Serialize());
			_computeManager.SetArg(KernelName.Prune, "pruningAddresses", BufferName.Bases);
			_computeManager.SetArg(KernelName.Prune, "inputData", _stateManager.UpdateInput.Serialize());

			var waitEvents = new[]{
				_computeManager.WriteBuffer(BufferName.DereferenceQueue, new ulong[blockCount], null),
				_computeManager.WriteBuffer(BufferName.DereferenceRemaining, new uint[] { 0 }, null),
				_computeManager.WriteBuffer(BufferName.Semaphor, new int[] { 0 }, null)
			};

			_computeManager.Enqueue(KernelName.Prune, new[] { (nuint)pruningData.Pruning.Length }, waitEvents);
			_computeManager.Flush();
		}

		private void runGraft()
		{
			var graftingData = _worldManager.GetGraftingData();
			if (graftingData == null) return;

			_stateManager.UpdateInput.GraftSize = (uint)graftingData.GraftingBlocks.Count();
			_computeManager.InitBuffer(BufferName.Grafting, graftingData.Grafting.Serialize());
			_computeManager.SetArg(KernelName.Graft, "grafting", BufferName.Pruning);

			_computeManager.InitBuffer(BufferName.GraftingBlocks, graftingData.GraftingBlocks.Serialize());
			_computeManager.SetArg(KernelName.Graft, "graftingBlocks", BufferName.Bases);

			_computeManager.InitBuffer(BufferName.GraftingAddresses, graftingData.GraftingAddresses.Serialize());
			_computeManager.SetArg(KernelName.Graft, "graftingAddresses", BufferName.Bases);

			_computeManager.InitBuffer(BufferName.HoldingAddresses, new uint[_stateManager.UpdateInput.GraftSize]);
			_computeManager.SetArg(KernelName.Graft, "holdingAddresses", BufferName.Bases);

			_computeManager.InitBuffer(BufferName.AddressPosition, new uint[] { 0 });
			_computeManager.SetArg(KernelName.Graft, "addressPosition", BufferName.Bases);
			_computeManager.SetArg(KernelName.Graft, "inputData", _stateManager.UpdateInput.Serialize());

			var waitEvents = new[]{
				_computeManager.WriteBuffer(BufferName.DereferenceQueue, new ulong[blockCount], null),
				_computeManager.WriteBuffer(BufferName.DereferenceRemaining, new uint[] { 0 }, null),
				_computeManager.WriteBuffer(BufferName.Semaphor, new int[] { 0 }, null)
			};

			_computeManager.Enqueue(KernelName.Graft, new[] { (nuint)graftingData.Grafting.Length }, waitEvents);
			_computeManager.Flush();
		}

		private void runTrace()
		{
			Stopwatch timer = new();
			timer.Start();
			//Flush child request buffer
			var (renderbuffer, waitEvent) = _computeManager.AcquireRenderbuffer();
			_computeManager.SetArg(KernelName.Trace, "outputImage", renderbuffer);
			var waitEvent2 = _computeManager.WriteBuffer(BufferName.ChildRequestID, new uint[] { 0 });
			_computeManager.SetArg(KernelName.Trace, "input", _stateManager.TraceInput.Serialize());

			var kernelRun = _computeManager.Enqueue(KernelName.Trace, new[] { (nuint)Size.X, (nuint)Size.Y }, new[] { waitEvent, waitEvent2 });
			_computeManager.ReleaseRenderbuffer(new[] { kernelRun });
			_computeManager.Flush();
			timer.Stop();
			Console.Write("\r{0}", 1000.0f / timer.ElapsedMilliseconds);
		}

		private void runWavefront()
		{
			Stopwatch timer = new();
			timer.Start();
			var traceInput = _stateManager.TraceInput;
			var input = traceInput.Serialize();
			_computeManager.SetArg(KernelName.Init, "input", input);
			_computeManager.SetArg(KernelName.RunBaseTrace, "input", input);
			_computeManager.SetArg(KernelName.RunBlockTrace, "input", input);
			_computeManager.SetArg(KernelName.EvaluateBackground, "input", input);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "input", input);
			_computeManager.SetArg(KernelName.ResolveAccumulators, "input", input);
			_computeManager.SetArg(KernelName.DrawTrace, "input", input);
			var childRequestEvent = _computeManager.WriteBuffer(BufferName.ChildRequestID, new uint[] { 0 });

			// trace initial rays
			var initEvent = _computeManager.Enqueue(KernelName.Init, new[] { (nuint)Size.X, (nuint)Size.Y }, new[] { childRequestEvent });
			runTraces(initEvent);

			traceInput.DoF = Vector2.Zero;
			input = traceInput.Serialize();
			_computeManager.SetArg(KernelName.RunBaseTrace, "input", input);
			_computeManager.SetArg(KernelName.RunBlockTrace, "input", input);

			// trace material rays
			var evaluateMaterialEvent = evaulateMaterial(null);
			runTraces(evaluateMaterialEvent);

			// resolve background illumination
			var backgroundEvent = evaulateBackground(null);
			var accumulatorEvent = _computeManager.Enqueue(KernelName.ResolveAccumulators, new[] { (nuint)Size.X, (nuint)Size.Y }, new[] { backgroundEvent });

			//Flush child request buffer
			var (renderbuffer, acquireBufferEvent) = _computeManager.AcquireRenderbuffer();
			_computeManager.SetArg(KernelName.DrawTrace, "outputImage", renderbuffer);
			_computeManager.SetArg(KernelName.DrawTrace, "input", input);

			var drawEvent = _computeManager.Enqueue(KernelName.DrawTrace, new[] { (nuint)Size.X, (nuint)Size.Y }, new[] { acquireBufferEvent, accumulatorEvent });
			_computeManager.ReleaseRenderbuffer(new[] { drawEvent });
			_computeManager.Flush();
			timer.Stop();
			Console.Write("\r{0}", 1000.0f / timer.ElapsedMilliseconds);
		}

		private void runTraces(OpenTK.Compute.OpenCL.CLEvent blockTraceEvent)
		{
			OpenTK.Compute.OpenCL.CLEvent baseTraceEvent;
			bool baseTraceRan, blockTraceRan, tracesRemain = true;
			while (tracesRemain)
			{
				(baseTraceRan, baseTraceEvent) = runBaseTrace(new[] { blockTraceEvent });
				(blockTraceRan, blockTraceEvent) = runBlockTrace(new[] { baseTraceEvent });
				tracesRemain = baseTraceRan || blockTraceRan;
			}
		}

		private (bool, OpenTK.Compute.OpenCL.CLEvent) runBaseTrace(OpenTK.Compute.OpenCL.CLEvent[] initEvent)
		{
			var (baseTraceSize, baseTraceEvent) = _computeManager.ResetIDBuffer(BufferName.BaseTraceQueueID, initEvent);
			if (baseTraceSize == 0) return (false, baseTraceEvent);
			_computeManager.Swap(BufferName.BaseTraces, BufferName.BaseTraceQueue);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BaseTraces", BufferName.BaseTraces);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BaseTraceQueue", BufferName.BaseTraceQueue);
			return (true, _computeManager.Enqueue(KernelName.RunBaseTrace, new[] { (nuint)baseTraceSize }, new[] { baseTraceEvent }));
		}

		private (bool, OpenTK.Compute.OpenCL.CLEvent) runBlockTrace(OpenTK.Compute.OpenCL.CLEvent[] initEvent)
		{
			var (blockTraceSize, blockTraceEvent) = _computeManager.ResetIDBuffer(BufferName.BlockTraceQueueID, initEvent);
			if (blockTraceSize == 0) return (false, blockTraceEvent);
			_computeManager.Swap(BufferName.BlockTraces, BufferName.BlockTraceQueue);
			_computeManager.SetArg(KernelName.RunBlockTrace, "BaseTraces", BufferName.BaseTraces);
			_computeManager.SetArg(KernelName.RunBlockTrace, "BaseTraceQueue", BufferName.BaseTraceQueue);
			return (true, _computeManager.Enqueue(KernelName.RunBlockTrace, new[] { (nuint)blockTraceSize }, new[] { blockTraceEvent }));
		}

		private OpenTK.Compute.OpenCL.CLEvent evaulateMaterial(OpenTK.Compute.OpenCL.CLEvent[] initEvent)
		{
			var (materialQueueSize, materialQueueEvent) = _computeManager.ResetIDBuffer(BufferName.MaterialQueueID, initEvent);
			if (materialQueueSize == 0) return materialQueueEvent;
			return _computeManager.Enqueue(KernelName.EvaluateMaterial, new[] { (nuint)materialQueueSize }, new[] { materialQueueEvent });
		}

		private OpenTK.Compute.OpenCL.CLEvent evaulateBackground(OpenTK.Compute.OpenCL.CLEvent[] initEvent)
		{
			var (backgroundQueueSize, backgroundQueueEvent) = _computeManager.ResetIDBuffer(BufferName.BackgroundQueueID, initEvent);
			if (backgroundQueueSize == 0) return backgroundQueueEvent;
			return _computeManager.Enqueue(KernelName.EvaluateBackground, new[] { (nuint)backgroundQueueSize }, new[] { backgroundQueueEvent });
		}
		#endregion

		#region //Screen Events

		protected override void OnUnload()
		{
			base.OnUnload();

			_computeManager.Dispose();
			GL.DeleteRenderbuffer(glRenderbuffer);
			GL.DeleteFramebuffer(framebuffer);
		}

		protected override void OnResize(ResizeEventArgs e)
		{
			base.OnResize(e);

			initScreenBuffers();
			_stateManager.UpdateScreenSize(Size);
		}

		protected override void OnUpdateFrame(FrameEventArgs e)
		{
			base.OnUpdateFrame(e);

			if (KeyboardState.IsKeyDown(Keys.Escape))
				Close();
		}

		protected override void OnRenderFrame(FrameEventArgs e)
		{
			if (!initialized) return;
			base.OnRenderFrame(e);

			// Update State
			_stateManager.ReadInput(MouseState, KeyboardState);
			_stateManager.IncrementTick();

			// Render Scene
			runKernels();

			// Display Scene
			GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FramebufferHandle.Zero);
			GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
			GL.BlitFramebuffer(0, 0, Size.X, Size.Y, 0, 0, Size.X, Size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

			SwapBuffers();
		}

		#endregion

		#region //Error

		private static GLDebugProc _debugProcCallback = DebugCallback;


		private void setDebug()
		{
			GL.DebugMessageCallback(_debugProcCallback, IntPtr.Zero);
			GL.Enable(EnableCap.DebugOutput);
			GL.Enable(EnableCap.DebugOutputSynchronous);

		}

		private static void DebugCallback(DebugSource source, DebugType type, uint id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
		{
			string messageString = Marshal.PtrToStringAnsi(message, length);
			Console.WriteLine($"{severity} {type} | {messageString}");

			if (type == DebugType.DebugTypeError)
				throw new Exception(messageString);
		}

		protected void OnError(IntPtr waitEvent, IntPtr userData)
		{

		}

		protected void HandleResultCode(CLResultCode resultCode, string method)
		{
			if (resultCode == CLResultCode.Success) return;
			throw new Exception($"{method}: {Enum.GetName(resultCode)}");
		}
		#endregion
	}
}
