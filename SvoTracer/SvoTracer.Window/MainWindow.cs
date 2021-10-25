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
		static readonly uint[] _indices = { 0, 1, 2, 0, 2, 3 };
		#endregion

		#region //GLCL Objects
		private OpenTK.Compute.OpenCL.CLContext cxGPUContext;
		private CLCommandQueue cqCommandQueue;

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
		private CLKernel traceMesh;
		private CLKernel traceParticle;
		private CLKernel spawnRays;
		private CLKernel traceLight;
		private CLKernel resolveImage;
		private CLImage glImageBuffer;
		private IntPtr[] computeMemory;
		private RenderbufferHandle renderBuffer = RenderbufferHandle.Zero;
		private FramebufferHandle frameBuffer = FramebufferHandle.Zero;
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

			var devices = createContext();

			try
			{
				var kernel = KernelLoader.Get("kernel.cl");
				if (kernel == null)
				{
					throw new Exception("Could not find kernel 'kernel.cl'");
				}
				CLProgram program = cxGPUContext.CreateProgramWithSource(kernel, out CLResultCode resultCode);
				HandleResultCode(resultCode, "CreateProgramWithSource");
				resultCode = program.BuildProgram(devices, null, OnError, IntPtr.Zero);
				if (resultCode == CLResultCode.BuildProgramFailure)
				{
					program.GetProgramBuildInfo(devices[0], ProgramBuildInfo.Log, out byte[] bytes);
					Console.WriteLine(Encoding.ASCII.GetString(bytes));
				}
				HandleResultCode(resultCode, "BuildProgram");
				// Set up the buffers
				initGLBuffers();

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

				baseBlockBuffer = cxGPUContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, octree.BaseBlocks, out resultCode);
				HandleResultCode(resultCode, "CreateBuffer:baseBlockBuffer");
				blockBuffer = cxGPUContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, octree.Blocks, out resultCode);
				HandleResultCode(resultCode, "CreateBuffer:blockBuffer");
				usageBuffer = cxGPUContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new Usage[octree.BlockCount >> 3], out resultCode);
				HandleResultCode(resultCode, "CreateBuffer:usageBuffer");
				childRequestIdBuffer = cxGPUContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new uint[1], out resultCode);
				HandleResultCode(resultCode, "CreateBuffer:childRequestIdBuffer");
				childRequestBuffer = cxGPUContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new ChildRequest[_traceInput.MaxChildRequestId], out resultCode);
				HandleResultCode(resultCode, "CreateBuffer:childRequestBuffer");
				parentSizeBuffer = cxGPUContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new uint[1], out resultCode);
				HandleResultCode(resultCode, "CreateBuffer:parentSizeBuffer");
				parentResidencyBuffer = cxGPUContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, parentResidency, out resultCode);
				HandleResultCode(resultCode, "CreateBuffer:parentResidencyBuffer");
				parentsBuffer = cxGPUContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, parents, out resultCode);
				HandleResultCode(resultCode, "CreateBuffer:parentsBuffer");
				dereferenceQueueBuffer = cxGPUContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new ulong[octree.BlockCount], out resultCode);
				HandleResultCode(resultCode, "CreateBuffer:dereferenceQueueBuffer");
				dereferenceRemainingBuffer = cxGPUContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new uint[1], out resultCode);
				HandleResultCode(resultCode, "CreateBuffer:dereferenceRemainingBuffer");
				semaphorBuffer = cxGPUContext.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, new int[1], out resultCode);
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
				resultCode = traceVoxel.SetKernelArg(5, glImageBuffer);
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

		unsafe private CLDevice[] createContext()
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
			CLDevice device = devices[0];
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

			cxGPUContext = props.CreateContextFromType(DeviceType.Gpu, null, IntPtr.Zero, out resultCode);
			HandleResultCode(resultCode, "CreateContextFromType");
			cqCommandQueue = cxGPUContext.CreateCommandQueueWithProperties(device, new CLCommandQueueProperties(), out resultCode);
			HandleResultCode(resultCode, "CreateCommandQueueWithProperties");
			return devices;
		}

		unsafe private void initGLBuffers()
		{
			// Remove buffers if they already exist
			List<BufferHandle> bufferHandles = new List<BufferHandle>();
			if (renderBuffer != RenderbufferHandle.Zero)
			{
				bufferHandles.Add(new BufferHandle(renderBuffer.Handle));
			}
			if (frameBuffer != FramebufferHandle.Zero)
			{
				bufferHandles.Add(new BufferHandle(frameBuffer.Handle));
			}
			if (bufferHandles.Any())
			{
				fixed (BufferHandle* bufferArray = bufferHandles.ToArray())
				{
					GL.DeleteBuffers(bufferHandles.Count, bufferArray);
				}
			}
			renderBuffer = GL.CreateRenderbuffer();
			frameBuffer = GL.CreateFramebuffer();

			// Initialize the frame buffer
			GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, frameBuffer);
			GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2d, new TextureHandle(renderBuffer.Handle), 0);

			// Initializes the render buffer
			GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderBuffer);
			GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Rgba16f, base.Size.X, base.Size.Y);
			GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, renderBuffer);

			this.glImageBuffer = cxGPUContext.CreateFromGLRenderbuffer(MemoryFlags.ReadWrite, (nuint)renderBuffer.Handle, out CLResultCode resultCode);
			HandleResultCode(resultCode, "CreateFromGLBuffer");

			//Release buffers
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBuffer);
			GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderBuffer);
		}

		#endregion

		#region //Data Processing
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
		#endregion

		#region //Change
		unsafe protected override void OnUnload()
		{
			base.OnUnload();

			List<BufferHandle> bufferHandles = new List<BufferHandle>();
			if (renderBuffer != RenderbufferHandle.Zero)
			{
				bufferHandles.Add(new BufferHandle(renderBuffer.Handle));
			}
			if (frameBuffer != FramebufferHandle.Zero)
			{
				bufferHandles.Add(new BufferHandle(frameBuffer.Handle));
			}
			if (bufferHandles.Any())
			{
				fixed (BufferHandle* bufferArray = bufferHandles.ToArray())
				{
					GL.DeleteBuffers(bufferHandles.Count, bufferArray);
				}
			}
		}

		protected override void OnResize(ResizeEventArgs e)
		{
			base.OnResize(e);
			//TODO - fix rendering
			GL.Viewport(0, 0, base.Size.X, base.Size.Y);
			//GL.MatrixMode(MatrixMode.Projection);
			float degreeRadian = ((float)Math.PI / 180f) * 50f;
			var projection = OpenTK.Mathematics.Matrix4.CreatePerspectiveFieldOfView(degreeRadian, base.Size.X / (float)base.Size.Y, 0.1f, 100.0f);
			//GL.LoadMatrix(ref projection);
			initGLBuffers();
			traceVoxel.SetKernelArg(5, this.glImageBuffer);
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

			GL.Viewport(0, 0, base.Size.X, base.Size.Y);
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, frameBuffer);
			GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, renderBuffer);

			GL.BlitFramebuffer(0, 0, base.Size.X, base.Size.Y, 0, 0, base.Size.X, base.Size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
			GL.BindTexture(TextureTarget.Texture2d, TextureHandle.Zero);
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferHandle.Zero);
			var err = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
			if (err != FramebufferStatus.FramebufferComplete)
				Console.WriteLine(err);

			if (tick < ushort.MaxValue - 2)
				tick += 1;
			else
				tick = 1;

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

			SwapBuffers();
		}

		private void runKernels()
		{
			GL.Finish();

			computeMemory = new IntPtr[] { this.glImageBuffer.Handle };
			var resultCode = cqCommandQueue.EnqueueAcquireGLObjects(computeMemory, null, out _);
			HandleResultCode(resultCode, "");


			_traceInput.ScreenSize = base.Size;
			_traceInput.FoV[0] = (float)base.Size.X / (float)base.Size.Y * (float)Math.PI / 4.0f;
			_traceInput.Tick = tick;
			_updateInput.Tick = tick;
			//Flush child request buffer
			resultCode = cqCommandQueue.EnqueueWriteBuffer(childRequestIdBuffer, false, 0, new uint[] { 0 }, null, out _);
			HandleResultCode(resultCode, "");

			runPrune();
			runGraft();

			resultCode = traceVoxel.SetKernelArg(6, _traceInput);
			HandleResultCode(resultCode, "SetKernelArg");
			resultCode = cqCommandQueue.EnqueueNDRangeKernel(traceVoxel, 2, null, new[] { (nuint)base.Size.X, (nuint)base.Size.Y }, null, null, out _);
			HandleResultCode(resultCode, "EnqueueNDRangeKernel");
			resultCode = cqCommandQueue.Flush();
			HandleResultCode(resultCode, "Flush");
			resultCode = cqCommandQueue.ReleaseCommandQueue();
			HandleResultCode(resultCode, "ReleaseCommandQueue");
			resultCode = cqCommandQueue.EnqueueReleaseGLObjects(computeMemory, null, out _);
			HandleResultCode(resultCode, "EnqueueReleaseGLObjects");
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

			pruningBuffer = cxGPUContext.CreateBuffer(MemoryFlags.None, pruningArray, out CLResultCode resultCode);
			HandleResultCode(resultCode, "CreateBuffer:pruningBuffer");
			pruningBlockDataBuffer = cxGPUContext.CreateBuffer(MemoryFlags.None, pruningBlockDataArray, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:pruningBlockDataBuffer");
			pruningAddressesBuffer = cxGPUContext.CreateBuffer(MemoryFlags.None, pruningAddressesArray, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:pruningAddressesBuffer");

			var waitEvents = new OpenTK.Compute.OpenCL.CLEvent[3];
			cqCommandQueue.EnqueueWriteBuffer(dereferenceQueueBuffer, false, 0, new ulong[blockCount], null, out waitEvents[0]);
			cqCommandQueue.EnqueueWriteBuffer(dereferenceRemainingBuffer, false, 0, new uint[] { 0 }, null, out waitEvents[1]);
			cqCommandQueue.EnqueueWriteBuffer(semaphorBuffer, false, 0, new int[] { 0 }, null, out waitEvents[2]);

			prune.SetKernelArg(11, pruningBuffer);
			prune.SetKernelArg(12, pruningBlockDataBuffer);
			prune.SetKernelArg(13, pruningAddressesBuffer);
			prune.SetKernelArg(14, _updateInput);

			cqCommandQueue.EnqueueNDRangeKernel(prune, 1, null, new[] { (nuint)pruningArray.Length }, null, waitEvents, out _);
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
			graftingBuffer = cxGPUContext.CreateBuffer(MemoryFlags.None, graftingArray, out CLResultCode resultCode);
			HandleResultCode(resultCode, "CreateBuffer:graftingBuffer");
			graftingBlocksBuffer = cxGPUContext.CreateBuffer(MemoryFlags.None, graftingBlocksArray, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:graftingBlocksBuffer");
			graftingAddressesBuffer = cxGPUContext.CreateBuffer(MemoryFlags.None, graftingAddressesArray, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:graftingAddressesBuffer");
			holdingAddressesBuffer = cxGPUContext.CreateBuffer(MemoryFlags.None, new uint[_updateInput.GraftSize], out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:holdingAddressesBuffer");
			addressPositionBuffer = cxGPUContext.CreateBuffer(MemoryFlags.None, new uint[] { 0 }, out resultCode);
			HandleResultCode(resultCode, "CreateBuffer:addressPositionBuffer");

			var waitEvents = new OpenTK.Compute.OpenCL.CLEvent[3];
			cqCommandQueue.EnqueueWriteBuffer(dereferenceQueueBuffer, false, 0, new ulong[blockCount], null, out waitEvents[0]);
			cqCommandQueue.EnqueueWriteBuffer(dereferenceRemainingBuffer, false, 0, new uint[] { 0 }, null, out waitEvents[1]);
			cqCommandQueue.EnqueueWriteBuffer(semaphorBuffer, false, 0, new int[] { 0 }, null, out waitEvents[2]);

			graft.SetKernelArg(10, graftingBuffer);
			graft.SetKernelArg(11, graftingBlocksBuffer);
			graft.SetKernelArg(12, graftingAddressesBuffer);
			graft.SetKernelArg(13, holdingAddressesBuffer);
			graft.SetKernelArg(14, addressPositionBuffer);
			graft.SetKernelArg(15, _updateInput);

			cqCommandQueue.EnqueueNDRangeKernel(graft, 1, null, new[] { (nuint)graftingArray.Length }, null, waitEvents, out _);
		}
		#endregion

		#region //Error
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
