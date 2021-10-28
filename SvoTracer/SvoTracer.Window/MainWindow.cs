using SvoTracer.Kernel;
using SvoTracer.Domain;
using SvoTracer.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Compute.OpenCL;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using OpenTK.Windowing.Common;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;

namespace SvoTracer.Window
{
	class MainWindow : GameWindow
	{
		#region //Local Variables
		private ushort tick = 1;
		private uint parentMaxSize = 6000;
		private uint blockCount = 0;
		private bool initialized = false;
		MouseState previousMouseState;
		private uint[] parentSize = new uint[] { 0 };
		private TraceInputData _traceInput;
		private UpdateInputData _updateInput;

		private readonly object _pruningBufferLock = new();
		private List<Pruning> pruning = new();
		private List<BlockData> pruningBlockData = new();
		private List<Location> pruningAddresses = new();

		private readonly object _graftingBufferLock = new();
		private List<Grafting> grafting = new();
		private List<Block> graftingBlocks = new();
		private List<Location> graftingAddresses = new();

		private readonly object _resizeLock = new();

		private ComputeManager _computeManager;
		private RenderbufferHandle glRenderbuffer = RenderbufferHandle.Zero;
		private FramebufferHandle framebuffer = FramebufferHandle.Zero;
		#endregion

		#region //Constructor
		public MainWindow(int width, int height, string title)
			: base(GameWindowSettings.Default, new NativeWindowSettings()
			{
				Title = title,
				Size = new OpenTK.Mathematics.Vector2i(width, height)
			})
		{
			_computeManager = buildComputeManager();
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
				setupKernels();
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

		unsafe private void initRenderBuffer()
		{
			lock (_resizeLock)
			{
				// Remove buffers if they already exist
				if (glRenderbuffer != RenderbufferHandle.Zero)
					fixed (BufferHandle* bufferArray = new[] { new BufferHandle(glRenderbuffer.Handle) })
						GL.DeleteBuffers(1, bufferArray);

				// Initializes the render buffer
				glRenderbuffer = GL.CreateRenderbuffer();
				GL.NamedRenderbufferStorage(glRenderbuffer, InternalFormat.Rgba32f, Size.X, Size.Y);
				GL.NamedFramebufferRenderbuffer(framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, glRenderbuffer);
				var err = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
				if (err != FramebufferStatus.FramebufferComplete)
					Console.WriteLine(err);
				GL.Flush();
				_computeManager.InitRenderbuffer((uint)glRenderbuffer.Handle);
			}
		}

		#endregion

		#region //Data Processing

		private void setupKernels()
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
			//new Vector3(0.5f, 0.5f, 0.2f),
			//new Vector3(0.6f, 0.3f, 0.4f),
			//new Vector3(0.6f, 0.7f, 0.4f),
			//new Vector3(0.7f, 0.5f, 0.4f),
			//new Vector3(0.4f, 0.3f, 0.6f),
			//new Vector3(0.4f, 0.7f, 0.6f),
			//new Vector3(0.3f, 0.5f, 0.6f),
			//new Vector3(0.5f, 0.5f, 0.8f));
			if (!builder.TreeExists("test"))
			{
				builder.SaveTree("test", 5, 7, uint.MaxValue / 64);
			}
			var octree = TreeBuilder.LoadTree("test");
			blockCount = octree.BlockCount;

			_traceInput = new TraceInputData(
				new Vector3(0.5f, 0.5f, -2f),
				new Vector3(0, (float)Math.PI / 2f, 0),
				new Vector2((float)Math.PI / 4f, (float)Math.PI / 4f),
				new Vector2(0, 0.169f),
				base.Bounds.Size.X,
				base.Bounds.Size.Y,
				200,
				octree.N,
				0,
				6000);

			_updateInput = new UpdateInputData
			{
				N = octree.N,
				MaxChildRequestId = 6000,
				MemorySize = octree.BlockCount,
				Offset = uint.MaxValue / 4,
			};
			parentMaxSize = 6000;

			var usage = new Usage[octree.BlockCount >> 3];
			var baseStart = TreeBuilder.PowSum((byte)(_traceInput.N - 1));
			var range = TreeBuilder.PowSum(_traceInput.N) << 3;
			//This iterates over the N+1 level
			for (int i = 0; i < range; i++)
			{
				if ((octree.BaseBlocks[baseStart + (i >> 3)] >> ((i & 7) * 2) & 3) != 3)
					break;
				usage[i].Count = ushort.MaxValue;
				usage[i].Parent = uint.MaxValue;
			}

			bool[] parentResidency = new bool[parentMaxSize];
			Parent[] parents = new Parent[parentMaxSize];
			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			foreach (var block in octree.Blocks) block.Serialize(writer);
			_computeManager.InitBuffer(BufferName.BaseBlocks, octree.BaseBlocks);
			_computeManager.InitBuffer(BufferName.Blocks, ms.ToArray());
			_computeManager.InitBuffer(BufferName.Usage, new Usage[octree.BlockCount >> 3]);
			_computeManager.InitBuffer(BufferName.ChildRequestId, new uint[1]);
			_computeManager.InitBuffer(BufferName.ChildRequests, new ChildRequest[_traceInput.MaxChildRequestId]);
			_computeManager.InitBuffer(BufferName.ParentSize, new uint[1]);
			_computeManager.InitBuffer(BufferName.ParentResidency, parentResidency);
			_computeManager.InitBuffer(BufferName.Parents, parents);
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

			this.KeyDown += MainWindow_KeyDown;
			this.KeyUp += MainWindow_KeyUp;
			this.MouseMove += MainWindow_MouseMove;

			previousMouseState = MouseState;
		}

