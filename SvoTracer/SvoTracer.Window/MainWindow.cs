using SvoTracer.Kernel;
using SvoTracer.Domain;
using SvoTracer.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Compute;
using OpenTK.Compute.OpenCL;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using System.Runtime.InteropServices;
using OpenTK.Windowing.Common;

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

        private object pruningBufferLock = new object();
        private List<Pruning> pruning = new List<Pruning>();
        private List<BlockData> pruningBlockData = new List<BlockData>();
        private List<Location> pruningAddresses = new List<Location>();

        private object graftingBufferLock = new object();
        private List<Grafting> grafting = new List<Grafting>();
        private List<Block> graftingBlocks = new List<Block>();
        private List<Location> graftingAddresses = new List<Location>();
        #endregion

        #region //GLCL Objects
        private CLContext cxGPUContext;
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
        private CLBuffer mColors;
        private CLBuffer[] computeMemory;
        private uint renderBuffer;
        private uint frameBuffer;
        #endregion

        #region //Constructor
        public MainWindow(int width, int height, string title)
            : base(GameWindowSettings.Default, new NativeWindowSettings()
            {
                Title = title,
                Size = new OpenTK.Mathematics.Vector2i(width, height)
            }) //1000, 1000, new OpenTK.Graphics.GraphicsMode(32, 16, 0, samples))
        {
        }

        #endregion

        #region //Load
        protected override void OnLoad()
        {
            base.OnLoad();
            Stopwatch watch = new Stopwatch();
            watch.Start();
            CLResultCode resultCode;

            #region Creating OpenGL compatible Context
            resultCode = CL.GetPlatformIds(out CLPlatform[] platformIds);
            HandleResultCode(resultCode, "CL.GetPlatformIds");
            CLPlatform platform = new CLPlatform();
            foreach (var platformId in platformIds)
            {
                resultCode = CL.SupportsPlatformExtension(platformId, "cl_khr_gl_sharing", out bool supported);
                HandleResultCode(resultCode, "CL.GetPlatformInfo");
                if (supported)
                {
                    platform = platformId;
                    break;
                }
            }
            resultCode = CL.GetDeviceIds(platform, DeviceType.Gpu, out CLDevice[] devices);
            CLDevice device = devices[0];
            HandleResultCode(resultCode, "CL.GetDeviceIds");
            var props = new CLContextProperties()
            {
                GlContextKHR = base.Context.WindowPtr, // CurrentContext
                WglHDCKHR = base.CurrentMonitor.Pointer, // DisplayContext???
                ContextPlatform = platform,
            };

            cxGPUContext = CL.CreateContextFromType(props, DeviceType.Gpu, null, IntPtr.Zero, out resultCode);
            HandleResultCode(resultCode, "CL.CreateContextFromType");
            #endregion

            cqCommandQueue = CL.CreateCommandQueueWithProperties(cxGPUContext, device, CommandQueueProperty.None, out resultCode);
            try
            {
                //CLProgram program = CL.CreateProgramWithSource(cxGPUContext, Kernel.Get("test.cl"), out resultCode);
                var kernel = KernelLoader.Get("kernel.cl");
                if (kernel == null){
                    throw new Exception("Could not find kernel 'kernel.cl'");
				}
                CLProgram program = CL.CreateProgramWithSource(cxGPUContext, kernel, out resultCode);
                CL.BuildProgram(program, devices, "", OnError, IntPtr.Zero);
                // Set up the buffers
                initVBO();

                var builder = new CubeBuilder(
                    new Vector3(0.5f, 0.5f, 0.2f),
                    new Vector3(0.6f, 0.3f, 0.4f),
                    new Vector3(0.6f, 0.7f, 0.4f),
                    new Vector3(0.7f, 0.5f, 0.4f),
                    new Vector3(0.4f, 0.3f, 0.6f),
                    new Vector3(0.4f, 0.7f, 0.6f),
                    new Vector3(0.3f, 0.5f, 0.6f),
                    new Vector3(0.5f, 0.5f, 0.8f));
                if (!builder.TreeExists("test"))
                    builder.SaveTree("test");
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

                cqCommandQueue = CL.CreateCommandQueueWithProperties(cxGPUContext, device, CommandQueueProperty.None, out resultCode);

                baseBlockBuffer = CL.CreateBuffer(cxGPUContext, MemoryFlags.ReadWrite, octree.BaseBlocks, out resultCode);
                blockBuffer = CL.CreateBuffer(cxGPUContext, MemoryFlags.ReadWrite, octree.Blocks, out resultCode);
                usageBuffer = CL.CreateBuffer(cxGPUContext, MemoryFlags.ReadWrite, new Usage[octree.BlockCount >> 3], out resultCode);
                childRequestIdBuffer = CL.CreateBuffer(cxGPUContext, MemoryFlags.ReadWrite, new uint[1], out resultCode);
                childRequestBuffer = CL.CreateBuffer(cxGPUContext, MemoryFlags.ReadWrite, new ChildRequest[_traceInput.MaxChildRequestId], out resultCode);
                parentSizeBuffer = CL.CreateBuffer(cxGPUContext, MemoryFlags.ReadWrite, new uint[1], out resultCode);
                parentResidencyBuffer = CL.CreateBuffer(cxGPUContext, MemoryFlags.ReadWrite, parentResidency, out resultCode);
                parentsBuffer = CL.CreateBuffer(cxGPUContext, MemoryFlags.ReadWrite, parents, out resultCode);
                dereferenceQueueBuffer = CL.CreateBuffer(cxGPUContext, MemoryFlags.ReadWrite, new ulong[octree.BlockCount], out resultCode);
                dereferenceRemainingBuffer = CL.CreateBuffer(cxGPUContext, MemoryFlags.ReadWrite, new uint[1], out resultCode);
                semaphorBuffer = CL.CreateBuffer(cxGPUContext, MemoryFlags.ReadWrite, new int[1], out resultCode);

                CLEvent @event;

                CL.EnqueueWriteBuffer(cqCommandQueue, baseBlockBuffer, false, UIntPtr.Zero, octree.BaseBlocks, Array.Empty<CLEvent>(), out @event);
                CL.EnqueueWriteBuffer(cqCommandQueue, blockBuffer, false, UIntPtr.Zero, octree.Blocks, Array.Empty<CLEvent>(), out @event);
                CL.EnqueueWriteBuffer(cqCommandQueue, usageBuffer, false, UIntPtr.Zero, usage, Array.Empty<CLEvent>(), out @event);
                CL.EnqueueWriteBuffer(cqCommandQueue, parentSizeBuffer, false, UIntPtr.Zero, parentSize, Array.Empty<CLEvent>(), out @event);
                CL.EnqueueWriteBuffer(cqCommandQueue, parentResidencyBuffer, false, UIntPtr.Zero, parentResidency, Array.Empty<CLEvent>(), out @event);
                CL.EnqueueWriteBuffer(cqCommandQueue, parentsBuffer, false, UIntPtr.Zero, parents, Array.Empty<CLEvent>(), out @event);
                CL.EnqueueWriteBuffer(cqCommandQueue, childRequestBuffer, false, UIntPtr.Zero, new ChildRequest[_traceInput.MaxChildRequestId], Array.Empty<CLEvent>(), out @event);

                prune = CL.CreateKernel(program, "prune", out resultCode);
                CL.SetKernelArg(prune, 0, baseBlockBuffer);
                CL.SetKernelArg(prune, 1, blockBuffer);
                CL.SetKernelArg(prune, 2, usageBuffer);
                CL.SetKernelArg(prune, 3, childRequestIdBuffer);
                CL.SetKernelArg(prune, 4, childRequestBuffer);
                CL.SetKernelArg(prune, 5, parentSizeBuffer);
                CL.SetKernelArg(prune, 6, parentResidencyBuffer);
                CL.SetKernelArg(prune, 7, parentsBuffer);
                CL.SetKernelArg(prune, 8, dereferenceQueueBuffer);
                CL.SetKernelArg(prune, 9, dereferenceRemainingBuffer);
                CL.SetKernelArg(prune, 10, semaphorBuffer);

                graft = CL.CreateKernel(program, "prune", out resultCode);
                CL.SetKernelArg(graft, 0, blockBuffer);
                CL.SetKernelArg(graft, 1, usageBuffer);
                CL.SetKernelArg(graft, 2, childRequestIdBuffer);
                CL.SetKernelArg(graft, 3, childRequestBuffer);
                CL.SetKernelArg(graft, 4, parentSizeBuffer);
                CL.SetKernelArg(graft, 5, parentResidencyBuffer);
                CL.SetKernelArg(graft, 6, parentsBuffer);
                CL.SetKernelArg(graft, 7, dereferenceQueueBuffer);
                CL.SetKernelArg(graft, 8, dereferenceRemainingBuffer);
                CL.SetKernelArg(graft, 9, semaphorBuffer);

                traceVoxel = CL.CreateKernel(program, "traceVoxel", out resultCode);
                CL.SetKernelArg(traceVoxel, 0, baseBlockBuffer);
                CL.SetKernelArg(traceVoxel, 1, blockBuffer);
                CL.SetKernelArg(traceVoxel, 2, usageBuffer);
                CL.SetKernelArg(traceVoxel, 3, childRequestIdBuffer);
                CL.SetKernelArg(traceVoxel, 4, childRequestBuffer);
                CL.SetKernelArg(traceVoxel, 5, this.mColors);

                traceMesh = CL.CreateKernel(program, "traceMesh", out resultCode);

                traceParticle = CL.CreateKernel(program, "traceParticle", out resultCode);

                spawnRays = CL.CreateKernel(program, "spawnRays", out resultCode);

                traceLight = CL.CreateKernel(program, "traceLight", out resultCode);

                resolveImage = CL.CreateKernel(program, "resolveImage", out resultCode);

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

        private void initVBO() { }
        /*
        private void initVBO()
        {
            // Remove buffers if they already exist
            if (renderBuffer != 0)
            {
                GL.DeleteBuffers(1, ref renderBuffer);
                renderBuffer = 0;
            }
            if (frameBuffer != 0)
            {
                GL.DeleteBuffers(1, ref frameBuffer);
                frameBuffer = 0;
            }

            // Create fresh buffers
            GL.GenRenderbuffers(1, out renderBuffer);
            GL.GenFramebuffers(1, out frameBuffer);

            // Initialize the frame buffer
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, frameBuffer);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, renderBuffer, 0);

            // Initializes the render buffer
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderBuffer);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba16f, base.Size.X, base.Size.Y);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, renderBuffer);

            this.mColors = CLImage2D.CreateFromGLRenderbuffer(cxGPUContext, MemoryFlags.WriteOnly, (int)renderBuffer);

            //Release buffers
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        }
        */
        #endregion

        #region //Data Processing
        public void UpdatePruning(Pruning pruningInput, BlockData? pruningBlockDataInput, Location? pruningAddressInput)
        {
            lock (pruningBufferLock)
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
            lock (pruningBufferLock)
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
        protected override void OnUnload()
        {
            base.OnUnload();
            
            //GL.DeleteBuffers(1, ref renderBuffer);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            //TODO - fix rendering
            GL.Viewport(new System.Drawing.Size(base.Size.X, base.Size.Y));
            //GL.MatrixMode(MatrixMode.Projection);
            float degreeRadian = ((float)Math.PI / 180f) * 50f;
            var projection = OpenTK.Mathematics.Matrix4.CreatePerspectiveFieldOfView(degreeRadian, base.Size.X / (float)base.Size.Y, 0.1f, 100.0f);
            GL.LoadMatrix(ref projection);
            initVBO();
            //traceVoxel.SetMemoryArgument(5, this.mColors);
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
        protected override void OnRenderFrame(FrameEventArgs e) { }
        /*
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            if (!initialized)
            {
                return;
            }

            runKernels();

            GL.Viewport(new System.Drawing.Size(base.Size.X, base.Size.Y));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, frameBuffer);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, renderBuffer);

            GL.BlitFramebuffer(0, 0, base.Size.X, base.Size.Y, 0, 0, base.Size.X, base.Size.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            FramebufferErrorCode err = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);

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
            float sinx = 0.005f * (float)Math.Sin(base.MouseState.X / 1000.0);
            float cosx = 0.005f * (float)Math.Sin(base.MouseState.X / 1000.0);
            float siny = 0.005f * (float)Math.Sin(base.MouseState.Y / 1000.0);
            float cosy = 0.005f * (float)Math.Sin(base.MouseState.Y / 1000.0);

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
        */

        private void runKernels()
        {
        }
        /*
        private void runKernels()
        {
            GL.Finish();
            
            computeMemory = new CLBuffer[] { this.mColors };
            CLGL.EnqueueAcquireGLObjects(cqCommandQueue, computeMemory, null, out CLEvent @event);

            _traceInput.ScreenSize = base.Size;
            _traceInput.FoV[0] = (float)base.Size.X / (float)base.Size.Y * (float)Math.PI / 4.0f;
            _traceInput.Tick = tick;
            _updateInput.Tick = tick;
            //Flush child request buffer
            cqCommandQueue.WriteToBuffer(new uint[] { 0 }, childRequestIdBuffer, false, null);

            runPrune();
            runGraft();

            traceVoxel.SetValueArgument(6, _traceInput);
            cqCommandQueue.Execute(traceVoxel, null, new long[2] { base.Size.X, base.Size.Y }, null, null);

            CL.ReleaseCommandQueue(cqCommandQueue);
            cqCommandQueue.ReleaseGLObjects(computeMemory, null);
        }
        */

        private void runPrune() { }
        /*
        private void runPrune()
        {
            long pruningCount = 0;
            var pruningArray = new Pruning[0];
            var pruningBlockDataArray = new BlockData[0];
            var pruningAddressesArray = new Location[0];
            lock (pruningBufferLock)
            {
                pruningCount = pruning.Count();
                if (pruningCount > 0)
                {
                    pruningBuffer.Dispose();
                    pruningBlockDataBuffer.Dispose();
                    pruningAddressesBuffer.Dispose();

                    pruningArray = pruning.ToArray();
                    pruningBlockDataArray = pruningBlockData.ToArray();
                    pruningAddressesArray = pruningAddresses.ToArray();

                    pruning = new List<Pruning>();
                    pruningBlockData = new List<BlockData>();
                    pruningAddresses = new List<Location>();
                }
            }
            if (pruningCount > 0)
            {
                pruningBuffer = CL.CreateBuffer<Pruning>(cxGPUContext, MemoryFlags.None, pruning.Count);
                pruningBlockDataBuffer = CL.CreateBuffer<BlockData>(cxGPUContext, MemoryFlags.None, pruningBlockData.Count);
                pruningAddressesBuffer = CL.CreateBuffer<Location>(cxGPUContext, MemoryFlags.None, pruningAddresses.Count);

                cqCommandQueue.WriteToBuffer(new uint2[blockCount], dereferenceQueueBuffer, false, null);
                cqCommandQueue.WriteToBuffer(new uint[] { 0 }, dereferenceRemainingBuffer, false, null);
                cqCommandQueue.WriteToBuffer(new int[] { 0 }, semaphorBuffer, false, null);
                cqCommandQueue.WriteToBuffer(pruningArray, pruningBuffer, false, null);
                cqCommandQueue.WriteToBuffer(pruningBlockDataArray, pruningBlockDataBuffer, false, null);
                cqCommandQueue.WriteToBuffer(pruningAddressesArray, pruningAddressesBuffer, false, null);

                prune.SetMemoryArgument(11, pruningBuffer);
                prune.SetMemoryArgument(12, pruningBlockDataBuffer);
                prune.SetMemoryArgument(13, pruningAddressesBuffer);
                prune.SetValueArgument(14, _updateInput);

                cqCommandQueue.Execute(prune, null, new long[1] { pruningCount }, null, null);
            }
        }
        */

        private void runGraft() { }
        /*
        private void runGraft()
        {
            long graftingCount = 0;
            var graftingArray = new Grafting[0];
            var graftingBlocksArray = new Block[0];
            var graftingAddressesArray = new Location[0];
            lock (graftingBufferLock)
            {
                graftingCount = pruning.Count();
                if (graftingCount > 0)
                {
                    graftingBuffer.Dispose();
                    graftingBlocksBuffer.Dispose();
                    graftingAddressesBuffer.Dispose();

                    graftingArray = grafting.ToArray();
                    graftingBlocksArray = graftingBlocks.ToArray();
                    graftingAddressesArray = graftingAddresses.ToArray();

                    grafting = new List<Grafting>();
                    graftingBlocks = new List<Block>();
                    graftingAddresses = new List<Location>();
                }
            }
            _updateInput.GraftSize = (uint)graftingBlocksArray.Count();
            if (graftingCount > 0)
            {

                graftingBuffer = CL.CreateBuffer<Grafting>(cxGPUContext, MemoryFlags.None, grafting.Count);
                graftingBlocksBuffer = CL.CreateBuffer<Block>(cxGPUContext, MemoryFlags.None, graftingBlocks.Count);
                graftingAddressesBuffer = CL.CreateBuffer<Location>(cxGPUContext, MemoryFlags.None, graftingAddresses.Count);
                holdingAddressesBuffer = CL.CreateBuffer<uint>(cxGPUContext, MemoryFlags.None, _updateInput.GraftSize);
                addressPositionBuffer = CL.CreateBuffer<uint>(cxGPUContext, MemoryFlags.None, 1);

                cqCommandQueue.WriteToBuffer(new uint2[blockCount], dereferenceQueueBuffer, false, null);
                cqCommandQueue.WriteToBuffer(new uint[] { 0 }, dereferenceRemainingBuffer, false, null);
                cqCommandQueue.WriteToBuffer(new int[] { 0 }, semaphorBuffer, false, null);
                cqCommandQueue.WriteToBuffer(new uint[_updateInput.GraftSize], holdingAddressesBuffer, false, null);
                cqCommandQueue.WriteToBuffer(new uint[] { 0 }, addressPositionBuffer, false, null);
                cqCommandQueue.WriteToBuffer(graftingArray, graftingBuffer, false, null);
                cqCommandQueue.WriteToBuffer(graftingBlocksArray, graftingBlocksBuffer, false, null);
                cqCommandQueue.WriteToBuffer(graftingAddressesArray, graftingAddressesBuffer, false, null);

                graft.SetMemoryArgument(10, graftingBuffer);
                graft.SetMemoryArgument(11, graftingBlocksBuffer);
                graft.SetMemoryArgument(12, graftingAddressesBuffer);
                graft.SetMemoryArgument(13, holdingAddressesBuffer);
                graft.SetMemoryArgument(14, addressPositionBuffer);
                graft.SetValueArgument(15, _updateInput);

                cqCommandQueue.Execute(graft, null, new long[1] { graftingCount }, null, null);
            }
        } 
        */
        #endregion

        #region //Error
        protected void OnError(IntPtr waitEvent, IntPtr userData){

		}

        protected void HandleResultCode(CLResultCode resultCode, string method){
            if (resultCode == CLResultCode.Success) return;
            throw new Exception($"{method}: {Enum.GetName(resultCode)}");
		}
        #endregion
    }
}
