using OpenTK.Compute.OpenCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Kernel
{
	public class ComputeManager
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
				resultCode = renderbuffer.ReleaseMemoryObject();
				HandleResultCode(resultCode, "ReleaseMemoryObject:renderbuffer");
			}

			renderbuffer = _context.CreateFromGLRenderbuffer(MemoryFlags.WriteOnly, renderbufferHandle, out resultCode);
			HandleResultCode(resultCode, "CreateFromGLRenderbuffer");
		}

		public (CLImage buffer, CLEvent waitEvent) AcquireRenderbuffer(CLEvent[] waitEvents = null)
		{
			var resultCode = _commandQueue.EnqueueAcquireGLObjects(new[] { renderbuffer.Handle }, waitEvents, out CLEvent acquireImage);
			HandleResultCode(resultCode, "EnqueueAcquireGLObjects");
			return (renderbuffer, acquireImage);
		}

		public CLEvent ReleaseRenderbuffer(CLEvent[] waitEvents = null)
		{
			var resultCode = _commandQueue.EnqueueReleaseGLObjects(new[] { renderbuffer.Handle }, waitEvents, out CLEvent waitEvent);
			HandleResultCode(resultCode, "EnqueueReleaseGLObjects");
			return waitEvent;
		}
		#endregion

		#region kernels

		private CLKernel getKernel(KernelName kernelName)
		{
			if (!_kernels.ContainsKey(kernelName))
			{
				var kernel = _program.CreateKernel(getKernelName(kernelName), out CLResultCode resultCode);
				HandleResultCode(resultCode, $"CreateKernel:{kernelName}");
				_kernels[kernelName] = kernel;
				_argumentPositions[kernelName] = getArgumentPositions(kernelName);
			}
			return _kernels[kernelName];
		}

		public CLEvent Enqueue(KernelName kernelName, nuint[] workSize, CLEvent[] waitEvents = null)
		{
			var kernel = getKernel(kernelName);
			var resultCode = _commandQueue.EnqueueNDRangeKernel(kernel, (uint)workSize.Length, null, workSize, null, waitEvents, out CLEvent kernelRun);
			HandleResultCode(resultCode, "EnqueueNDRangeKernel");
			return kernelRun;
		}

		public void SetArg<T>(KernelName kernelName, string paramName, T argument) where T : unmanaged
		{
			var kernel = getKernel(kernelName);
			var resultCode = kernel.SetKernelArg(_argumentPositions[kernelName][paramName], argument);
			ComputeManager.HandleResultCode(resultCode, "EnqueueNDRangeKernel");
		}

		public void SetArg(KernelName kernelName, string paramName, BufferName bufferName)
		{
			var kernel = getKernel(kernelName);
			var buffer = getBuffer(bufferName); ;
			var resultCode = kernel.SetKernelArg(_argumentPositions[kernelName][paramName], buffer);
			ComputeManager.HandleResultCode(resultCode, "EnqueueNDRangeKernel");
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
				resultCode = _buffers[bufferName].ReleaseMemoryObject();
				HandleResultCode(resultCode, $"ReleaseBuffer:{bufferName}");
			}
			_buffers[bufferName] = _context.CreateBuffer(MemoryFlags.ReadWrite | MemoryFlags.UseHostPtr, data, out resultCode);
			HandleResultCode(resultCode, $"CreateBuffer:{bufferName}");
		}

		public CLEvent WriteBuffer<T>(BufferName bufferName, T[] data, CLEvent[] waitEvents = null) where T : unmanaged
		{
			var buffer = getBuffer(bufferName);
			var resultCode = _commandQueue.EnqueueWriteBuffer(buffer, false, 0, data, waitEvents, out CLEvent finishEvent);
			HandleResultCode(resultCode, $"EnqueueWriteBuffer:{bufferName}");
			return finishEvent;
		}

		#endregion

		public void Flush()
		{
			var resultCode = _commandQueue.Flush();
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
					paramList = new[] { "bases", "blocks", "usage", "childRequestId", "childRequests", "outputImage", "_input" };
					break;
				case KernelName.TraceMesh:
					break;
				case KernelName.TraceParticle:
					break;
				case KernelName.TraceLight:
					break;
				case KernelName.ResolveImage:
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
	}
}
