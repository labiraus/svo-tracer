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
		private const bool sine = false;

		#endregion

		#region //GLCL Objects
		private OpenTK.Compute.OpenCL.CLContext clContext;
		private CLCommandQueue commandQueue;
		private CLDevice device;

		private CLBuffer baseBlockBuffer;
		private CLBuffer blockBuffer;
		private CLBuffer usageBuffer;
		private CLBuffer childRequestIdBuffer;
		private CLBuffer childRequestBuffer;
		private CLBuffer parentSizeBuffer;
		private CLBuffer parentResidencyBuffer;
		private CLBuffer parentsBuffer;
		private CLBuffer dereferenceQueueBuffer;
		private CLBuffer dereferenceRemainingBuffer;
		private CLBuffer semaphorBuffer;
		private CLBuffer pruningBuffer;
		private CLBuffer pruningBlockDataBuffer;
		private CLBuffer pruningAddressesBuffer;
		private CLBuffer graftingBuffer;
		private CLBuffer graftingBlocksBuffer;
		private CLBuffer graftingAddressesBuffer;
		private CLBuffer holdingAddressesBuffer;
		private CLBuffer addressPositionBuffer;

		private CLKernel prune;
		private CLKernel graft;
		private CLKernel traceVoxel;
		private CLKernel sineWave;
		private CLKernel traceMesh;
		private CLKernel traceParticle;
		private CLKernel spawnRays;
		private CLKernel traceLight;
		private CLKernel resolveImage;
		private CLImage clRenderbuffer;
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
		}

		#endregion

		#region //Load
		protected override void OnLoad()
		{
			base.OnLoad();
			Stopwatch watch = new Stopwatch();
			watch.Start();
			setDebug();
			createContext();

			try
			{
				// Set up the buffers
				framebuffer = GL.CreateFramebuffer();
				GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);

				setupKernels();
				//setupSine();
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

		unsafe private void createContext()
		{
			CLResultCode resultCode;
			resultCode = CL.GetPlatformIDs(out CLPlatform[] platformIds);
			HandleResultCode(resultCode, "CL.GetPlatformIds");
			CLPlatform platform = new CLPlatform();
			foreach (var platformId in platformIds)
			{
				resultCode = platformId.SupportsPlatformExtension("cl_khr_gl_sharing", out bool supported);
				HandleResultCode(resultCode, "SupportsPlatformExtension");
				if (supported)
				{
					platform = platformId;
					break;
				}
			}
			resultCode = platform.GetDeviceIDs(DeviceType.Gpu, out CLDevice[] devices);
			HandleResultCode(resultCode, "GetDeviceIDs");
			foreach (var deviceId in devices)
			{
				resultCode = deviceId.GetDeviceInfo(DeviceInfo.Extensions, out byte[] bytes);
				HandleResultCode(resultCode, "GetDeviceInfo");
				var extensions = Encoding.ASCII.GetString(bytes).Split(" ");
				if (extensions.Any(x => x == "cl_khr_gl_sharing"))
				{
					device = deviceId;
					break;
				}
			}
			var props = new CLContextProperties(platform)
			{
				// if windows
				ContextInteropUserSync = true,
				GlContextKHR = GLFW.GetWGLContext(base.WindowPtr),
				WglHDCKHR = GLFW.GetWin32Window(base.WindowPtr)
			};
			// if linux
			//props.GlContextKHR = (IntPtr)GLFW.GetGLXContext(base.WindowPtr);
			//props.GlxDisplayKHR = (IntPtr)GLFW.GetX11Window(base.WindowPtr);

			clContext = props.CreateContextFromType(DeviceType.Gpu, null, IntPtr.Zero, out resultCode);
			HandleResultCode(resultCode, "CreateContextFromType");
			commandQueue = clContext.CreateCommandQueueWithProperties(device, new CLCommandQueueProperties(), out resultCode);
			HandleResultCode(resultCode, "CreateCommandQueueWithProperties");
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

				clRenderbuffer = clContext.CreateFromGLRenderbuffer(MemoryFlags.WriteOnly, (uint)glRenderbuffer.Handle, out CLResultCode resultCode);
				HandleResultCode(resultCode, "CreateFromGLRenderbuffer");
				resultCode = clRenderbuffer.RetainMemoryObject();
				HandleResultCode(resultCode, "RetainMemoryObject");
			}
		}

		#endregion

		#region //Data Processing

		private void setupSine()
		{
			var kernel = KernelLoader.Get("sine.cl");
			if (kernel == null)
			{
				throw new Exception("Could not find kernel 'sine.cl'");
			}
			CLProgram program = clContext.CreateProgramWithSource(kernel, out CLResultCode resultCode);
			HandleResultCode(resultCode, "CreateProgramWithSource");
			resultCode = program.BuildProgram(new[] { device }, null, null, IntPtr.Zero);
			if (resultCode == CLResultCode.BuildProgramFailure)
			{
				program.GetProgramBuildInfo(device, ProgramBuildInfo.Log, out byte[] bytes);
				Console.WriteLine(Encoding.ASCII.GetString(bytes));
			}
			HandleResultCode(resultCode, "BuildProgram");

			sineWave = program.CreateKernel("sine_wave", out resultCode);
			HandleResultCode(resultCode, "CreateKernel:sine_wave");
			resultCode = sineWave.SetKernelArg(1, (uint)this.Size.X);
			HandleResultCode(resultCode, "SetKernelArg:sineWave:width");
			resultCode = sineWave.SetKernelArg(2, (uint)this.Size.Y);
			HandleResultCode(resultCode, "SetKernelArg:sineWave:height");
		}

		private void runSine()
		{
			var resultCode = commandQueue.EnqueueAcquireGLObjects(new[] { clRenderbuffer.Handle }, null, out OpenTK.Compute.OpenCL.CLEvent acquireImage);
			HandleResultCode(resultCode, "EnqueueAcquireGLObjects");

			resultCode = sineWave.SetKernelArg(0, clRenderbuffer);
			HandleResultCode(resultCode, "SetKernelArg:traceVoxel:clRenderBuffer");
			resultCode = sineWave.SetKernelArg(3, tick / 100.0f);
			HandleResultCode(resultCode, "SetKernelArg:sineWave:time");

			resultCode = commandQueue.EnqueueNDRangeKernel(sineWave, 2, null, new[] { (nuint)Size.X, (nuint)Size.Y }, null, new[] { acquireImage }, out OpenTK.Compute.OpenCL.CLEvent kernelRun);
			HandleResultCode(resultCode, "EnqueueNDRangeKernel:sineWave");

			//var output = new float[16];
			//commandQueue.EnqueueReadImage(clRenderBuffer, true, new nuint[] { 0, 0, 0 }, new nuint[] { 4, 1, 1 }, 0, 0, output, new[] { kernelRun }, out _);
			//Console.WriteLine(output[1]);

			// Release 
			resultCode = commandQueue.EnqueueReleaseGLObjects(new[] { clRenderbuffer.Handle }, new[] { kernelRun }, out _);
			HandleResultCode(resultCode, "EnqueueReleaseGLObjects");
			resultCode = commandQueue.Flush();
			HandleResultCode(resultCode, "Flush");
		}

		private void setupKernels()
		{
			if (sine)
			{
				setupSine();
				return;
			}
			var kernel = KernelLoader.Get("kernel.cl");
			if (kernel == null)
			{
				throw new Exception("Could not find kernel 'kernel.cl'");
			}
			CLProgram program = clContext.CreateProgramWithSource(kernel, out CLResultCode resultCode);
			HandleResultCode(resultCode, "CreateProgramWithSource");
			resultCode = program.BuildProgram(new[] { device }, null, null, IntPtr.Zero);
			if (resultCode == CLResultCode.BuildProgramFailure)
			{
				program.GetProgramBuildInfo(device, ProgramBuildInfo.Log, out byte[] bytes);
				Console.WriteLine(Encoding.ASCII.GetString(bytes));
			}
			HandleResultCode(resultCode, "BuildProgram");
			// Set up the buffers
			initRenderBuffer();

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
			var octree = builder.LoadTree("test");
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
				MemorySize = blockCount,
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

			baseBlockBuffer = clContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, octree.BaseBlocks, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:baseBlockBuffer");
			blockBuffer = clContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, octree.Blocks, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:blockBuffer");
			usageBuffer = clContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new Usage[octree.BlockCount >> 3], out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:usageBuffer");
			childRequestIdBuffer = clContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new uint[1], out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:childRequestIdBuffer");
			childRequestBuffer = clContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new ChildRequest[_traceInput.MaxChildRequestId], out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:childRequestBuffer");
			parentSizeBuffer = clContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new uint[1], out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:parentSizeBuffer");
			parentResidencyBuffer = clContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, parentResidency, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:parentResidencyBuffer");
			parentsBuffer = clContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, parents, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:parentsBuffer");
			dereferenceQueueBuffer = clContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new ulong[octree.BlockCount], out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:dereferenceQueueBuffer");
			dereferenceRemainingBuffer = clContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new uint[1], out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:dereferenceRemainingBuffer");
			semaphorBuffer = clContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new int[1], out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:semaphorBuffer");

			prune = program.CreateKernel("prune", out resultCode);
			HandleResultCode(resultCode, "CreateKernel:prune");
			resultCode = prune.SetKernelArg(0, baseBlockBuffer);
			HandleResultCode(resultCode, "SetKernelArg:prune:baseBlockBuffer");
			resultCode = prune.SetKernelArg(1, blockBuffer);
			HandleResultCode(resultCode, "SetKernelArg:prune:blockBuffer");
			resultCode = prune.SetKernelArg(2, usageBuffer);
			HandleResultCode(resultCode, "SetKernelArg:prune:usageBuffer");
			resultCode = prune.SetKernelArg(3, childRequestIdBuffer);
			HandleResultCode(resultCode, "SetKernelArg:prune:childRequestIdBuffer");
			resultCode = prune.SetKernelArg(4, childRequestBuffer);
			HandleResultCode(resultCode, "SetKernelArg:prune:childRequestBuffer");
			resultCode = prune.SetKernelArg(5, parentSizeBuffer);
			HandleResultCode(resultCode, "SetKernelArg:prune:parentSizeBuffer");
			resultCode = prune.SetKernelArg(6, parentResidencyBuffer);
			HandleResultCode(resultCode, "SetKernelArg:prune:parentResidencyBuffer");
			resultCode = prune.SetKernelArg(7, parentsBuffer);
			HandleResultCode(resultCode, "SetKernelArg:prune:parentsBuffer");
			resultCode = prune.SetKernelArg(8, dereferenceQueueBuffer);
			HandleResultCode(resultCode, "SetKernelArg:prune:dereferenceQueueBuffer");
			resultCode = prune.SetKernelArg(9, dereferenceRemainingBuffer);
			HandleResultCode(resultCode, "SetKernelArg:prune:dereferenceRemainingBuffer");
			resultCode = prune.SetKernelArg(10, semaphorBuffer);
			HandleResultCode(resultCode, "SetKernelArg:prune:semaphorBuffer");

			graft = program.CreateKernel("graft", out resultCode);
			HandleResultCode(resultCode, "CreateKernel:graft");
			resultCode = graft.SetKernelArg(0, blockBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graft:blockBuffer");
			resultCode = graft.SetKernelArg(1, usageBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graft:usageBuffer");
			resultCode = graft.SetKernelArg(2, childRequestIdBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graft:childRequestIdBuffer");
			resultCode = graft.SetKernelArg(3, childRequestBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graft:childRequestBuffer");
			resultCode = graft.SetKernelArg(4, parentSizeBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graft:parentSizeBuffer");
			resultCode = graft.SetKernelArg(5, parentResidencyBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graft:parentResidencyBuffer");
			resultCode = graft.SetKernelArg(6, parentsBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graft:parentsBuffer");
			resultCode = graft.SetKernelArg(7, dereferenceQueueBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graft:dereferenceQueueBuffer");
			resultCode = graft.SetKernelArg(8, dereferenceRemainingBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graft:dereferenceRemainingBuffer");
			resultCode = graft.SetKernelArg(9, semaphorBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graft:semaphorBuffer");

			traceVoxel = program.CreateKernel("traceVoxel", out resultCode);
			HandleResultCode(resultCode, "CreateKernel:traceVoxel");
			resultCode = traceVoxel.SetKernelArg(0, baseBlockBuffer);
			HandleResultCode(resultCode, "SetKernelArg:traceVoxel:baseBlockBuffer");
			resultCode = traceVoxel.SetKernelArg(1, blockBuffer);
			HandleResultCode(resultCode, "SetKernelArg:traceVoxel:blockBuffer");
			resultCode = traceVoxel.SetKernelArg(2, usageBuffer);
			HandleResultCode(resultCode, "SetKernelArg:traceVoxel:usageBuffer");
			resultCode = traceVoxel.SetKernelArg(3, childRequestIdBuffer);
			HandleResultCode(resultCode, "SetKernelArg:traceVoxel:childRequestIdBuffer");
			resultCode = traceVoxel.SetKernelArg(4, childRequestBuffer);
			HandleResultCode(resultCode, "SetKernelArg:traceVoxel:childRequestBuffer");
			resultCode = traceVoxel.SetKernelArg(5, clRenderbuffer);
			HandleResultCode(resultCode, "SetKernelArg:traceVoxel:glImageBuffer");

			traceMesh = program.CreateKernel("traceMesh", out resultCode);
			HandleResultCode(resultCode, "CreateKernel:traceMesh");

			traceParticle = program.CreateKernel("traceParticle", out resultCode);
			HandleResultCode(resultCode, "CreateKernel:traceParticle");

			spawnRays = program.CreateKernel("spawnRays", out resultCode);
			HandleResultCode(resultCode, "CreateKernel:spawnRays");

			traceLight = program.CreateKernel("traceLight", out resultCode);
			HandleResultCode(resultCode, "CreateKernel:traceLight");

			resolveImage = program.CreateKernel("resolveImage", out resultCode);
			HandleResultCode(resultCode, "CreateKernel:resolveImage");

			this.KeyDown += MainWindow_KeyDown;
			this.KeyUp += MainWindow_KeyUp;
			this.MouseMove += MainWindow_MouseMove;

			previousMouseState = MouseState;
		}

		private void runKernels()
		{
			lock (_resizeLock)
			{
				if (sine)
				{
					runSine();
					return;
				}
				_traceInput.ScreenSize = base.Size;
				_traceInput.FoV[0] = (float)base.Size.X / (float)base.Size.Y * (float)Math.PI / 4.0f;
				_traceInput.Tick = tick;
				_updateInput.Tick = tick;
				//Flush child request buffer
				var resultCode = commandQueue.EnqueueWriteBuffer(childRequestIdBuffer, false, 0, new uint[] { 0 }, null, out _);
				HandleResultCode(resultCode, "EnqueueWriteBuffer:childRequestIdBuffer");

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

				pruningBuffer.ReleaseMemoryObject();
				pruningBlockDataBuffer.ReleaseMemoryObject();
				pruningAddressesBuffer.ReleaseMemoryObject();

				pruningArray = pruning.ToArray();
				pruningBlockDataArray = pruningBlockData.ToArray();
				pruningAddressesArray = pruningAddresses.ToArray();

				pruning = new List<Pruning>();
				pruningBlockData = new List<BlockData>();
				pruningAddresses = new List<Location>();
			}

			pruningBuffer = clContext.CreateBuffer(MemoryFlags.None, pruningArray, out CLResultCode resultCode);
			HandleResultCode(resultCode, "CreateBuffer:pruningBuffer");
			pruningBlockDataBuffer = clContext.CreateBuffer(MemoryFlags.None, pruningBlockDataArray, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:pruningBlockDataBuffer");
			pruningAddressesBuffer = clContext.CreateBuffer(MemoryFlags.None, pruningAddressesArray, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:pruningAddressesBuffer");

			var waitEvents = new OpenTK.Compute.OpenCL.CLEvent[3];
			resultCode = commandQueue.EnqueueWriteBuffer(dereferenceQueueBuffer, false, 0, new ulong[blockCount], null, out waitEvents[0]);
			HandleResultCode(resultCode, "EnqueueWriteBuffer:dereferenceQueueBuffer");
			resultCode = commandQueue.EnqueueWriteBuffer(dereferenceRemainingBuffer, false, 0, new uint[] { 0 }, null, out waitEvents[1]);
			HandleResultCode(resultCode, "EnqueueWriteBuffer:dereferenceRemainingBuffer");
			resultCode = commandQueue.EnqueueWriteBuffer(semaphorBuffer, false, 0, new int[] { 0 }, null, out waitEvents[2]);
			HandleResultCode(resultCode, "EnqueueWriteBuffer:semaphorBuffer");

			resultCode = prune.SetKernelArg(11, pruningBuffer);
			HandleResultCode(resultCode, "SetKernelArg:pruningBuffer");
			resultCode = prune.SetKernelArg(12, pruningBlockDataBuffer);
			HandleResultCode(resultCode, "SetKernelArg:pruningBlockDataBuffer");
			resultCode = prune.SetKernelArg(13, pruningAddressesBuffer);
			HandleResultCode(resultCode, "SetKernelArg:pruningAddressesBuffer");
			resultCode = prune.SetKernelArg(14, _updateInput);
			HandleResultCode(resultCode, "SetKernelArg:_updateInput");

			resultCode = commandQueue.EnqueueNDRangeKernel(prune, 1, null, new[] { (nuint)pruningArray.Length }, null, waitEvents, out _);
			HandleResultCode(resultCode, "EnqueueNDRangeKernel:prune");
			resultCode = commandQueue.Flush();
			HandleResultCode(resultCode, "Flush");
		}

		private void runGraft()
		{
			var graftingArray = Array.Empty<Grafting>();
			var graftingBlocksArray = Array.Empty<Block>();
			var graftingAddressesArray = Array.Empty<Location>();

			lock (_graftingBufferLock)
			{
				if (grafting.Count == 0) return;

				graftingBuffer.ReleaseMemoryObject();
				graftingBlocksBuffer.ReleaseMemoryObject();
				graftingAddressesBuffer.ReleaseMemoryObject();

				graftingArray = grafting.ToArray();
				graftingBlocksArray = graftingBlocks.ToArray();
				graftingAddressesArray = graftingAddresses.ToArray();

				grafting = new List<Grafting>();
				graftingBlocks = new List<Block>();
				graftingAddresses = new List<Location>();
			}

			_updateInput.GraftSize = (uint)graftingBlocksArray.Count();
			graftingBuffer = clContext.CreateBuffer(MemoryFlags.None, graftingArray, out CLResultCode resultCode);
			HandleResultCode(resultCode, "CreateBuffer:graftingBuffer");
			graftingBlocksBuffer = clContext.CreateBuffer(MemoryFlags.None, graftingBlocksArray, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:graftingBlocksBuffer");
			graftingAddressesBuffer = clContext.CreateBuffer(MemoryFlags.None, graftingAddressesArray, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:graftingAddressesBuffer");
			holdingAddressesBuffer = clContext.CreateBuffer(MemoryFlags.None, new uint[_updateInput.GraftSize], out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:holdingAddressesBuffer");
			addressPositionBuffer = clContext.CreateBuffer(MemoryFlags.None, new uint[] { 0 }, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:addressPositionBuffer");

			var waitEvents = new OpenTK.Compute.OpenCL.CLEvent[3];
			resultCode = commandQueue.EnqueueWriteBuffer(dereferenceQueueBuffer, false, 0, new ulong[blockCount], null, out waitEvents[0]);
			HandleResultCode(resultCode, "EnqueueWriteBuffer:dereferenceQueueBuffer");
			resultCode = commandQueue.EnqueueWriteBuffer(dereferenceRemainingBuffer, false, 0, new uint[] { 0 }, null, out waitEvents[1]);
			HandleResultCode(resultCode, "EnqueueWriteBuffer:dereferenceRemainingBuffer");
			resultCode = commandQueue.EnqueueWriteBuffer(semaphorBuffer, false, 0, new int[] { 0 }, null, out waitEvents[2]);
			HandleResultCode(resultCode, "EnqueueWriteBuffer:semaphorBuffer");

			resultCode = graft.SetKernelArg(10, graftingBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graftingBuffer");
			resultCode = graft.SetKernelArg(11, graftingBlocksBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graftingBlocksBuffer");
			resultCode = graft.SetKernelArg(12, graftingAddressesBuffer);
			HandleResultCode(resultCode, "SetKernelArg:graftingAddressesBuffer");
			resultCode = graft.SetKernelArg(13, holdingAddressesBuffer);
			HandleResultCode(resultCode, "SetKernelArg:holdingAddressesBuffer");
			resultCode = graft.SetKernelArg(14, addressPositionBuffer);
			HandleResultCode(resultCode, "SetKernelArg:addressPositionBuffer");
			resultCode = graft.SetKernelArg(15, _updateInput);
			HandleResultCode(resultCode, "SetKernelArg:_updateInput");

			resultCode = commandQueue.EnqueueNDRangeKernel(graft, 1, null, new[] { (nuint)graftingArray.Length }, null, waitEvents, out _);
			HandleResultCode(resultCode, "EnqueueNDRangeKernel:graft");
			resultCode = commandQueue.Flush();
			HandleResultCode(resultCode, "Flush");
		}

		private void runTrace()
		{
			var resultCode = commandQueue.EnqueueAcquireGLObjects(new[] { clRenderbuffer.Handle }, null, out OpenTK.Compute.OpenCL.CLEvent acquireImage);
			HandleResultCode(resultCode, "EnqueueAcquireGLObjects");


			resultCode = traceVoxel.SetKernelArg(5, clRenderbuffer);
			HandleResultCode(resultCode, "SetKernelArg:traceVoxel:clRenderBuffer");
			resultCode = traceVoxel.SetKernelArg(6, _traceInput);
			HandleResultCode(resultCode, "SetKernelArg:traceVoxel:_traceInput");

			resultCode = commandQueue.EnqueueNDRangeKernel(traceVoxel, 2, null, new[] { (nuint)Size.X, (nuint)Size.Y }, null, new[] { acquireImage }, out OpenTK.Compute.OpenCL.CLEvent kernelRun);
			HandleResultCode(resultCode, "EnqueueNDRangeKernel:traceVoxel");

			// Release 
			resultCode = commandQueue.EnqueueReleaseGLObjects(new[] { clRenderbuffer.Handle }, new[] { kernelRun }, out _);
			HandleResultCode(resultCode, "EnqueueReleaseGLObjects");
			resultCode = commandQueue.Flush();
			HandleResultCode(resultCode, "Flush");
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
				_traceInput.Facing.X -= (base.MouseState.X - previousMouseState.X) / 1000.0f;
				_traceInput.Facing.Y += (base.MouseState.Y - previousMouseState.Y) / 1000.0f;

				if (_traceInput.Facing.Y > Math.PI)
					_traceInput.Facing.Y = (float)Math.PI;
				else if (_traceInput.Facing.Y < -Math.PI)
					_traceInput.Facing.Y = -(float)Math.PI;
				if (_traceInput.Facing.X > Math.PI)
					_traceInput.Facing.X -= (float)Math.PI * 2;
				else if (_traceInput.Facing.X < -Math.PI)
					_traceInput.Facing.X += (float)Math.PI * 2;
			}
			previousMouseState = base.MouseState;

			if (base.KeyboardState.IsKeyDown(Keys.Space))
				_traceInput.Origin.Z -= 0.005f;
			if (base.KeyboardState.IsKeyDown(Keys.C))
				_traceInput.Origin.Z += 0.005f;
			if (base.KeyboardState.IsKeyDown(Keys.W) && !base.KeyboardState.IsKeyDown(Keys.S))
			{
				_traceInput.Origin.Y -= 0.005f;
			}
			if (base.KeyboardState.IsKeyDown(Keys.S) && !base.KeyboardState.IsKeyDown(Keys.W))
			{
				_traceInput.Origin.Y += 0.005f;
			}
			if (base.KeyboardState.IsKeyDown(Keys.D) && !base.KeyboardState.IsKeyDown(Keys.A))
			{
				_traceInput.Origin.X -= 0.005f;
			}
			if (base.KeyboardState.IsKeyDown(Keys.A) && !base.KeyboardState.IsKeyDown(Keys.D))
			{
				_traceInput.Origin.X += 0.005f;
			}
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
			traceVoxel.SetKernelArg(5, clRenderbuffer);
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
