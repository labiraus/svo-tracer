using OpenTK.Compute.OpenCL;
using System;
using System.Collections.Generic;
using System.Text;

namespace SvoTracer.Kernel
{
	public class EnqueueTest
	{

		string kernelSource = @"
kernel void test(global int *out) {
  int i = get_global_id(0);
  if (get_default_queue() != 0) {
    out[i] = 1;
  } else {
    out[i] = 2;
  }
}";

		public EnqueueTest()
		{
			//ListDevices();
			//Console.WriteLine();
			TestAll();
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
					HandleResultCode(resultCode, "CL.GetDeviceInfo:DriverVersion");
					var driverVersionString = Encoding.ASCII.GetString(driverVersion);

					Console.WriteLine($"Version: {versionString}, Platform: {platformNameString}, Device: {deviceNameString}, Driver: {driverVersionString}");
				}
			}
		}

		private void TestAll()
		{
			foreach (var platform in GetPlatforms())
				foreach (var device in GetDevices(platform))
					Test(platform, device);
		}

		private IEnumerable<CLPlatform> GetPlatforms()
		{
			CLResultCode resultCode = CL.GetPlatformIDs(out CLPlatform[] platformIds);
			HandleResultCode(resultCode, "CL.GetPlatformIds");
			return platformIds;
		}

		private IEnumerable<CLDevice> GetDevices(CLPlatform platform)
		{
			CLResultCode resultCode = CL.GetDeviceIDs(platform, DeviceType.Gpu, out CLDevice[] devices);
			HandleResultCode(resultCode, "GetDeviceIDs");
			foreach (var deviceId in devices)
			{
				resultCode = CL.GetDeviceInfo(deviceId, DeviceInfo.DeviceDeviceEnqueueCapabilities, out byte[] enqueueCapabilities);
				HandleResultCode(resultCode, "GetDeviceInfo:DeviceDeviceEnqueueCapabilities");
				if ((enqueueCapabilities[0] & (byte)DeviceEnqueueCapabilities.Supported) > 0)
				{
					yield return deviceId;
				}
			}
		}

		private void Test(CLPlatform platform, CLDevice device)
		{
			CLResultCode resultCode;

			var context = CL.CreateContextFromType(new CLContextProperties(platform), DeviceType.Gpu, null, IntPtr.Zero, out resultCode);
			HandleResultCode(resultCode, "CreateContextFromType");

			// Create the on device command queue - required for enqueue_kernel
			var deviceCommandQueue = CL.CreateCommandQueueWithProperties(context, device, new CLCommandQueueProperties(CommandQueueProperties.OnDevice | CommandQueueProperties.OnDeviceDefault | CommandQueueProperties.OutOfOrderExecModeEnable), out resultCode);
			HandleResultCode(resultCode, "CreateCommandQueueWithProperties:deviceCommandQueue");

			var commandQueue = CL.CreateCommandQueueWithProperties(context, device, new CLCommandQueueProperties(), out resultCode);
			HandleResultCode(resultCode, "CreateCommandQueueWithProperties:commandQueue");

			var program = CL.CreateProgramWithSource(context, kernelSource, out resultCode);
			HandleResultCode(resultCode, "CreateProgramWithSource");

			resultCode = CL.BuildProgram(program, new[] { device }, "-cl-std=CL2.0", null, IntPtr.Zero);
			if (resultCode == CLResultCode.BuildProgramFailure)
			{
				CL.GetProgramBuildInfo(program, device, ProgramBuildInfo.Log, out byte[] buildLog);
				Console.WriteLine(Encoding.ASCII.GetString(buildLog));
			}
			HandleResultCode(resultCode, "BuildProgram");

			var kernel = CL.CreateKernel(program, "test", out resultCode);
			HandleResultCode(resultCode, "CreateKernel");

			var buffer = CL.CreateBuffer(context, MemoryFlags.WriteOnly, sizeof(int), IntPtr.Zero, out resultCode);
			HandleResultCode(resultCode, $"CreateBuffer");

			resultCode = CL.SetKernelArg(kernel, 0, buffer);
			HandleResultCode(resultCode, $"SetKernelArg");

			resultCode = CL.EnqueueNDRangeKernel(commandQueue, kernel, 1, null, new nuint[] { 1 }, null, null, out CLEvent kernelRun);
			HandleResultCode(resultCode, "EnqueueNDRangeKernel");

			resultCode = CL.Flush(commandQueue);
			HandleResultCode(resultCode, $"Flush");

			CL.WaitForEvents(new[] { kernelRun });

			var output = new int[1];
			resultCode = CL.EnqueueReadBuffer(commandQueue, buffer, true, 0, output, null, out _);
			HandleResultCode(resultCode, $"EnqueueReadBuffer");

			resultCode = CL.GetPlatformInfo(platform, PlatformInfo.Version, out byte[] version);
			HandleResultCode(resultCode, "CL.GetPlatformInfo:Version");
			var versionString = Encoding.ASCII.GetString(version);

			resultCode = CL.GetPlatformInfo(platform, PlatformInfo.Name, out byte[] platformName);
			HandleResultCode(resultCode, "CL.GetPlatformInfo:Name");
			var platformNameString = Encoding.ASCII.GetString(platformName);

			resultCode = CL.GetDeviceInfo(device, DeviceInfo.Name, out byte[] deviceName);
			HandleResultCode(resultCode, "CL.GetDeviceInfo:Name");
			var deviceNameString = Encoding.ASCII.GetString(deviceName);

			resultCode = CL.GetDeviceInfo(device, DeviceInfo.DriverVersion, out byte[] driverVersion);
			HandleResultCode(resultCode, "CL.GetDeviceInfo:DriverVersion");
			var driverVersionString = Encoding.ASCII.GetString(driverVersion);

			switch (output[0])
			{
				case 0:
					Console.WriteLine($"Error - Version: {versionString}, Platform: {platformNameString}, Device: {deviceNameString}, Driver: {driverVersionString}");
					break;
				case 1:
					Console.WriteLine($"Success - Version: {versionString}, Platform: {platformNameString}, Device: {deviceNameString}, Driver: {driverVersionString}");
					break;
				case 2:
					Console.WriteLine($"Failure - Version: {versionString}, Platform: {platformNameString}, Device: {deviceNameString}, Driver: {driverVersionString}");
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
