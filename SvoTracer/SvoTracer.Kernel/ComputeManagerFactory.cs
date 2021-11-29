﻿using OpenTK.Compute.OpenCL;
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

			resultCode = CL.GetDeviceInfo(device, DeviceInfo.DeviceDeviceEnqueueCapabilities, out byte[] enqueueCapabilities);
			ComputeManager.HandleResultCode(resultCode, "GetDeviceInfo:DeviceDeviceEnqueueCapabilities");
			bool deviceQueueSupported = (enqueueCapabilities[0] & (byte)DeviceEnqueueCapabilities.Supported) > 0;
			if (!deviceQueueSupported)
				Console.WriteLine("Device queue not supported");
			bool deviceQueueReplacableDefault = (enqueueCapabilities[0] & (byte)DeviceEnqueueCapabilities.ReplaceableDefault) > 0;
			if (!deviceQueueReplacableDefault)
				Console.WriteLine("Device queue replacable default not supported");

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

			var context = CL.CreateContextFromType(props, DeviceType.Gpu, null, IntPtr.Zero, out resultCode);
			ComputeManager.HandleResultCode(resultCode, "CreateContextFromType");

			// Create the on device command queue - required for enqueue_kernel
			var deviceCommandQueue = CL.CreateCommandQueueWithProperties(context, device, new CLCommandQueueProperties(CommandQueueProperties.OnDevice | CommandQueueProperties.OnDeviceDefault | CommandQueueProperties.OutOfOrderExecModeEnable), out resultCode);
			ComputeManager.HandleResultCode(resultCode, "CreateCommandQueueWithProperties:deviceCommandQueue");


			var commandQueue = CL.CreateCommandQueueWithProperties(context, device, new CLCommandQueueProperties(), out resultCode);
			ComputeManager.HandleResultCode(resultCode, "CreateCommandQueueWithProperties:commandQueue");

			var program = CL.CreateProgramWithSource(context, programFiles.Select(x => KernelLoader.Get(x)).ToArray(), out resultCode);
			ComputeManager.HandleResultCode(resultCode, "CreateProgramWithSource");
			resultCode = CL.BuildProgram(program, new[] { device }, "-cl-std=CL3.0", null, IntPtr.Zero);
			if (resultCode == CLResultCode.BuildProgramFailure)
			{
				CL.GetProgramBuildInfo(program, device, ProgramBuildInfo.Log, out byte[] buildLog);
				Console.WriteLine(Encoding.ASCII.GetString(buildLog));
			}
			ComputeManager.HandleResultCode(resultCode, "BuildProgram");

			return new ComputeManager(context, commandQueue, deviceCommandQueue, program);
		}
	}
}
