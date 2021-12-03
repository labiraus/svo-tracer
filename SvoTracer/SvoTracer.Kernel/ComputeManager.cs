using OpenTK.Compute.OpenCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Kernel
{
	public class ComputeManager : IDisposable
	{
		private readonly CLContext _context;
		private readonly CLCommandQueue _commandQueue;
		private readonly CLProgram _program;
		private CLImage renderbuffer = CLImage.Zero;
		private readonly Dictionary<KernelName, CLKernel> _kernels = new Dictionary<KernelName, CLKernel>();
		private readonly Dictionary<BufferName, CLBuffer> _buffers = new Dictionary<BufferName, CLBuffer>();
		private readonly Dictionary<KernelName, Dictionary<string, uint>> _argumentPositions = new Dictionary<KernelName, Dictionary<string, uint>>();


		internal ComputeManager(CLContext context, CLCommandQueue commandQueue, CLProgram program)
		{
			_context = context;
			_commandQueue = commandQueue;
			_program = program;
		}

		#region renderbuffer
		public void InitRenderbuffer(uint renderbufferHandle)
		{
			CLResultCode resultCode;
			if (renderbuffer != CLImage.Zero)
			{
				resultCode = CL.ReleaseMemoryObject(renderbuffer);
				HandleResultCode(resultCode, "ReleaseMemoryObject:renderbuffer");
			}

			renderbuffer = CLGL.CreateFromGLRenderbuffer(_context, MemoryFlags.WriteOnly, renderbufferHandle, out resultCode);
			HandleResultCode(resultCode, "CreateFromGLRenderbuffer");
		}

		public (CLImage renderbuffer, CLEvent waitEvent) AcquireRenderbuffer(CLEvent[] waitEvents = null)
		{
			var resultCode = CLGL.EnqueueAcquireGLObjects(_commandQueue, new[] { renderbuffer.Handle }, waitEvents, out CLEvent acquireImage);
			HandleResultCode(resultCode, "EnqueueAcquireGLObjects");
			return (renderbuffer, acquireImage);
		}

		public CLEvent ReleaseRenderbuffer(CLEvent[] waitEvents = null)
		{
			var resultCode = CLGL.EnqueueReleaseGLObjects(_commandQueue, new[] { renderbuffer.Handle }, waitEvents, out CLEvent waitEvent);
			HandleResultCode(resultCode, "EnqueueReleaseGLObjects");
			return waitEvent;
		}
		#endregion

		#region kernels

		private CLKernel getKernel(KernelName kernelName)
		{
			if (!_kernels.ContainsKey(kernelName))
			{
				var kernel = CL.CreateKernel(_program, getKernelName(kernelName), out CLResultCode resultCode);
				HandleResultCode(resultCode, $"CreateKernel:{kernelName}");
				_kernels[kernelName] = kernel;
				_argumentPositions[kernelName] = getArgumentPositions(kernelName);
			}
			return _kernels[kernelName];
		}

		public CLEvent Enqueue(KernelName kernelName, nuint[] workSize, CLEvent[] waitEvents = null)
		{
			var kernel = getKernel(kernelName);
			var resultCode = CL.EnqueueNDRangeKernel(_commandQueue, kernel, (uint)workSize.Length, null, workSize, null, waitEvents, out CLEvent kernelRun);
			HandleResultCode(resultCode, "EnqueueNDRangeKernel");
			return kernelRun;
		}

		public void SetArg<T>(KernelName kernelName, string paramName, T argument) where T : unmanaged
		{
			var kernel = getKernel(kernelName);
			var resultCode = CL.SetKernelArg(kernel, _argumentPositions[kernelName][paramName], argument);
			HandleResultCode(resultCode, $"SetKernelArg:{kernelName}:{paramName}");
		}

		public void SetArg<T>(KernelName kernelName, string paramName, T[] argument) where T : unmanaged
		{
			var kernel = getKernel(kernelName);
			var resultCode = CL.SetKernelArg(kernel, _argumentPositions[kernelName][paramName], argument);
			HandleResultCode(resultCode, $"SetKernelArg:{kernelName}:{paramName}");
		}

		public void SetArg(KernelName kernelName, string paramName, BufferName bufferName)
		{
			var kernel = getKernel(kernelName);
			var buffer = getBuffer(bufferName);
			var resultCode = CL.SetKernelArg(kernel, _argumentPositions[kernelName][paramName], buffer);
			HandleResultCode(resultCode, $"SetKernelArg:{kernelName}:{paramName}");
		}

		public void Wait(CLEvent[] waitEvents)
		{
			CL.WaitForEvents(waitEvents);
		}

		#endregion

		#region buffers

		private CLBuffer getBuffer(BufferName bufferName)
		{
			if (!_buffers.ContainsKey(bufferName))
			{
				throw new Exception($"Buffer {bufferName} not initializied");
			}
			return _buffers[bufferName];
		}

		public void InitBuffer<T>(BufferName bufferName, T[] data) where T : unmanaged
		{
			CLResultCode resultCode;
			if (_buffers.ContainsKey(bufferName))
			{
				resultCode = CL.ReleaseMemoryObject(_buffers[bufferName]);
				HandleResultCode(resultCode, $"ReleaseBuffer:{bufferName}");
			}
			_buffers[bufferName] = CL.CreateBuffer(_context, MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, data, out resultCode);
			HandleResultCode(resultCode, $"CreateBuffer:{bufferName}");
		}

		public void InitDeviceBuffer(BufferName bufferName, int arraySize, int elementSize)
		{
			CLResultCode resultCode;
			if (_buffers.ContainsKey(bufferName))
			{
				resultCode = CL.ReleaseMemoryObject(_buffers[bufferName]);
				HandleResultCode(resultCode, $"ReleaseBuffer:{bufferName}");
			}
			_buffers[bufferName] = CL.CreateBuffer(_context, MemoryFlags.ReadWrite | MemoryFlags.HostNoAccess, (uint)(arraySize * elementSize), IntPtr.Zero, out resultCode);
			HandleResultCode(resultCode, $"CreateBuffer:{bufferName}");
		}

		public unsafe void InitDeviceBuffer<T>(BufferName bufferName, int arraySize) where T : unmanaged
		{
			CLResultCode resultCode;
			if (_buffers.ContainsKey(bufferName))
			{
				resultCode = CL.ReleaseMemoryObject(_buffers[bufferName]);
				HandleResultCode(resultCode, $"ReleaseBuffer:{bufferName}");
			}
			_buffers[bufferName] = CL.CreateBuffer(_context, MemoryFlags.ReadWrite | MemoryFlags.HostNoAccess, (uint)(arraySize * sizeof(T)), IntPtr.Zero, out resultCode);
			HandleResultCode(resultCode, $"CreateBuffer:{bufferName}");
		}

		public void InitReadBuffer<T>(BufferName bufferName, T[] data) where T : unmanaged
		{
			CLResultCode resultCode;
			if (_buffers.ContainsKey(bufferName))
			{
				resultCode = CL.ReleaseMemoryObject(_buffers[bufferName]);
				HandleResultCode(resultCode, $"ReleaseBuffer:{bufferName}");
			}
			_buffers[bufferName] = CL.CreateBuffer(_context, MemoryFlags.ReadOnly | MemoryFlags.CopyHostPtr, data, out resultCode);
			HandleResultCode(resultCode, $"CreateBuffer:{bufferName}");
		}

		public unsafe void InitWriteBuffer<T>(BufferName bufferName, int arraySize) where T : unmanaged
		{
			CLResultCode resultCode;
			if (_buffers.ContainsKey(bufferName))
			{
				resultCode = CL.ReleaseMemoryObject(_buffers[bufferName]);
				HandleResultCode(resultCode, $"ReleaseBuffer:{bufferName}");
			}
			_buffers[bufferName] = CL.CreateBuffer(_context, MemoryFlags.WriteOnly, (uint)(arraySize * sizeof(T)), IntPtr.Zero, out resultCode);
			HandleResultCode(resultCode, $"CreateBuffer:{bufferName}");
		}

		public CLEvent WriteBuffer<T>(BufferName bufferName, T[] data, CLEvent[] waitEvents = null) where T : unmanaged
		{
			var buffer = getBuffer(bufferName);
			var resultCode = CL.EnqueueWriteBuffer(_commandQueue, buffer, false, 0, data, waitEvents, out CLEvent finishEvent);
			HandleResultCode(resultCode, $"EnqueueWriteBuffer:{bufferName}");
			return finishEvent;
		}

		public void ReadBuffer<T>(BufferName bufferName, T[] array) where T : unmanaged
		{
			var buffer = getBuffer(bufferName);
			var resultCode = CL.EnqueueReadBuffer(_commandQueue, buffer, true, 0, array, null, out _);
			HandleResultCode(resultCode, $"EnqueueReadBuffer:{bufferName}");
		}
		#endregion

		public void Flush()
		{
			var resultCode = CL.Flush(_commandQueue);
			HandleResultCode(resultCode, $"Flush");
		}

		public static void HandleResultCode(CLResultCode resultCode, string method)
		{
			if (resultCode != CLResultCode.Success)
				throw new Exception($"{method}: {Enum.GetName(resultCode)}");
		}

		private static string getKernelName(KernelName name)
		{
			return name switch
			{
				KernelName.Prune => "prune",
				KernelName.Graft => "graft",
				KernelName.SpawnRays => "spawnRays",
				KernelName.TraceVoxel => "traceVoxel",
				KernelName.TraceMesh => "traceMesh",
				KernelName.TraceParticle => "traceParticle",
				KernelName.TraceLight => "traceLight",
				KernelName.ResolveImage => "resolveImage",
				KernelName.Test => "test",
				_ => throw new Exception($"Kernel name {name} not found"),
			};
		}

		private static Dictionary<string, uint> getArgumentPositions(KernelName name)
		{
			string[] paramList = Array.Empty<string>();
			switch (name)
			{
				case KernelName.Prune:
					paramList = new[] { "bases", "blocks", "usage", "childRequestId", "childRequests", "parentSize", "parentResidency", "parents", "dereferenceQueue", "dereferenceRemaining", "semaphor", "pruning", "pruningBlockData", "pruningAddresses", "inputData" };
					break;
				case KernelName.Graft:
					paramList = new[] { "blocks", "usage", "childRequestId", "childRequests", "parentSize", "parentResidency", "parents", "dereferenceQueue", "dereferenceRemaining", "semaphor", "grafting", "graftingBlocks", "graftingAddresses", "holdingAddresses", "addressPosition", "inputData" };
					break;
				case KernelName.SpawnRays:
					break;
				case KernelName.TraceVoxel:
					paramList = new[] { "bases", "blocks", "usage", "childRequestId", "childRequests", "depthMask", "outputImage", "_input" };
					break;
				case KernelName.TraceMesh:
					break;
				case KernelName.TraceParticle:
					break;
				case KernelName.TraceLight:
					break;
				case KernelName.ResolveImage:
					break;
				case KernelName.Test:
					paramList = new[] { "a", "b", "c", "q" };
					break;
				default:
					throw new Exception($"Kernel name {name} not found");
			}
			var output = new Dictionary<string, uint>();
			for (uint i = 0; i < paramList.Length; i++)
			{
				output[paramList[i]] = i;
			}
			return output;
		}

		public void Dispose()
		{
			foreach (var buffer in _buffers.Values)
				CL.ReleaseMemoryObject(buffer);
			CL.ReleaseMemoryObject(renderbuffer);
			foreach (var kernel in _kernels.Values)
				CL.ReleaseKernel(kernel);
			CL.ReleaseProgram(_program);
			CL.ReleaseCommandQueue(_commandQueue);
			CL.ReleaseContext(_context);
		}
	}
}