		private void runKernels()
		{
			lock (_resizeLock)
			{
				_traceInput.ScreenSize = Size;
				var fov = _traceInput.FoV;
				fov[0] = (float)Size.X / (float)Size.Y * (float)Math.PI / 4.0f;
				_traceInput.FoV = fov;
				_traceInput.Tick = tick;
				_updateInput.Tick = tick;
				//Flush child request buffer
				_computeManager.WriteBuffer(BufferName.ChildRequestId, new uint[] { 0 });

				runPrune();
				runGraft();
				runTrace();
			}
		}

		private void runPrune()
		{
			var pruningArray = Array.Empty<Pruning>();
			var pruningBlockDataArray = Array.Empty<BlockData>();
			var pruningAddressesArray = Array.Empty<Location>();

			lock (_pruningBufferLock)
			{
				if (pruning.Count == 0) return;

				pruningArray = pruning.ToArray();
				pruningBlockDataArray = pruningBlockData.ToArray();
				pruningAddressesArray = pruningAddresses.ToArray();

				pruning = new List<Pruning>();
				pruningBlockData = new List<BlockData>();
				pruningAddresses = new List<Location>();
			}

			_computeManager.InitBuffer(BufferName.Pruning, pruningArray);
			_computeManager.SetArg(KernelName.Prune, "pruning", BufferName.Pruning);

			_computeManager.InitBuffer(BufferName.PruningBlockData, pruningBlockDataArray);
			_computeManager.SetArg(KernelName.Prune, "pruningBlockData", BufferName.BaseBlocks);

			_computeManager.InitBuffer(BufferName.PruningAddresses, pruningAddressesArray);
			_computeManager.SetArg(KernelName.Prune, "pruningAddresses", BufferName.BaseBlocks);
			_computeManager.SetArg(KernelName.Prune, "inputData", _updateInput);

			var waitEvents = new List<OpenTK.Compute.OpenCL.CLEvent>();
			waitEvents.Add(_computeManager.WriteBuffer(BufferName.DereferenceQueue, new ulong[blockCount], null));
			waitEvents.Add(_computeManager.WriteBuffer(BufferName.DereferenceRemaining, new uint[] { 0 }, null));
			waitEvents.Add(_computeManager.WriteBuffer(BufferName.Semaphor, new int[] { 0 }, null));

			_computeManager.Enqueue(KernelName.Prune, new[] { (nuint)pruningArray.Length }, waitEvents.ToArray());
			_computeManager.Flush();
		}

