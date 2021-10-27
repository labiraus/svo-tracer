using OpenTK.Compute.OpenCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Kernel
{
	public static class ComputeManagerFactory
	{
		public static ComputeManager Build(IntPtr glContext, IntPtr WglHdc, IEnumerable<string> programFiles)
		{
			CLDevice device = CLDevice.Zero;
			CLResultCode resultCode;

			resultCode = CL.GetPlatformIDs(out CLPlatform[] platformIds);
			ComputeManager.HandleResultCode(resultCode, "CL.GetPlatformIds");
			CLPlatform platform = CLPlatform.Zero;
			foreach (var platformId in platformIds)
			{
				resultCode = platformId.SupportsPlatformExtension("cl_khr_gl_sharing", out bool supported);
				ComputeManager.HandleResultCode(resultCode, "SupportsPlatformExtension");
				if (supported)
				{
					platform = platformId;
					break;
				}
			}
			if (platform == CLPlatform.Zero)
			{
				throw new Exception("Platform supporting cl_khr_gl_sharing not found");
			}

			resultCode = platform.GetDeviceIDs(DeviceType.Gpu, out CLDevice[] devices);
			ComputeManager.HandleResultCode(resultCode, "GetDeviceIDs");
			foreach (var deviceId in devices)
			{
				resultCode = deviceId.GetDeviceInfo(DeviceInfo.Extensions, out byte[] bytes);
				ComputeManager.HandleResultCode(resultCode, "GetDeviceInfo");
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

			var props = new CLContextProperties(platform)
			{
				// if windows
				ContextInteropUserSync = true,
				GlContextKHR = glContext,
				WglHDCKHR = WglHdc
			};

			// if linux
			//props.GlContextKHR = (IntPtr)GLFW.GetGLXContext(base.WindowPtr);
			//props.GlxDisplayKHR = (IntPtr)GLFW.GetX11Window(base.WindowPtr);

			var context = props.CreateContextFromType(DeviceType.Gpu, null, IntPtr.Zero, out resultCode);
			ComputeManager.HandleResultCode(resultCode, "CreateContextFromType");
			var commandQueue = context.CreateCommandQueueWithProperties(device, new CLCommandQueueProperties(), out resultCode);
			ComputeManager.HandleResultCode(resultCode, "CreateCommandQueueWithProperties");

			var programSources = programFiles.Select(x => KernelLoader.Get(x)).ToArray();

			var program = context.CreateProgramWithSource(programFiles.Select(x => KernelLoader.Get(x)).ToArray(), out resultCode);
			ComputeManager.HandleResultCode(resultCode, "CreateProgramWithSource");
			resultCode = program.BuildProgram(new[] { device }, null, null, IntPtr.Zero);
			if (resultCode == CLResultCode.BuildProgramFailure)
			{
				program.GetProgramBuildInfo(device, ProgramBuildInfo.Log, out byte[] bytes);
				Console.WriteLine(Encoding.ASCII.GetString(bytes));
			}
			ComputeManager.HandleResultCode(resultCode, "BuildProgram");

			return new ComputeManager(context, commandQueue, program);
		}
	}
}
