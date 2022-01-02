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
using System.Collections.Generic;
using CLEvent = OpenTK.Compute.OpenCL.CLEvent;

namespace SvoTracer.Window
{
	class MainWindow : GameWindow
	{
		#region //Local Variables
		private uint parentMaxSize = 6000;
		private uint blockCount = 0;
		private int bufferSize = 4000000;
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
			//_computeManager = buildComputeManager(new[] { "kernel.cl" });
			_computeManager = buildComputeManager(new[] { "wavefront.cl" });
			SetupWavefrontKernels(tree);
		}

		unsafe private ComputeManager buildComputeManager(string[] programFiles)
		{
			return ComputeManagerFactory.Build(GLFW.GetWGLContext(WindowPtr), GLFW.GetWin32Window(base.WindowPtr), programFiles);
		}

		private void SetupMegaKernels(Octree octree)
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


		}

		private void SetupWavefrontKernels(Octree octree)
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
			_computeManager.InitBuffer(BufferName.Usages, usage.Serialize());
			_computeManager.InitBuffer(BufferName.ChildRequestID, new uint[1]);
			_computeManager.InitBuffer(BufferName.ChildRequests, new byte[_stateManager.TraceInput.MaxChildRequestId * ChildRequest.Size]);

			_computeManager.InitBuffer(BufferName.ParentSize, new uint[1]);
			_computeManager.InitBuffer(BufferName.ParentResidency, new bool[parentMaxSize]);
			_computeManager.InitBuffer(BufferName.Parents, new byte[parentMaxSize * Parent.Size]);
			_computeManager.InitBuffer(BufferName.DereferenceQueue, new ulong[octree.BlockCount]);
			_computeManager.InitBuffer(BufferName.DereferenceRemaining, new uint[1]);
			_computeManager.InitBuffer(BufferName.Semaphor, new uint[1]);

			_computeManager.InitDeviceBuffer<uint>(BufferName.RootParentTraces, bufferSize);
			_computeManager.InitDeviceBuffer<byte>(BufferName.RootWeightings, bufferSize);

			_computeManager.InitDeviceBuffer<uint>(BufferName.ParentTraces, bufferSize);
			_computeManager.InitDeviceBuffer<byte>(BufferName.ColourRs, bufferSize);
			_computeManager.InitDeviceBuffer<byte>(BufferName.ColourGs, bufferSize);
			_computeManager.InitDeviceBuffer<byte>(BufferName.ColourBs, bufferSize);
			_computeManager.InitDeviceBuffer<byte>(BufferName.Weightings, bufferSize);
			// These might change depending on how we do light
			_computeManager.InitDeviceBuffer<byte>(BufferName.Luminosities, bufferSize);
			_computeManager.InitDeviceBuffer<float>(BufferName.RayLengths, bufferSize);

			_computeManager.InitDeviceBuffer<uint>(BufferName.BaseTraces, bufferSize);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BaseTraceQueue, bufferSize);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BlockTraces, bufferSize);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BlockTraceQueue, bufferSize);
			_computeManager.InitDeviceBuffer<uint>(BufferName.BackgroundQueue, bufferSize);
			_computeManager.InitDeviceBuffer<uint>(BufferName.MaterialQueue, bufferSize);
			_computeManager.InitBuffer(BufferName.BlockTraceQueueID, new uint[1]);
			_computeManager.InitBuffer(BufferName.BaseTraceQueueID, new uint[1]);
			_computeManager.InitBuffer(BufferName.BackgroundQueueID, new uint[1]);
			_computeManager.InitBuffer(BufferName.MaterialQueueID, new uint[1]);
			_computeManager.InitBuffer(BufferName.AccumulatorID, new uint[1]);

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
		}

		#endregion

		#region //Load

		protected override void OnLoad()
		{
			base.OnLoad();
			Stopwatch watch = new Stopwatch();
			watch.Start();
			SetDebug();

			try
			{
				// Set up the buffers
				framebuffer = GL.CreateFramebuffer();
				GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
				InitScreenBuffers();
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

		private void InitScreenBuffers()
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

			// Setup sized buffers
			_computeManager.InitDeviceBuffer<float>(BufferName.Origins, Math.Max(Size.X * Size.Y, bufferSize) * 3);
			_computeManager.InitDeviceBuffer<float>(BufferName.Directions, Math.Max(Size.X * Size.Y, bufferSize) * 3);
			_computeManager.InitDeviceBuffer<float>(BufferName.FoVs, Math.Max(Size.X * Size.Y, bufferSize));
			_computeManager.InitDeviceBuffer<ulong>(BufferName.Locations, Math.Max(Size.X * Size.Y, bufferSize) * 3);
			_computeManager.InitDeviceBuffer<byte>(BufferName.Depths, Math.Max(Size.X * Size.Y, bufferSize));

			// These args are always before the EvaluateMaterial kernel is run
			_computeManager.InitDeviceBuffer<float>(BufferName.RootDirections, Math.Max(Size.X * Size.Y, bufferSize) * 3);
			_computeManager.InitDeviceBuffer<ulong>(BufferName.RootLocations, Math.Max(Size.X * Size.Y, bufferSize) * 3);
			_computeManager.InitDeviceBuffer<byte>(BufferName.RootDepths, Math.Max(Size.X * Size.Y, bufferSize));

			_computeManager.InitDeviceBuffer<uint>(BufferName.FinalColourRs, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.FinalColourGs, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.FinalColourBs, Size.X * Size.Y);
			_computeManager.InitDeviceBuffer<uint>(BufferName.FinalWeightings, Size.X * Size.Y);
		}

		#endregion

		#region //Data Processing

		private void RunKernels()
		{
			RunPrune();
			RunGraft();
			RunWavefront();
		}

		private void RunPrune()
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

		private void RunGraft()
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

		private void RunTrace()
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

		private void RunWavefront()
		{
			Stopwatch timer = new();
			timer.Start();
			//Flush child request buffer
			var (renderbuffer, acquireBufferEvent) = _computeManager.AcquireRenderbuffer();

			var waitList = new CLEvent[]
			{
				_computeManager.FillBuffer(BufferName.FinalColourRs, 0),
				_computeManager.FillBuffer(BufferName.FinalColourGs, 0),
				_computeManager.FillBuffer(BufferName.FinalColourBs, 0),
				_computeManager.FillBuffer(BufferName.FinalWeightings, 0),
				_computeManager.ZeroIDBuffer(BufferName.BlockTraceQueueID, null),
				_computeManager.ZeroIDBuffer(BufferName.BaseTraceQueueID, null),
				_computeManager.ZeroIDBuffer(BufferName.BackgroundQueueID, null),
				_computeManager.ZeroIDBuffer(BufferName.MaterialQueueID, null),
				_computeManager.ZeroIDBuffer(BufferName.AccumulatorID, null),
				_computeManager.ZeroIDBuffer(BufferName.ChildRequestID, null)
			};

			// Copy the struct so that changing the DoF later doesn't update the original
			var traceInput = _stateManager.TraceInput;
			var input = traceInput.Serialize();

			// trace initial rays
			_computeManager.SetArg(KernelName.Init, "input", input);
			CLEvent initEvent = Init(waitList);

			_computeManager.SetArg(KernelName.RunBaseTrace, "input", input);
			_computeManager.SetArg(KernelName.RunBlockTrace, "input", input);
			RunTraces(initEvent);

			traceInput.DoF = Vector2.Zero;
			input = traceInput.Serialize();
			_computeManager.SetArg(KernelName.RunBaseTrace, "input", input);
			_computeManager.SetArg(KernelName.RunBlockTrace, "input", input);

			// trace material rays
			_computeManager.SetArg(KernelName.EvaluateMaterial, "input", input);
			var evaluateMaterialEvent = EvaulateMaterial(null);
			RunTraces(evaluateMaterialEvent);

			// resolve background illumination
			_computeManager.SetArg(KernelName.EvaluateBackground, "input", input);
			var backgroundEvent = EvaulateBackground(null);

			// second bounce
			// _computeManager.SetArg(KernelName.EvaluateMaterial, "input", input);
			// evaluateMaterialEvent = EvaulateMaterial(new[]{backgroundEvent});

			//_computeManager.SetArg(KernelName.RunBaseTrace, "input", input);
			//_computeManager.SetArg(KernelName.RunBlockTrace, "input", input);
			// RunTraces(evaluateMaterialEvent);

			// _computeManager.SetArg(KernelName.EvaluateBackground, "input", input);
			// backgroundEvent = EvaulateBackground(null);
			_computeManager.SetArg(KernelName.ResolveRemainders, "input", input);
			var resolveEvent = ResolveRemainders(new[] { backgroundEvent });


			_computeManager.SetArg(KernelName.DrawTrace, "outputImage", renderbuffer);
			_computeManager.SetArg(KernelName.DrawTrace, "input", input);
			var drawEvent = DrawTrace(new[] { acquireBufferEvent, resolveEvent });

			_computeManager.ReleaseRenderbuffer(new[] { drawEvent });
			_computeManager.Flush();
			timer.Stop();
			Console.Write("\r{0}", 1000.0f / timer.ElapsedMilliseconds);
		}

		private CLEvent Init(CLEvent[] waitList)
		{
			_computeManager.SetArg(KernelName.Init, "Origins", BufferName.Origins);
			_computeManager.SetArg(KernelName.Init, "Directions", BufferName.Directions);
			_computeManager.SetArg(KernelName.Init, "FoVs", BufferName.FoVs);
			_computeManager.SetArg(KernelName.Init, "Locations", BufferName.Locations);
			_computeManager.SetArg(KernelName.Init, "Depths", BufferName.Depths);
			_computeManager.SetArg(KernelName.Init, "BaseTraceQueue", BufferName.BaseTraceQueue);
			_computeManager.SetArg(KernelName.Init, "BaseTraceQueueID", BufferName.BaseTraceQueueID);
			_computeManager.SetArg(KernelName.Init, "BackgroundQueue", BufferName.BackgroundQueue);
			_computeManager.SetArg(KernelName.Init, "BackgroundQueueID", BufferName.BackgroundQueueID);

			var initEvent = _computeManager.Enqueue(KernelName.Init, new[] { (nuint)Size.X, (nuint)Size.Y }, waitList);
			return initEvent;
		}

		private void RunTraces(CLEvent initialEvent)
		{
			CLEvent resetBaseTraceEvent;
			CLEvent baseTraceEvent = OpenTK.Compute.OpenCL.CLEvent.Zero;
			CLEvent resetBlockTraceEvent = initialEvent;
			CLEvent blockTraceEvent = OpenTK.Compute.OpenCL.CLEvent.Zero;
			bool baseTraceRan;
			bool blockTraceRan = false;
			bool tracesRemain = true;
			uint baseTraceSize, blockTraceSize;
			while (tracesRemain)
			{
				(baseTraceSize, resetBaseTraceEvent) = _computeManager.ResetIDBuffer(BufferName.BaseTraceQueueID, new[] { blockTraceRan ? blockTraceEvent : resetBlockTraceEvent });
				if (baseTraceSize > 0)
				{
					baseTraceEvent = RunBaseTrace(baseTraceSize, new[] { resetBaseTraceEvent });
					baseTraceRan = true;
				}
				else baseTraceRan = false;

				(blockTraceSize, resetBlockTraceEvent) = _computeManager.ResetIDBuffer(BufferName.BlockTraceQueueID, new[] { baseTraceRan ? baseTraceEvent : resetBaseTraceEvent });
				if (blockTraceSize > 0)
				{
					blockTraceEvent = RunBlockTrace(blockTraceSize, new[] { resetBlockTraceEvent });
					blockTraceRan = true;
				}
				else blockTraceRan = false;
				tracesRemain = baseTraceRan || blockTraceRan;
			}
		}

		private CLEvent RunBaseTrace(uint baseTraceSize, CLEvent[] waitEvents)
		{
			_computeManager.Swap(BufferName.BaseTraces, BufferName.BaseTraceQueue);

			_computeManager.SetArg(KernelName.RunBaseTrace, "Bases", BufferName.Bases);
			_computeManager.SetArg(KernelName.RunBaseTrace, "Origins", BufferName.Origins);
			_computeManager.SetArg(KernelName.RunBaseTrace, "Directions", BufferName.Directions);
			_computeManager.SetArg(KernelName.RunBaseTrace, "FoVs", BufferName.FoVs);
			_computeManager.SetArg(KernelName.RunBaseTrace, "Locations", BufferName.Locations);
			_computeManager.SetArg(KernelName.RunBaseTrace, "Weightings", BufferName.Weightings);
			_computeManager.SetArg(KernelName.RunBaseTrace, "Depths", BufferName.Depths);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BaseTraces", BufferName.BaseTraces);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BaseTraceQueue", BufferName.BaseTraceQueue);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BaseTraceQueueID", BufferName.BaseTraceQueueID);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BlockTraceQueue", BufferName.BlockTraceQueue);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BlockTraceQueueID", BufferName.BlockTraceQueueID);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BackgroundQueue", BufferName.BackgroundQueue);
			_computeManager.SetArg(KernelName.RunBaseTrace, "BackgroundQueueID", BufferName.BackgroundQueueID);

			return _computeManager.Enqueue(KernelName.RunBaseTrace, new[] { (nuint)baseTraceSize }, waitEvents);
		}

		private CLEvent RunBlockTrace(uint blockTraceSize, CLEvent[] waitEvents)
		{
			_computeManager.Swap(BufferName.BlockTraces, BufferName.BlockTraceQueue);

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
			_computeManager.SetArg(KernelName.RunBlockTrace, "ParentTraces", BufferName.ParentTraces);

			return _computeManager.Enqueue(KernelName.RunBlockTrace, new[] { (nuint)blockTraceSize }, waitEvents);
		}

		private CLEvent EvaulateMaterial(CLEvent[] waitList)
		{
			var (_, accumulatorResetEvent) = _computeManager.ResetIDBuffer(BufferName.AccumulatorID, waitList);
			var (materialQueueSize, materialQueueEvent) = _computeManager.ResetIDBuffer(BufferName.MaterialQueueID, waitList);
			if (materialQueueSize == 0) return materialQueueEvent;

			_computeManager.Swap(BufferName.RootDirections, BufferName.Directions);
			_computeManager.Swap(BufferName.RootLocations, BufferName.Locations);
			_computeManager.Swap(BufferName.RootDepths, BufferName.Depths);
			_computeManager.Swap(BufferName.RootWeightings, BufferName.Weightings);
			_computeManager.Swap(BufferName.RootParentTraces, BufferName.ParentTraces);

			_computeManager.SetArg(KernelName.EvaluateMaterial, "Blocks", BufferName.Blocks);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "Usages", BufferName.Usages);
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
			_computeManager.SetArg(KernelName.EvaluateMaterial, "MaterialQueue", BufferName.MaterialQueue);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "ParentTraces", BufferName.ParentTraces);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "RootDirections", BufferName.RootDirections);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "RootLocations", BufferName.RootLocations);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "RootDepths", BufferName.RootDepths);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "RootWeightings", BufferName.RootWeightings);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "RootParentTraces", BufferName.RootParentTraces);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "BaseTraceQueue", BufferName.BaseTraceQueue);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "BaseTraceQueueID", BufferName.BaseTraceQueueID);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "FinalWeightings", BufferName.FinalWeightings);
			_computeManager.SetArg(KernelName.EvaluateMaterial, "AccumulatorID", BufferName.AccumulatorID);

			return _computeManager.Enqueue(KernelName.EvaluateMaterial, new[] { (nuint)materialQueueSize }, new[] { materialQueueEvent, accumulatorResetEvent });
		}

		private CLEvent EvaulateBackground(CLEvent[] waitList)
		{
			var (backgroundQueueSize, backgroundQueueEvent) = _computeManager.ResetIDBuffer(BufferName.BackgroundQueueID, waitList);
			if (backgroundQueueSize == 0) return backgroundQueueEvent;

			_computeManager.SetArg(KernelName.EvaluateBackground, "BackgroundQueue", BufferName.BackgroundQueue);
			_computeManager.SetArg(KernelName.EvaluateBackground, "Directions", BufferName.Directions);
			_computeManager.SetArg(KernelName.EvaluateBackground, "ParentTraces", BufferName.ParentTraces);
			_computeManager.SetArg(KernelName.EvaluateBackground, "ColourRs", BufferName.ColourRs);
			_computeManager.SetArg(KernelName.EvaluateBackground, "ColourGs", BufferName.ColourGs);
			_computeManager.SetArg(KernelName.EvaluateBackground, "ColourBs", BufferName.ColourBs);
			_computeManager.SetArg(KernelName.EvaluateBackground, "Weightings", BufferName.Weightings);
			_computeManager.SetArg(KernelName.EvaluateBackground, "FinalColourRs", BufferName.FinalColourRs);
			_computeManager.SetArg(KernelName.EvaluateBackground, "FinalColourGs", BufferName.FinalColourGs);
			_computeManager.SetArg(KernelName.EvaluateBackground, "FinalColourBs", BufferName.FinalColourBs);
			_computeManager.SetArg(KernelName.EvaluateBackground, "FinalWeightings", BufferName.FinalWeightings);

			return _computeManager.Enqueue(KernelName.EvaluateBackground, new[] { (nuint)backgroundQueueSize }, new[] { backgroundQueueEvent });
		}

		private CLEvent ResolveRemainders(CLEvent[] waitList)
		{
			var (remainderSize, resetRemainderEvent) = _computeManager.ResetIDBuffer(BufferName.MaterialQueueID, waitList);
			if (remainderSize == 0) return resetRemainderEvent;

			_computeManager.SetArg(KernelName.ResolveRemainders, "MaterialQueue", BufferName.MaterialQueue);
			_computeManager.SetArg(KernelName.ResolveRemainders, "FinalWeightings", BufferName.FinalWeightings);
			_computeManager.SetArg(KernelName.ResolveRemainders, "Weightings", BufferName.Weightings);
			_computeManager.SetArg(KernelName.ResolveRemainders, "ParentTraces", BufferName.ParentTraces);

			return _computeManager.Enqueue(KernelName.ResolveRemainders, new[] { (nuint)remainderSize }, new[] { resetRemainderEvent });
		}

		private CLEvent DrawTrace(CLEvent[] waitList)
		{
			_computeManager.SetArg(KernelName.DrawTrace, "FinalColourRs", BufferName.FinalColourRs);
			_computeManager.SetArg(KernelName.DrawTrace, "FinalColourGs", BufferName.FinalColourGs);
			_computeManager.SetArg(KernelName.DrawTrace, "FinalColourBs", BufferName.FinalColourBs);
			_computeManager.SetArg(KernelName.DrawTrace, "FinalWeightings", BufferName.FinalWeightings);

			var drawEvent = _computeManager.Enqueue(KernelName.DrawTrace, new[] { (nuint)Size.X, (nuint)Size.Y }, waitList);
			return drawEvent;
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

			InitScreenBuffers();
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
			RunKernels();

			// Display Scene
			GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FramebufferHandle.Zero);
			GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
			GL.BlitFramebuffer(0, 0, Size.X, Size.Y, 0, 0, Size.X, Size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

			SwapBuffers();
		}

		#endregion

		#region //Error

		private static GLDebugProc _debugProcCallback = DebugCallback;

		private void SetDebug()
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