		private void runGraft()
		{
			var graftingArray = Array.Empty<Grafting>();
			var graftingBlocksArray = Array.Empty<Block>();
			var graftingAddressesArray = Array.Empty<Location>();

			lock (_graftingBufferLock)
			{
				if (grafting.Count == 0) return;

				graftingArray = grafting.ToArray();
				graftingBlocksArray = graftingBlocks.ToArray();
				graftingAddressesArray = graftingAddresses.ToArray();

				grafting = new List<Grafting>();
				graftingBlocks = new List<Block>();
				graftingAddresses = new List<Location>();
			}

			_updateInput.GraftSize = (uint)graftingBlocksArray.Count();
			_computeManager.InitBuffer(BufferName.Grafting, graftingArray);
			_computeManager.SetArg(KernelName.Graft, "grafting", BufferName.Pruning);

			_computeManager.InitBuffer(BufferName.GraftingBlocks, graftingBlocksArray);
			_computeManager.SetArg(KernelName.Graft, "graftingBlocks", BufferName.BaseBlocks);

			_computeManager.InitBuffer(BufferName.GraftingAddresses, graftingAddressesArray);
			_computeManager.SetArg(KernelName.Graft, "graftingAddresses", BufferName.BaseBlocks);

			_computeManager.InitBuffer(BufferName.HoldingAddresses, new uint[_updateInput.GraftSize]);
			_computeManager.SetArg(KernelName.Graft, "holdingAddresses", BufferName.BaseBlocks);

			_computeManager.InitBuffer(BufferName.AddressPosition, new uint[] { 0 });
			_computeManager.SetArg(KernelName.Graft, "addressPosition", BufferName.BaseBlocks);
			_computeManager.SetArg(KernelName.Graft, "inputData", _updateInput);

			var waitEvents = new List<OpenTK.Compute.OpenCL.CLEvent>();
			waitEvents.Add(_computeManager.WriteBuffer(BufferName.DereferenceQueue, new ulong[blockCount], null));
			waitEvents.Add(_computeManager.WriteBuffer(BufferName.DereferenceRemaining, new uint[] { 0 }, null));
			waitEvents.Add(_computeManager.WriteBuffer(BufferName.Semaphor, new int[] { 0 }, null));

			_computeManager.Enqueue(KernelName.Graft, new[] { (nuint)graftingArray.Length }, waitEvents.ToArray());
			_computeManager.Flush();
		}

		private void runTrace()
		{
			var renderBuffer = _computeManager.AcquireRenderbuffer();
			_computeManager.SetArg(KernelName.TraceVoxel, "outputImage", renderBuffer.buffer);
			_computeManager.SetArg(KernelName.TraceVoxel, "_input", _traceInput);

			var kernelRun = _computeManager.Enqueue(KernelName.TraceVoxel, new[] { (nuint)Size.X, (nuint)Size.Y }, new[] { renderBuffer.waitEvent });
			_computeManager.ReleaseRenderbuffer(new[] { kernelRun });
			_computeManager.Flush();
		}

		public void UpdatePruning(Pruning pruningInput, BlockData? pruningBlockDataInput, Location? pruningAddressInput)
		{
			lock (_pruningBufferLock)
			{
				if (pruningBlockDataInput != null)
				{
					pruningInput.ColourAddress = (uint)pruningBlockData.Count;
					pruningBlockData.Add(pruningBlockDataInput.Value);
				}
				if (pruningAddressInput != null)
				{
					pruningInput.Address = (uint)pruningAddresses.Count;
					pruningAddresses.Add(pruningAddressInput.Value);
				}
				pruning.Add(pruningInput);
			}
		}

