using OpenTK.Compute.OpenCL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Kernel
{
	public class AtomicTest
	{
		private readonly CLContext _context;
		private readonly CLCommandQueue _commandQueue;
		private readonly CLProgram _program;

		public AtomicTest()
		{
			CLDevice device = CLDevice.Zero;
			CLResultCode resultCode;

			resultCode = CL.GetPlatformIDs(out CLPlatform[] platformIds);
			ComputeManager.HandleResultCode(resultCode, "CL.GetPlatformIds");
			CLPlatform platform = CLPlatform.Zero;
			foreach (var platformId in platformIds)
			{
				resultCode = CL.GetPlatformInfo(platformId, PlatformInfo.Extensions, out byte[] bytes);
				ComputeManager.HandleResultCode(resultCode, "CL.GetPlatformInfo:Extensions");
				var extensions = Encoding.ASCII.GetString(bytes).Split(" ");
				if (extensions.Any(x => x == "cl_khr_gl_sharing"))
				{
					platform = platformId;
					break;
				}
			}
			if (platform == CLPlatform.Zero)
			{
				throw new Exception("Platform supporting cl_khr_gl_sharing not found");
			}
			//platform = platformIds[1];
			resultCode = CL.GetPlatformInfo(platform, PlatformInfo.Name, out byte[] platformName);
			ComputeManager.HandleResultCode(resultCode, "CL.GetPlatformInfo:Name");
			Console.WriteLine(Encoding.ASCII.GetString(platformName));

			resultCode = CL.GetDeviceIDs(platform, DeviceType.Gpu, out CLDevice[] devices);
			ComputeManager.HandleResultCode(resultCode, "GetDeviceIDs");
			foreach (var deviceId in devices)
			{
				resultCode = CL.GetDeviceInfo(deviceId, DeviceInfo.Extensions, out byte[] bytes);
				ComputeManager.HandleResultCode(resultCode, "GetDeviceInfo:Extensions");
				var extensions = Encoding.ASCII.GetString(bytes).Split(" ");
				if (extensions.Any(x => x == "cl_khr_gl_sharing"))
				{
					device = deviceId;
					break;
				}
			}
			if (device == CLDevice.Zero)
			{
				throw new Exception("Device supporting cl_khr_gl_sharing not found");
			}

			var props = new CLContextProperties()
			{
				ContextPlatform = platform,
			};

			_context = CL.CreateContextFromType(props, DeviceType.Gpu, null, IntPtr.Zero, out resultCode);
			ComputeManager.HandleResultCode(resultCode, "CreateContextFromType");

			_commandQueue = CL.CreateCommandQueueWithProperties(_context, device, new CLCommandQueueProperties(), out resultCode);
			ComputeManager.HandleResultCode(resultCode, "CreateCommandQueueWithProperties:commandQueue");

			var source = KernelLoader.Get("test.cl");
			_program = CL.CreateProgramWithSource(_context, source, out resultCode);
			ComputeManager.HandleResultCode(resultCode, "CreateProgramWithSource");
			resultCode = CL.BuildProgram(_program, new[] { device }, "-cl-std=CL3.0", null, IntPtr.Zero);
			if (resultCode == CLResultCode.BuildProgramFailure)
			{
				CL.GetProgramBuildInfo(_program, device, ProgramBuildInfo.Log, out byte[] buildLog);
				Console.WriteLine(Encoding.ASCII.GetString(buildLog));
			}
			ComputeManager.HandleResultCode(resultCode, "BuildProgram");
		}

		public void Run(string kernelName)
		{
			CLResultCode resultCode;
			var workSize = new nuint[] { 10000, 10000 };

			var kernel = CL.CreateKernel(_program, kernelName, out resultCode);
			ComputeManager.HandleResultCode(resultCode, "CreateKernel");

			var idBuffer = CL.CreateBuffer(_context, MemoryFlags.WriteOnly, (uint)(sizeof(uint)), IntPtr.Zero, out resultCode);
			ComputeManager.HandleResultCode(resultCode, $"CreateBuffer");

			var queueBuffer = CL.CreateBuffer(_context, MemoryFlags.ReadWrite, (uint)(workSize.Aggregate(1, (a, b) => a * (int)b) * sizeof(uint)), IntPtr.Zero, out resultCode);
			ComputeManager.HandleResultCode(resultCode, $"CreateBuffer");
			resultCode = CL.EnqueueFillBuffer(_commandQueue, queueBuffer, new uint[] { 0 }, 0, workSize[0], null, out CLEvent kernelInit);


			resultCode = CL.SetKernelArg(kernel, 0, idBuffer);
			resultCode = CL.SetKernelArg(kernel, 1, queueBuffer);
			ComputeManager.HandleResultCode(resultCode, $"SetKernelArg idBuffer");

			Stopwatch timer = new();
			timer.Start();
			resultCode = CL.EnqueueNDRangeKernel(_commandQueue, kernel, (uint)workSize.Length, null, workSize, null, new[] { kernelInit }, out CLEvent kernelRun);
			ComputeManager.HandleResultCode(resultCode, $"EnqueueNDRangeKernel");
			resultCode = CL.WaitForEvents(new[] { kernelRun });
			ComputeManager.HandleResultCode(resultCode, $"WaitForEvents");
			timer.Stop();

			var idOutput = new uint[1];
			resultCode = CL.EnqueueReadBuffer(_commandQueue, idBuffer, true, 0, idOutput, new[] { kernelRun }, out CLEvent readRunId);
			ComputeManager.HandleResultCode(resultCode, $"EnqueueReadBuffer:idOutput");
			var queueOutput = new uint[workSize.Aggregate(1, (a, b) => a * (int)b)];
			resultCode = CL.EnqueueReadBuffer(_commandQueue, queueBuffer, true, 0, queueOutput, new[] { kernelRun }, out CLEvent readRunQueue);
			ComputeManager.HandleResultCode(resultCode, $"EnqueueReadBuffer:queueOutput");

			resultCode = CL.WaitForEvents(new[] { readRunId, readRunQueue });
			ComputeManager.HandleResultCode(resultCode, $"WaitForEvents");
			Console.WriteLine("\r{0}: {1}", !queueOutput.Any(x => x == 0), timer.Elapsed);

			Console.WriteLine(idOutput[0]);
			CL.ReleaseKernel(kernel);
			CL.ReleaseMemoryObject(idBuffer);
			CL.ReleaseMemoryObject(queueBuffer);
		}
	}
}
