using OpenTK.Compute.OpenCL;
using System;
using System.Text;

namespace SvoTracer.Kernel
{
	public class EnqueueTest
	{
		CLDevice device = CLDevice.Zero;
		CLPlatform platform = CLPlatform.Zero;
		CLCommandQueue deviceCommandQueue;
		CLCommandQueue commandQueue;
		CLContext context;
		CLProgram program;
		CLKernel kernel;

		public EnqueueTest()
		{
			ListDevices();
			GetPlatform();
			GetDevice();
			CLResultCode resultCode;

			context = CL.CreateContextFromType(new CLContextProperties(platform), DeviceType.Gpu, null, IntPtr.Zero, out resultCode);
			HandleResultCode(resultCode, "CreateContextFromType");

			// Create the on device command queue - required for enqueue_kernel
			deviceCommandQueue = CL.CreateCommandQueueWithProperties(context, device, new CLCommandQueueProperties(CommandQueueProperties.OnDevice | CommandQueueProperties.OnDeviceDefault | CommandQueueProperties.OutOfOrderExecModeEnable), out resultCode);
			HandleResultCode(resultCode, "CreateCommandQueueWithProperties:deviceCommandQueue");

			commandQueue = CL.CreateCommandQueueWithProperties(context, device, new CLCommandQueueProperties(), out resultCode);
			HandleResultCode(resultCode, "CreateCommandQueueWithProperties:commandQueue");

			program = CL.CreateProgramWithSource(context, KernelLoader.Get("test.cl"), out resultCode);
			HandleResultCode(resultCode, "CreateProgramWithSource");

			resultCode = CL.BuildProgram(program, new[] { device }, "-cl-std=CL3.0", null, IntPtr.Zero);
			if (resultCode == CLResultCode.BuildProgramFailure)
			{
				CL.GetProgramBuildInfo(program, device, ProgramBuildInfo.Log, out byte[] buildLog);
				Console.WriteLine(Encoding.ASCII.GetString(buildLog));
			}
			HandleResultCode(resultCode, "BuildProgram");

			kernel = CL.CreateKernel(program, "test", out resultCode);
			HandleResultCode(resultCode, "CreateKernel");
		}

		private void ListDevices()
		{
			CLResultCode resultCode = CL.GetPlatformIDs(out CLPlatform[] platformIds);
			HandleResultCode(resultCode, "CL.GetPlatformIds");
			foreach (var platformId in platformIds)
			{
				resultCode = CL.GetPlatformInfo(platformId, PlatformInfo.Version, out byte[] version);
				HandleResultCode(resultCode, "CL.GetPlatformInfo:Version");
				var versionString = Encoding.ASCII.GetString(version);

				resultCode = CL.GetPlatformInfo(platformId, PlatformInfo.Name, out byte[] platformName);
				HandleResultCode(resultCode, "CL.GetPlatformInfo:Name");
				var platformNameString = Encoding.ASCII.GetString(platformName);

				resultCode = CL.GetDeviceIDs(platformId, DeviceType.All, out CLDevice[] devices);
				HandleResultCode(resultCode, "GetDeviceIDs");

				foreach (var deviceId in devices)
				{
					resultCode = CL.GetDeviceInfo(deviceId, DeviceInfo.Name, out byte[] deviceName);
					HandleResultCode(resultCode, "CL.GetDeviceInfo:Name");
					var deviceNameString = Encoding.ASCII.GetString(deviceName);

					resultCode = CL.GetDeviceInfo(deviceId, DeviceInfo.DriverVersion, out byte[] driverVersion);
					HandleResultCode(resultCode, "CL.GetDeviceInfo:Name");
					var driverVersionString = Encoding.ASCII.GetString(driverVersion);

					Console.WriteLine($"Version: {versionString}, Platform: {platformNameString}, Device: {deviceNameString}, Driver: {driverVersionString}");
				}
			}
		}

		private void GetPlatform()
		{
			CLResultCode resultCode = CL.GetPlatformIDs(out CLPlatform[] platformIds);
			HandleResultCode(resultCode, "CL.GetPlatformIds");
			foreach (var platformId in platformIds)
			{
				platform = platformId;
				break;
			}
			if (platform == CLPlatform.Zero)
			{
				throw new Exception("Platform not found");
			}
		}

		private void GetDevice()
		{
			CLResultCode resultCode = CL.GetDeviceIDs(platform, DeviceType.Gpu, out CLDevice[] devices);
			HandleResultCode(resultCode, "GetDeviceIDs");
			foreach (var deviceId in devices)
			{
				resultCode = CL.GetDeviceInfo(deviceId, DeviceInfo.DeviceDeviceEnqueueCapabilities, out byte[] enqueueCapabilities);
				HandleResultCode(resultCode, "GetDeviceInfo:DeviceDeviceEnqueueCapabilities");
				if ((enqueueCapabilities[0] & (byte)DeviceEnqueueCapabilities.Supported) > 0)
				{
					device = deviceId;
					break;
				}
			}
			if (device == CLDevice.Zero)
			{
				throw new Exception("Device supporting cl_khr_gl_sharing not found");
			}
		}

		public void Run()
		{
			CLResultCode resultCode;
			var buffer = CL.CreateBuffer(context, MemoryFlags.WriteOnly, sizeof(int), IntPtr.Zero, out resultCode);
			HandleResultCode(resultCode, $"CreateBuffer");

			resultCode = CL.SetKernelArg(kernel, 0, buffer);
			HandleResultCode(resultCode, $"SetKernelArg");

			CL.GetCommandQueueInfo(commandQueue, CommandQueueInfo.DeviceDefault, out byte[] commandProps);
			if ((IntPtr)BitConverter.ToUInt64(commandProps, 0) == commandQueue.Handle)
				Console.WriteLine("commandQueue is default");
			else if ((IntPtr)BitConverter.ToUInt64(commandProps, 0) == deviceCommandQueue.Handle)
				Console.WriteLine("deviceCommandQueue is default");
			else
				Console.WriteLine("unknown default command queue");

			resultCode = CL.EnqueueNDRangeKernel(commandQueue, kernel, 1, null, new nuint[] { 1 }, null, null, out CLEvent kernelRun);
			HandleResultCode(resultCode, "EnqueueNDRangeKernel");

			resultCode = CL.Flush(commandQueue);
			HandleResultCode(resultCode, $"Flush");

			CL.WaitForEvents(new[] { kernelRun });

			var output = new int[1];
			resultCode = CL.EnqueueReadBuffer(commandQueue, buffer, true, 0, output, null, out _);
			HandleResultCode(resultCode, $"EnqueueReadBuffer");

			switch (output[0])
			{
				case 0:
					Console.WriteLine("Error");
					break;
				case 1:
					Console.WriteLine("Success");
					break;
				case 2:
					Console.WriteLine("Failure");
					break;
			}
		}

		private static void HandleResultCode(CLResultCode resultCode, string method)
		{
			if (resultCode != CLResultCode.Success)
				throw new Exception($"{method}: {Enum.GetName(resultCode)}");
		}
	}
}