		public void UpdateGrafting(Grafting graftingInput, List<Block> graftingBlockInput, Location? graftingAddressInput)
		{
			lock (_graftingBufferLock)
			{
				graftingInput.GraftDataAddress = (uint)graftingBlocks.Count;
				graftingInput.GraftTotalSize = (uint)graftingBlockInput.Count;
				for (int i = 0; i < graftingBlockInput.Count; i++)
				{
					var graftingBlock = graftingBlockInput[i];
					graftingBlock.Child += graftingInput.GraftDataAddress;
					graftingBlocks.Add(graftingBlock);
				}
				if (graftingAddressInput != null)
				{
					graftingInput.GraftAddress = (uint)graftingAddresses.Count;
					graftingAddresses.Add(graftingAddressInput.Value);
				}
				grafting.Add(graftingInput);
			}
		}
		#endregion

		#region //Input
		private void MainWindow_MouseMove(MouseMoveEventArgs e)
		{
		}

		private void MainWindow_KeyUp(KeyboardKeyEventArgs e)
		{
		}

		private void MainWindow_KeyDown(KeyboardKeyEventArgs e)
		{
		}

		private void readInput()
		{
			if (base.MouseState.IsButtonDown(MouseButton.Left) && previousMouseState.IsButtonDown(MouseButton.Left))
			{
				var facing = _traceInput.Facing;
				facing.X -= (base.MouseState.X - previousMouseState.X) / 1000.0f;
				facing.Y += (base.MouseState.Y - previousMouseState.Y) / 1000.0f;

				if (facing.Y > Math.PI)
					facing.Y = (float)Math.PI;
				else if (facing.Y < -Math.PI)
					facing.Y = -(float)Math.PI;
				if (facing.X > Math.PI)
					facing.X -= (float)Math.PI * 2;
				else if (facing.X < -Math.PI)
					facing.X += (float)Math.PI * 2;
				_traceInput.Facing = facing;
			}
			previousMouseState = base.MouseState;
			var origin = _traceInput.Origin;
			if (base.KeyboardState.IsKeyDown(Keys.Space))
				origin.Z -= 0.005f;
			if (base.KeyboardState.IsKeyDown(Keys.C))
				origin.Z += 0.005f;
			if (base.KeyboardState.IsKeyDown(Keys.W) && !base.KeyboardState.IsKeyDown(Keys.S))
				origin.Y -= 0.005f;
			if (base.KeyboardState.IsKeyDown(Keys.S) && !base.KeyboardState.IsKeyDown(Keys.W))
				origin.Y += 0.005f;
			if (base.KeyboardState.IsKeyDown(Keys.D) && !base.KeyboardState.IsKeyDown(Keys.A))
				origin.X -= 0.005f;
			if (base.KeyboardState.IsKeyDown(Keys.A) && !base.KeyboardState.IsKeyDown(Keys.D))
				origin.X += 0.005f;
			_traceInput.Origin = origin;
		}
		#endregion

		#region //Change
		unsafe protected override void OnUnload()
		{
			base.OnUnload();
			var bufferHandles = new[] { new BufferHandle(framebuffer.Handle) };
			fixed (BufferHandle* bufferArray = bufferHandles.ToArray())
			{
				GL.DeleteBuffers(1, bufferArray);
			}
		}

		protected override void OnResize(ResizeEventArgs e)
		{
			base.OnResize(e);
			initRenderBuffer();
		}
		#endregion

		#region //Update
		protected override void OnUpdateFrame(FrameEventArgs e)
		{
			base.OnUpdateFrame(e);

			if (KeyboardState.IsKeyDown(Keys.Escape))
				base.Close();
		}
		#endregion

		#region //Render
		protected override void OnRenderFrame(FrameEventArgs e)
		{
			base.OnRenderFrame(e);

			if (!initialized)
			{
				return;
			}

			runKernels();
			GL.Viewport(0, 0, Size.X, Size.Y);
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FramebufferHandle.Zero);
			GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
			GL.BlitFramebuffer(0, 0, Size.X, Size.Y, 0, 0, Size.X, Size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

			SwapBuffers();
			if (tick < ushort.MaxValue - 2)
				tick += 1;
			else
				tick = 1;

			readInput();
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
