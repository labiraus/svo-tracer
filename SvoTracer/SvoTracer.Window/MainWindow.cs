using SvoTracer.Kernel;
using SvoTracer.Domain;
using SvoTracer.Domain.Models;
using System;
using System.Linq;
using System.Numerics;
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
		private readonly StateManager _stateManager = new();
		private readonly ITreeBuilder _treeBuilder;
		private readonly ITreeManager _treeManager;
		private RenderbufferHandle glRenderbuffer = RenderbufferHandle.Zero;
		private FramebufferHandle framebuffer = FramebufferHandle.Zero;
		#endregion

		#region //Constructor
		public MainWindow(int width, int height, string title, ITreeBuilder treeBuilder, ITreeManager treeManager)
			: base(GameWindowSettings.Default, new NativeWindowSettings()
			{
				Title = title,
				Size = new OpenTK.Mathematics.Vector2i(width, height)
			})
		{
			_computeManager = buildComputeManager();
			_treeBuilder = treeBuilder;
			_treeManager = treeManager;
		}

		unsafe private ComputeManager buildComputeManager()
		{
			return ComputeManagerFactory.Build(GLFW.GetWGLContext(WindowPtr), GLFW.GetWin32Window(base.WindowPtr), new[] { "kernel.cl" });
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
				initRenderBuffer();

				if (!_treeManager.TreeExists("test"))
					_treeManager.SaveTree("test", _treeBuilder.BuildTree(5, 7, uint.MaxValue / 64));
				setupKernels(_treeManager.LoadTree("test"));
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

		private void initRenderBuffer()
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

		private void setupKernels(Octree octree)
		{
			_stateManager.TraceInput.N = octree.N;
			_stateManager.UpdateInput.N = octree.N;
			_stateManager.UpdateInput.MemorySize = octree.BlockCount;
			blockCount = octree.BlockCount;
			parentMaxSize = 6000;

			// Usage contains one element for every block for recording when it was last used
			var usage = new Usage[octree.BlockCount];
			var baseStart = (int)TreeBuilder.PowSum((byte)(octree.N - 1)) << 3;
			var range = TreeBuilder.PowSum(octree.N) << 3;
			//This iterates over the N+1 level and makes blocks inviolate
			for (int i = baseStart; i < range; i++)
			{
				if ((octree.BaseBlocks[i >> 3] >> ((i & 7) * 2) & 3) != 3)
					continue;
				usage[i].Tick = ushort.MaxValue;
				usage[i].Parent = uint.MaxValue;
			}

			_computeManager.InitBuffer(BufferName.BaseBlocks, octree.BaseBlocks);
			_computeManager.InitBuffer(BufferName.Blocks, octree.Blocks.Serialize());
			_computeManager.InitBuffer(BufferName.Usage, usage.Serialize());
			_computeManager.InitBuffer(BufferName.ChildRequestId, new uint[1]);
			_computeManager.InitBuffer(BufferName.ChildRequests, new byte[_stateManager.TraceInput.MaxChildRequestId * ChildRequest.Size]);
			_computeManager.InitBuffer(BufferName.ParentSize, new uint[1]);
			_computeManager.InitBuffer(BufferName.ParentResidency, new bool[parentMaxSize]);
			_computeManager.InitBuffer(BufferName.Parents, new byte[parentMaxSize * Parent.Size]);
			_computeManager.InitBuffer(BufferName.DereferenceQueue, new ulong[octree.BlockCount]);
			_computeManager.InitBuffer(BufferName.DereferenceRemaining, new uint[1]);
			_computeManager.InitBuffer(BufferName.Semaphor, new uint[1]);

			_computeManager.SetArg(KernelName.Prune, "bases", BufferName.BaseBlocks);
			_computeManager.SetArg(KernelName.Prune, "blocks", BufferName.Blocks);
			_computeManager.SetArg(KernelName.Prune, "usage", BufferName.Usage);
			_computeManager.SetArg(KernelName.Prune, "childRequestId", BufferName.ChildRequestId);
			_computeManager.SetArg(KernelName.Prune, "childRequests", BufferName.ChildRequests);
			_computeManager.SetArg(KernelName.Prune, "parentSize", BufferName.ParentSize);
			_computeManager.SetArg(KernelName.Prune, "parentResidency", BufferName.ParentResidency);
			_computeManager.SetArg(KernelName.Prune, "parents", BufferName.Parents);
			_computeManager.SetArg(KernelName.Prune, "dereferenceQueue", BufferName.DereferenceQueue);
			_computeManager.SetArg(KernelName.Prune, "dereferenceRemaining", BufferName.DereferenceRemaining);
			_computeManager.SetArg(KernelName.Prune, "semaphor", BufferName.Semaphor);

			_computeManager.SetArg(KernelName.Graft, "blocks", BufferName.Blocks);
			_computeManager.SetArg(KernelName.Graft, "usage", BufferName.Usage);
			_computeManager.SetArg(KernelName.Graft, "childRequestId", BufferName.ChildRequestId);
			_computeManager.SetArg(KernelName.Graft, "childRequests", BufferName.ChildRequests);
			_computeManager.SetArg(KernelName.Graft, "parentSize", BufferName.ParentSize);
			_computeManager.SetArg(KernelName.Graft, "parentResidency", BufferName.ParentResidency);
			_computeManager.SetArg(KernelName.Graft, "parents", BufferName.Parents);
			_computeManager.SetArg(KernelName.Graft, "dereferenceQueue", BufferName.DereferenceQueue);
			_computeManager.SetArg(KernelName.Graft, "dereferenceRemaining", BufferName.DereferenceRemaining);
			_computeManager.SetArg(KernelName.Graft, "semaphor", BufferName.Semaphor);

			_computeManager.SetArg(KernelName.TraceVoxel, "bases", BufferName.BaseBlocks);
			_computeManager.SetArg(KernelName.TraceVoxel, "blocks", BufferName.Blocks);
			_computeManager.SetArg(KernelName.TraceVoxel, "usage", BufferName.Usage);
			_computeManager.SetArg(KernelName.TraceVoxel, "childRequestId", BufferName.ChildRequestId);
			_computeManager.SetArg(KernelName.TraceVoxel, "childRequests", BufferName.ChildRequests);
		}

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
			_computeManager.SetArg(KernelName.Prune, "pruningBlockData", BufferName.BaseBlocks);

			_computeManager.InitBuffer(BufferName.PruningAddresses, pruningData.PruningAddresses.Serialize());
			_computeManager.SetArg(KernelName.Prune, "pruningAddresses", BufferName.BaseBlocks);
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
			_computeManager.SetArg(KernelName.Graft, "graftingBlocks", BufferName.BaseBlocks);

			_computeManager.InitBuffer(BufferName.GraftingAddresses, graftingData.GraftingAddresses.Serialize());
			_computeManager.SetArg(KernelName.Graft, "graftingAddresses", BufferName.BaseBlocks);

			_computeManager.InitBuffer(BufferName.HoldingAddresses, new uint[_stateManager.UpdateInput.GraftSize]);
			_computeManager.SetArg(KernelName.Graft, "holdingAddresses", BufferName.BaseBlocks);

			_computeManager.InitBuffer(BufferName.AddressPosition, new uint[] { 0 });
			_computeManager.SetArg(KernelName.Graft, "addressPosition", BufferName.BaseBlocks);
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
			//Flush child request buffer
			_computeManager.WriteBuffer(BufferName.ChildRequestId, new uint[] { 0 });
			var (renderbuffer, waitEvent) = _computeManager.AcquireRenderbuffer();
			_computeManager.SetArg(KernelName.TraceVoxel, "outputImage", renderbuffer);
			_computeManager.SetArg(KernelName.TraceVoxel, "_input", _stateManager.TraceInput.Serialize());

			var kernelRun = _computeManager.Enqueue(KernelName.TraceVoxel, new[] { (nuint)Size.X, (nuint)Size.Y }, new[] { waitEvent });
			_computeManager.ReleaseRenderbuffer(new[] { kernelRun });
			_computeManager.Flush();
		}
		#endregion

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

			initRenderBuffer();
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
			GL.Viewport(0, 0, Size.X, Size.Y);
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FramebufferHandle.Zero);
			GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
			GL.BlitFramebuffer(0, 0, Size.X, Size.Y, 0, 0, Size.X, Size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

			SwapBuffers();
		}

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
