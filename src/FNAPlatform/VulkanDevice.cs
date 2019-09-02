#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2019 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */
#endregion

#region Using Statements
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SDL2;
#endregion

// FIXME: The environment variables need proper documentation

/* ==================================
 * VulkanDevice Environment Variables
 * ==================================
 *
 * FNA_VULKAN_ENABLE_RENDERDOC
 *	Enables the VK_LAYER_RENDERDOC_Capture validation layer.
 *	Set this to "1" to enable the layer.
 *	If the layer is not supported, a warning will be logged to the output.
 *
 * FNA_VULKAN_ENABLE_VALIDATION
 *	Enables the VK_EXT_debug_utils extension and VK_LAYER_KHRONOS_VALIDATION layer.
 *	This allows for extensive logging of Vulkan errors, warnings, and general info.
 *	Set this to "1" to enable standard validation logging.
 *	Set this to "2" for VERY VERBOSE validation logging that reports all driver activity.
 *	If the layer is not supported, a warning will be logged to the output.
 *
 */

namespace Microsoft.Xna.Framework.Graphics
{
	internal partial class VulkanDevice : IGLDevice
	{
		private IntPtr Instance;
		private ulong WindowSurface;
		private IntPtr PhysicalDevice;
		private IntPtr Device;
		private IntPtr GraphicsQueue;
		private IntPtr PresentationQueue;

		private uint graphicsQueueFamilyIndex;
		private uint presentationQueueFamilyIndex;

		private bool renderdocEnabled;
		private bool validationEnabled;
		private bool verboseValidationEnabled;
		private ulong DebugMessenger;

		#region Public Constructor

		public VulkanDevice(
			PresentationParameters presentationParameters,
			GraphicsAdapter adapter
		) {
			// Handle environment variables
			renderdocEnabled = Environment.GetEnvironmentVariable("FNA_VULKAN_ENABLE_RENDERDOC") == "1";
			string validationEnv = Environment.GetEnvironmentVariable("FNA_VULKAN_ENABLE_VALIDATION");
			validationEnabled = (validationEnv == "1" || validationEnv == "2");
			verboseValidationEnabled = (validationEnv == "2");

			// Initialize Vulkan
			LoadGlobalEntryPoints();
			CreateVulkanInstance(presentationParameters.DeviceWindowHandle);
			LoadInstanceEntryPoints();
			if (validationEnabled)
			{
				InitDebugMessenger();
			}
			CreateWindowSurface(presentationParameters.DeviceWindowHandle);
			SelectPhysicalDevice();
			CreateLogicalDevice();

			// Print GPU / driver info
			VkPhysicalDeviceProperties deviceProperties;
			vkGetPhysicalDeviceProperties(PhysicalDevice, out deviceProperties);

			string deviceName = GetDriverDeviceName(deviceProperties);
			string driverVersion = GetDriverVersionInfo(deviceProperties);
			string driverVendor = GetDriverVendorName(deviceProperties);

			FNALoggerEXT.LogInfo("IGLDevice: VulkanDevice");
			FNALoggerEXT.LogInfo("Vulkan Device: " + deviceName);
			FNALoggerEXT.LogInfo("Vulkan Driver: " + driverVersion);
			FNALoggerEXT.LogInfo("Vulkan Vendor: " + driverVendor);

			// Populate properties with device info
			MaxTextureSlots = (int) deviceProperties.limits.maxPerStageDescriptorSamplers;

			SupportsDxt1 = FormatSupported(
				VkFormat.VK_FORMAT_BC1_RGBA_UNORM_BLOCK,
				PhysicalDevice
			);

			SupportsS3tc = (
				SupportsDxt1 ||
				FormatSupported(VkFormat.VK_FORMAT_BC3_UNORM_BLOCK, PhysicalDevice) ||
				FormatSupported(VkFormat.VK_FORMAT_BC5_UNORM_BLOCK, PhysicalDevice)
			);

			SupportsHardwareInstancing = true;

			/* Check the max multisample count, override parameters if necessary */
			// FIXME: Is this right?
			int colorSamples = GetSampleCount(deviceProperties.limits.framebufferColorSampleCounts);
			int depthSamples = GetSampleCount(deviceProperties.limits.framebufferDepthSampleCounts);
			int maxSamples = Math.Min(colorSamples, depthSamples);
			MaxMultiSampleCount = maxSamples;
			presentationParameters.MultiSampleCount = Math.Min(
				presentationParameters.MultiSampleCount,
				MaxMultiSampleCount
			);

			// Create the swapchain / backbuffer
			Backbuffer = new VulkanBackbuffer(
				this,
				presentationParameters.BackBufferWidth,
				presentationParameters.BackBufferHeight,
				presentationParameters.DepthStencilFormat,
				presentationParameters.MultiSampleCount
			);
		}

		#endregion

		#region Dispose Method

		public void Dispose()
		{
			(Backbuffer as VulkanBackbuffer).Dispose();

			vkDestroySwapchainKHR(
				Device,
				(Backbuffer as VulkanBackbuffer).SwapchainHandle,
				IntPtr.Zero
			);
			vkDestroyDevice(
				Device,
				IntPtr.Zero
			);
			vkDestroyDebugUtilsMessengerEXT(
				Instance,
				DebugMessenger,
				IntPtr.Zero
			);
			vkDestroySurfaceKHR(
				Instance,
				WindowSurface,
				IntPtr.Zero
			);
			vkDestroyInstance(
				Instance,
				IntPtr.Zero
			);
		}

		#endregion

		#region Vulkan Initialization

		private unsafe void CreateVulkanInstance(IntPtr windowHandle)
		{
			// Describe app metadata
			string appName = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
			VkApplicationInfo appInfo = new VkApplicationInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
				pNext = IntPtr.Zero,
				pApplicationName = UTF8_ToNative(appName),
				applicationVersion = VK_MAKE_VERSION(1, 0, 0), // FIXME: Should this be automatically set?
				pEngineName = UTF8_ToNative("FNA"),
				engineVersion = VK_MAKE_VERSION(19, 9, 0), // FIXME: What version should go here?
				apiVersion = VK_MAKE_VERSION(1, 0, 0),
			};

			// Get all available instance extensions
			uint availableExtensionCount;
			vkEnumerateInstanceExtensionProperties(null, out availableExtensionCount, null);
			VkExtensionProperties[] availableExtensions = new VkExtensionProperties[availableExtensionCount];
			fixed (VkExtensionProperties* availableExtensionsPtr = availableExtensions)
			{
				vkEnumerateInstanceExtensionProperties(
					null,
					out availableExtensionCount,
					availableExtensionsPtr
				);
			}

			// Generate a list of all instance extensions we will use
			List<IntPtr> extensions = new List<IntPtr>();

			// SDL2 extensions
			uint sdlExtCount;
			SDL.SDL_Vulkan_GetInstanceExtensions(windowHandle, out sdlExtCount, null);
			IntPtr[] sdlExtensions = new IntPtr[sdlExtCount];
			SDL.SDL_Vulkan_GetInstanceExtensions(windowHandle, out sdlExtCount, sdlExtensions);
			extensions.AddRange(sdlExtensions);

			// Debug utility extensions
			if (validationEnabled)
			{
				string debugUtilsExt = "VK_EXT_debug_utils";
				if (ExtensionSupported(debugUtilsExt, availableExtensions))
				{
					extensions.Add(UTF8_ToNative(debugUtilsExt));
				}
				else
				{
					validationEnabled = false;
					FNALoggerEXT.LogWarn(debugUtilsExt + " not supported!");
				}
			}

			// Get all available validation layers
			uint availableLayerCount;
			vkEnumerateInstanceLayerProperties(out availableLayerCount, null);
			VkLayerProperties[] availableLayers = new VkLayerProperties[availableLayerCount];
			fixed (VkLayerProperties* availableLayersPtr = availableLayers)
			{
				vkEnumerateInstanceLayerProperties(
					out availableLayerCount,
					availableLayersPtr
				);
			}

			// Generate a list of all validation layers we will use
			List<IntPtr> layers = new List<IntPtr>();
			if (renderdocEnabled)
			{
				string renderdocLayerName = "VK_LAYER_RENDERDOC_Capture";
				if (LayerSupported(renderdocLayerName, availableLayers))
				{
					layers.Add(UTF8_ToNative(renderdocLayerName));
				}
				else
				{
					FNALoggerEXT.LogWarn(renderdocLayerName + " not supported!");
				}
			}
			if (validationEnabled)
			{
				/* No need to check if the layer is supported.
				 * If this code is reached, VK_EXT_debug_utils
				 * exists so the layer must as well.
				 * -caleb
				 */
				layers.Add(UTF8_ToNative("VK_LAYER_KHRONOS_validation"));
			}

			// Validate the instance creation/destruction, if needed
			VkDebugUtilsMessengerCreateInfoEXT debugCreateInfo;
			IntPtr pNext = IntPtr.Zero;
			if (validationEnabled)
			{
				debugCreateInfo = CreateDebugMessengerCreateInfo();
				pNext = (IntPtr) (&debugCreateInfo);
			}

			// Create the Vulkan instance
			VkInstanceCreateInfo appCreateInfo;
			IntPtr[] extensionsArray = extensions.ToArray();
			IntPtr[] layersArray = layers.ToArray();
			fixed (IntPtr* extNamesPtr = extensionsArray)
			{
				fixed (IntPtr* layerNamesPtr = layersArray)
				{
					appCreateInfo = new VkInstanceCreateInfo
					{
						sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
						pNext = pNext,
						flags = 0,
						pApplicationInfo = &appInfo,
						enabledLayerCount = (uint) layersArray.Length,
						ppEnabledLayerNames = layerNamesPtr,
						enabledExtensionCount = (uint) extensionsArray.Length,
						ppEnabledExtensionNames = extNamesPtr
					};
				}
			}
			VkResult res = vkCreateInstance(&appCreateInfo, IntPtr.Zero, out Instance);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception("Could not create Vulkan Instance! Error: " + res);
			}
		}

		private unsafe void InitDebugMessenger()
		{
			VkDebugUtilsMessengerCreateInfoEXT createInfo = CreateDebugMessengerCreateInfo();
			VkResult res = vkCreateDebugUtilsMessengerEXT(
				Instance,
				&createInfo,
				IntPtr.Zero,
				out DebugMessenger
			);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception("Could not initialize debug messenger! Error: " + res);
			}
		}

		private unsafe uint DebugCallback (
			VkDebugUtilsMessageSeverityFlagBitsEXT messageSeverity,
			VkDebugUtilsMessageTypeFlagBitsEXT messageType,
			VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
			IntPtr pUserData
		) {
			string message = UTF8_ToManaged(pCallbackData->pMessage);

			if (messageSeverity == VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT)
			{
				// This is serious, so throw an exception.
				throw new Exception("Vulkan Error: " + message);
			}
			else if (messageSeverity == VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT)
			{
				FNALoggerEXT.LogWarn("WARNING: " + message);
			}
			else
			{
				FNALoggerEXT.LogInfo("INFO: " + message);
			}

			return 0;
		}

		private void CreateWindowSurface(IntPtr window)
		{
			SDL.SDL_bool result = SDL.SDL_Vulkan_CreateSurface(
				window,
				Instance,
				out WindowSurface
			);
			if (result == 0)
			{
				throw new Exception("Could not create Vulkan window surface!");
			}
		}

		private unsafe void SelectPhysicalDevice()
		{
			// Get all physical devices
			uint physicalDeviceCount;
			vkEnumeratePhysicalDevices(Instance, out physicalDeviceCount, null);
			IntPtr[] physicalDevices = new IntPtr[physicalDeviceCount];
			fixed (IntPtr* physicalDevicesPtr = physicalDevices)
			{
				vkEnumeratePhysicalDevices(Instance, out physicalDeviceCount, physicalDevicesPtr);
			}

			/* To find the ideal physical device, each GPU is assigned
			 * a score based on its properties and features. The device
			 * with the most points "wins" and becomes our PhysicalDevice.
			 */
			int[] scores = new int[physicalDeviceCount];

			/* We also need to remember the location of each GPU's
			 * first graphics queue family and presentation queue
			 * family. These are used when creating a logical device.
			 */
			int[] graphicsQueueFamilyIndices = new int[physicalDeviceCount];
			int[] presentationQueueFamilyIndices = new int[physicalDeviceCount];

			// Begin the competition for the best physical device!
			for (int i = 0; i < physicalDeviceCount; i += 1)
			{
				// Get all queue families supported by this device
				uint queueFamilyCount;
				vkGetPhysicalDeviceQueueFamilyProperties(
					physicalDevices[i],
					out queueFamilyCount,
					null
				);
				VkQueueFamilyProperties[] queueFamilies = new VkQueueFamilyProperties[queueFamilyCount];
				fixed (VkQueueFamilyProperties* queueFamiliesPtr = queueFamilies)
				{
					vkGetPhysicalDeviceQueueFamilyProperties(
						physicalDevices[i],
						out queueFamilyCount,
						queueFamiliesPtr
					);
				}

				/* A suitable physical device MUST have...
				 * 1. Support for the VK_KHR_swapchain extension
				 * 2. Support for the RGBA8_UNORM surface format
				 * 2. At least one graphics queue family
				 * 3. At least one queue family that supports presentation
				 */
				bool supportsSwapchainExt = ExtensionSupported(
					"VK_KHR_swapchain",
					GetAllDeviceExtensions(physicalDevices[i])
				);

				bool supportsRGBA8 = SurfaceFormatSupported(
					VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
					VkColorSpaceKHR.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR,
					physicalDevices[i],
					WindowSurface
				);

				graphicsQueueFamilyIndices[i] = -1;
				presentationQueueFamilyIndices[i] = -1;

				for (int j = 0; j < queueFamilies.Length; j += 1)
				{
					if (!supportsSwapchainExt || !supportsRGBA8)
					{
						// This GPU's a dud, let's bail.
						break;
					}

					if (graphicsQueueFamilyIndices[i] == -1)
					{
						if (QueueFamilySupportsGraphics(queueFamilies[j]))
						{
							// This queue family supports graphics!
							graphicsQueueFamilyIndices[i] = j;
						}
					}

					if (presentationQueueFamilyIndices[i] == -1)
					{
						if (QueueFamilySupportsPresentation(physicalDevices[i], j))
						{
							// This queue family supports presentation!
							presentationQueueFamilyIndices[i] = j;
						}
					}
				}
				if (graphicsQueueFamilyIndices[i] == -1 || presentationQueueFamilyIndices[i] == -1)
				{
					// This GPU is useless to us. Skip it!
					scores[i] = int.MinValue;
					continue;
				}

				// Score the device properties and features
				VkPhysicalDeviceProperties deviceProperties;
				vkGetPhysicalDeviceProperties(
					physicalDevices[i],
					out deviceProperties
				);

				// discrete GPU > integrated GPU > anything else
				if (deviceProperties.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU)
				{
					scores[i] += 100;
				}
				else if (deviceProperties.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU)
				{
					scores[i] += 50;
				}

				// FIXME: Other GPU properties to check?

				VkPhysicalDeviceFeatures deviceFeatures;
				vkGetPhysicalDeviceFeatures(
					physicalDevices[i],
					out deviceFeatures
				);

				/* FIXME: What features should we check for?
				 * And if they exist, we should store them for logical device creation.
				 * -caleb
				 */
			}

			// Determine the winner
			int bestScore = -1;
			for (int i = 0; i < physicalDeviceCount; i += 1)
			{
				if (scores[i] > bestScore)
				{
					bestScore = scores[i];
					PhysicalDevice = physicalDevices[i];
					graphicsQueueFamilyIndex = (uint) graphicsQueueFamilyIndices[i];
					presentationQueueFamilyIndex = (uint) presentationQueueFamilyIndices[i];
				}
			}
			if (bestScore == -1)
			{
				throw new NoSuitableGraphicsDeviceException("No Vulkan compatible GPUs detected!");
			}
		}

		private unsafe void CreateLogicalDevice()
		{
			/* Check if the graphics queue family and the presentation
			 * queue family are the same. If so, we can generate one
			 * queue and use it for both purposes. If not, we'll need
			 * to make a second queue just for presenting.
			 */
			bool differentPresentationQueue = (graphicsQueueFamilyIndex != presentationQueueFamilyIndex);
			uint queueCount = (differentPresentationQueue) ? 2u : 1u;

			// Create a list of queue CreateInfo's
			VkDeviceQueueCreateInfo[] queueCreateInfos = new VkDeviceQueueCreateInfo[queueCount];

			// Create the graphics queue CreateInfo
			float priority = 1.0f;
			VkDeviceQueueCreateInfo graphicsQueueCreateInfo = new VkDeviceQueueCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
				pNext = IntPtr.Zero,
				flags = 0,
				queueCount = 1,
				pQueuePriorities = &priority,
				queueFamilyIndex = graphicsQueueFamilyIndex
			};
			queueCreateInfos[0] = graphicsQueueCreateInfo;

			// Create the presentation queue CreateInfo, if needed
			if (differentPresentationQueue)
			{
				VkDeviceQueueCreateInfo presentationQueueCreateInfo = new VkDeviceQueueCreateInfo
				{
					sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
					pNext = IntPtr.Zero,
					flags = 0,
					queueCount = 1,
					pQueuePriorities = &priority,
					queueFamilyIndex = presentationQueueFamilyIndex
				};
				queueCreateInfos[1] = presentationQueueCreateInfo;
			}

			// List all the device extensions we want to use.
			// FIXME: This will probably need to be revisited.
			IntPtr[] extensionNames =
			{
				UTF8_ToNative("VK_KHR_swapchain")
			};

			// Prepare for device creation
			VkDeviceCreateInfo deviceCreateInfo;
			fixed (VkDeviceQueueCreateInfo* queueCreateInfosPtr = queueCreateInfos)
			{
				fixed (IntPtr* extensionNamesPtr = extensionNames)
				{
					deviceCreateInfo = new VkDeviceCreateInfo
					{
						sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
						pNext = IntPtr.Zero,
						flags = 0,
						queueCreateInfoCount = queueCount,
						pQueueCreateInfos = queueCreateInfosPtr,

						// FIXME: Should these be the same as instance layers?
						enabledLayerCount = 0,
						ppEnabledLayerNames = null,

						enabledExtensionCount = (uint) extensionNames.Length,
						ppEnabledExtensionNames = extensionNamesPtr,

						// FIXME: Which device features do we want?
						pEnabledFeatures = null
					};
				}
			}

			// Create the device!
			vkCreateDevice(PhysicalDevice, &deviceCreateInfo, IntPtr.Zero, out Device);

			// Store handles to the graphics and presentation queues
			vkGetDeviceQueue(Device, graphicsQueueFamilyIndex, 0, out GraphicsQueue);
			vkGetDeviceQueue(Device, presentationQueueFamilyIndex, 0, out PresentationQueue);
		}

		#endregion

		#region Private Vulkan Helper Methods

		private string GetDriverDeviceName(VkPhysicalDeviceProperties properties)
		{
			string name;
			unsafe
			{
				name = UTF8_ToManaged((IntPtr) properties.deviceName);
			}
			return name;
		}

		private string GetDriverVersionInfo(VkPhysicalDeviceProperties properties)
		{
			return	VK_GetVersionString(properties.apiVersion) +
				" - Version " +
				properties.driverVersion.ToString();
		}

		private string GetDriverVendorName(VkPhysicalDeviceProperties properties)
		{
			switch (properties.vendorID)
			{
				case (0x1002):
					return "AMD";
				case (0x1010):
					return "ImgTec";
				case (0x10DE):
					return "NVIDIA";
				case (0x13B5):
					return "ARM";
				case (0x5143):
					return "Qualcomm";
				case (0x8086):
					return "Intel";
				default:
					return "Unknown";
			}
		}

		private int GetSampleCount(VkSampleCountFlagBits flags)
		{
			if ((flags & VkSampleCountFlagBits.VK_SAMPLE_COUNT_64_BIT) != 0)
			{
				return 64;
			}
			else if ((flags & VkSampleCountFlagBits.VK_SAMPLE_COUNT_32_BIT) != 0)
			{
				return 32;
			}
			else if ((flags & VkSampleCountFlagBits.VK_SAMPLE_COUNT_16_BIT) != 0)
			{
				return 16;
			}
			else if ((flags & VkSampleCountFlagBits.VK_SAMPLE_COUNT_8_BIT) != 0)
			{
				return 8;
			}
			else if ((flags & VkSampleCountFlagBits.VK_SAMPLE_COUNT_4_BIT) != 0)
			{
				return 4;
			}
			else if ((flags & VkSampleCountFlagBits.VK_SAMPLE_COUNT_2_BIT) != 0)
			{
				return 2;
			}
			else if ((flags & VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT) != 0)
			{
				return 1;
			}

			return 0;
		}

		private VkSampleCountFlagBits GetSampleCountFlags(int samples)
		{
			if (samples >= 64)
			{
				return VkSampleCountFlagBits.VK_SAMPLE_COUNT_64_BIT;
			}
			else if (samples >= 32)
			{
				return VkSampleCountFlagBits.VK_SAMPLE_COUNT_32_BIT;
			}
			else if (samples >= 16)
			{
				return VkSampleCountFlagBits.VK_SAMPLE_COUNT_16_BIT;
			}
			else if (samples >= 8)
			{
				return VkSampleCountFlagBits.VK_SAMPLE_COUNT_8_BIT;
			}
			else if (samples >= 4)
			{
				return VkSampleCountFlagBits.VK_SAMPLE_COUNT_4_BIT;
			}
			else if (samples >= 2)
			{
				return VkSampleCountFlagBits.VK_SAMPLE_COUNT_2_BIT;
			}
			else if (samples == 1)
			{
				return VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT;
			}

			return 0;
		}

		private bool FormatSupported(
			VkFormat format,
			IntPtr physicalDevice
		) {
			VkFormatProperties formatProperties;
			vkGetPhysicalDeviceFormatProperties(
				physicalDevice,
				format,
				out formatProperties
			);
			return	formatProperties.optimalTilingFeatures != 0 ||
				formatProperties.linearTilingFeatures != 0 ||
				formatProperties.bufferFeatures == 0;
		}

		private unsafe bool SurfaceFormatSupported(
			VkFormat format,
			VkColorSpaceKHR colorSpace,
			IntPtr physicalDevice,
			ulong surface
		) {
			// Get all supported format+colorspace combinations
			uint numFormats;
			vkGetPhysicalDeviceSurfaceFormatsKHR(
				physicalDevice,
				surface,
				out numFormats,
				null
			);
			VkSurfaceFormatKHR[] formats = new VkSurfaceFormatKHR[numFormats];
			fixed (VkSurfaceFormatKHR* formatsPtr = formats)
			{
				vkGetPhysicalDeviceSurfaceFormatsKHR(
					physicalDevice,
					surface,
					out numFormats,
					formatsPtr
				);
			}

			// Check if there's a match
			foreach (VkSurfaceFormatKHR surfaceFormat in formats)
			{
				if (	surfaceFormat.format == format &&
					surfaceFormat.colorSpace == colorSpace	)
				{
					return true;
				}
			}

			return false;
		}

		private unsafe bool ExtensionSupported(
			string extName,
			VkExtensionProperties[] extensions
		) {
			foreach (VkExtensionProperties ext in extensions)
			{
				if (UTF8_ToManaged((IntPtr) ext.extensionName) == extName)
				{
					return true;
				}
			}

			return false;
		}

		private unsafe bool LayerSupported(
			string layerName,
			VkLayerProperties[] layers
		) {
			foreach (VkLayerProperties layer in layers)
			{
				if (UTF8_ToManaged((IntPtr) layer.layerName) == layerName)
				{
					return true;
				}
			}

			return false;
		}

		private unsafe VkExtensionProperties[] GetAllDeviceExtensions(IntPtr physicalDevice)
		{
			uint extensionCount;
			vkEnumerateDeviceExtensionProperties(
				physicalDevice,
				IntPtr.Zero,
				out extensionCount,
				null
			);
			VkExtensionProperties[] extensions = new VkExtensionProperties[extensionCount];
			fixed (VkExtensionProperties* extptr = extensions)
			{
				vkEnumerateDeviceExtensionProperties(
					physicalDevice,
					IntPtr.Zero,
					out extensionCount,
					extptr
				);
			}

			return extensions;
		}

		private unsafe VkDebugUtilsMessengerCreateInfoEXT CreateDebugMessengerCreateInfo()
		{
			VkDebugUtilsMessageSeverityFlagBitsEXT severityFlags =
				VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT |
				VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT |
				VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_VERBOSE_BIT_EXT;

			if (verboseValidationEnabled)
			{
				/* This will spew a TON of crap into the output.
				 * Some of it is useful, most of it not so much.
				 * -caleb
				 */
				severityFlags |= VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_INFO_BIT_EXT;
			}

			VkDebugUtilsMessageTypeFlagBitsEXT messageFlags =
				VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT |
				VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT |
				VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT;

			return new VkDebugUtilsMessengerCreateInfoEXT
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_UTILS_MESSENGER_CREATE_INFO_EXT,
				pNext = IntPtr.Zero,
				flags = 0,
				messageSeverity = severityFlags,
				messageType = messageFlags,
				pfnUserCallback = Marshal.GetFunctionPointerForDelegate(
					(PFN_vkDebugUtilsMessengerCallbackEXT) DebugCallback
				),
				pUserData = IntPtr.Zero
			};
		}

		private bool QueueFamilySupportsGraphics(VkQueueFamilyProperties family)
		{
			return (family.queueFlags & VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT) != 0;
		}

		private bool QueueFamilySupportsPresentation(
			IntPtr physicalDevice,
			int queueFamilyIndex
		) {
			uint supportsPresentation = 0;
			VkResult res = vkGetPhysicalDeviceSurfaceSupportKHR(
				physicalDevice,
				(uint) queueFamilyIndex,
				WindowSurface,
				out supportsPresentation
			);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception(
					"Could not query queue family presentation capability! Error: " + res
				);
			}
			return (supportsPresentation == 1);
		}

		#endregion

		public Color BlendFactor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public int MultiSampleMask { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public int ReferenceStencil { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		#region Vulkan Device Capabilities

		public bool SupportsDxt1
		{
			get;
			private set;
		}

		public bool SupportsS3tc
		{
			get;
			private set;
		}

		public bool SupportsHardwareInstancing
		{
			get;
			private set;
		}

		public int MaxTextureSlots
		{
			get;
			private set;
		}

		public int MaxMultiSampleCount
		{
			get;
			private set;
		}

		#endregion

		#region Viewport State Variables

		private Rectangle scissorRectangle = new Rectangle();
		private Rectangle viewport = new Rectangle();
		private float depthRangeMin = 0.0f;
		private float depthRangeMax = 1.0f;

		#endregion

		#region State Management Methods

		public void SetPresentationInterval(PresentInterval presentInterval)
		{
			/* No-op. Vulkan requires you to specify the
			 * VkPresentModeKHR at swapchain creation time,
			 * so we handle this when resetting the backbuffer.
			 * -caleb
			 */
		}

		public void SetViewport(Viewport vp)
		{
			if (vp.Bounds != viewport)
			{
				viewport = vp.Bounds;
				// FIXME: vkCmdSetViewport(CommandBuffer, 0, 1, <VkViewport representation of vp>)
			}

			if (vp.MinDepth != depthRangeMin || vp.MaxDepth != depthRangeMax)
			{
				depthRangeMin = vp.MinDepth;
				depthRangeMax = vp.MaxDepth;
				// FIXME: Update DepthStencilState in the pipeline
			}
		}

		public void SetScissorRect(Rectangle scissorRect)
		{
			if (scissorRect != scissorRectangle)
			{
				scissorRectangle = scissorRect;
				// FIXME: vkCmdSetScissor(CommandBuffer, 0, 1, <ptr to rect>
			}
		}

		public void SetBlendState(BlendState blendState)
		{
			throw new NotImplementedException();
		}

		public void SetDepthStencilState(DepthStencilState depthStencilState)
		{
			throw new NotImplementedException();
		}

		public void ApplyRasterizerState(RasterizerState rasterizerState)
		{
			throw new NotImplementedException();
		}

		public void VerifySampler(int index, Texture texture, SamplerState sampler)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region Vulkan Renderbuffer Container Class

		private class VulkanRenderbuffer : IGLRenderbuffer
		{

		}

		#endregion

		#region Faux-Backbuffer and Swapchain

		public IGLBackbuffer Backbuffer
		{
			get;
			private set;
		}

		private class VulkanBackbuffer : IGLBackbuffer
		{
			public ulong SwapchainHandle
			{
				get
				{
					return swapchainHandle;
				}
			}

			// FIXME: Do these need to be stored at all?
			public ulong[] SwapchainImages
			{
				get
				{
					return swapchainImages;
				}
			}

			public ulong[] SwapchainImageViews
			{
				get
				{
					return swapchainImageViews;
				}
			}

			public int Width
			{
				get;
				private set;
			}

			public int Height
			{
				get;
				private set;
			}

			public DepthFormat DepthFormat
			{
				get;
				private set;
			}

			public int MultiSampleCount
			{
				get;
				private set;
			}

			private VulkanDevice vkDevice;
			private VkPresentModeKHR[] supportedPresentModes;
			private ulong swapchainHandle;
			private ulong[] swapchainImages;
			private ulong[] swapchainImageViews;

			public VulkanBackbuffer(
				VulkanDevice device,
				int width,
				int height,
				DepthFormat depthFormat,
				int multiSampleCount
			) {
				Width = width;
				Height = height;

				DepthFormat = depthFormat;
				MultiSampleCount = multiSampleCount;
				vkDevice = device;

				// Cache an array of supported VkPresentModeKHRs
				unsafe
				{
					uint numPresentModes;
					vkDevice.vkGetPhysicalDeviceSurfacePresentModesKHR(
						vkDevice.PhysicalDevice,
						vkDevice.WindowSurface,
						out numPresentModes,
						null
					);
					supportedPresentModes = new VkPresentModeKHR[numPresentModes];
					fixed (VkPresentModeKHR* presentModesPtr = supportedPresentModes)
					{
						vkDevice.vkGetPhysicalDeviceSurfacePresentModesKHR(
							vkDevice.PhysicalDevice,
							vkDevice.WindowSurface,
							out numPresentModes,
							presentModesPtr
						);
					}
				}
			}

			private bool PresentModeSupported(VkPresentModeKHR mode)
			{
				foreach (VkPresentModeKHR presentMode in supportedPresentModes)
				{
					if (presentMode == mode)
					{
						return true;
					}
				}

				return false;
			}

			public unsafe void ResetFramebuffer(
				PresentationParameters presentationParameters
			) {
				/* In Vulkan, resetting the backbuffer framebuffer means
				 * creating a new swapchain and a new depth-stencil buffer
				 * with updated image properties (size/format/etc.).
				 * 
				 * FIXME: Add depth-stencil buffer
				 */

				// FIXME: Need to handle detatching/freeing old resources

				// Fill out basic properties of the swapchain
				VkSwapchainCreateInfoKHR createInfo = new VkSwapchainCreateInfoKHR();
				createInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR;
				createInfo.pNext = IntPtr.Zero;
				createInfo.flags = 0;
				createInfo.surface = vkDevice.WindowSurface;

				// Retrieve the surface capabilities
				VkSurfaceCapabilitiesKHR surfaceCapabilities;
				vkDevice.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(
					vkDevice.PhysicalDevice,
					vkDevice.WindowSurface,
					out surfaceCapabilities
				);

				// Determine how many images to request
				uint imageCount = surfaceCapabilities.minImageCount + 1;
				if (surfaceCapabilities.maxImageCount > 0 &&
					imageCount > surfaceCapabilities.maxImageCount)
				{
					imageCount = surfaceCapabilities.maxImageCount;
				}
				createInfo.minImageCount = imageCount;

				// Specify the swapchain image format
				createInfo.imageFormat = VkFormat.VK_FORMAT_R8G8B8A8_UNORM;
				createInfo.imageColorSpace = VkColorSpaceKHR.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR;

				// Bound the size of swapchain images to the surface's supported limits
				int imageWidth = MathHelper.Clamp(
					Width,
					(int) surfaceCapabilities.minImageExtent.width,
					(int) surfaceCapabilities.maxImageExtent.width
				);
				int imageHeight = MathHelper.Clamp(
					Height,
					(int) surfaceCapabilities.maxImageExtent.height,
					(int) surfaceCapabilities.minImageExtent.height
				);
				createInfo.imageExtent = new VkExtent2D(
					(uint) imageWidth,
					(uint) imageHeight
				);

				// Update our Width and Height variables to match
				Width = imageWidth;
				Height = imageHeight;

				/* FIXME: From the docs...
				 * 
				 * On some platforms, it is normal that maxImageExtent may become (0, 0),
				 * for example when the window is minimized. In such a case, it is not
				 * possible to create a swapchain due to the Valid Usage requirements.
				 * https://www.khronos.org/registry/vulkan/specs/1.1-extensions/man/html/VkSwapchainCreateInfoKHR.html
				 * 
				 * Should we do anything special to handle this case?
				 * -caleb
				 */

				// Define how the images will be used
				createInfo.imageArrayLayers = 1;
				createInfo.imageUsage = (
					VkImageUsageFlagBits.VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT |
					VkImageUsageFlagBits.VK_IMAGE_USAGE_TRANSFER_DST_BIT
				);

				// Define concurrency details (if needed)
				if (vkDevice.graphicsQueueFamilyIndex != vkDevice.presentationQueueFamilyIndex)
				{
					createInfo.imageSharingMode = VkSharingMode.VK_SHARING_MODE_CONCURRENT;
					createInfo.queueFamilyIndexCount = 2;

					uint[] indices =
					{
						vkDevice.graphicsQueueFamilyIndex,
						vkDevice.presentationQueueFamilyIndex
					};
					fixed (uint* indicesPtr = indices)
					{
						// FIXME: This doesn't seem right...
						createInfo.pQueueFamilyIndices = indicesPtr;
					}
				}
				else
				{
					createInfo.imageSharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
					createInfo.queueFamilyIndexCount = 0;
					createInfo.pQueueFamilyIndices = null;
				}

				// Transform is same as the device's current transform
				createInfo.preTransform = surfaceCapabilities.currentTransform;

				// Make the surface opaque
				createInfo.compositeAlpha = VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;

				// Check if the requested present interval is supported
				PresentInterval presentInterval = presentationParameters.PresentationInterval;
				VkPresentModeKHR presentMode = XNAToVK.PresentMode[(int)presentInterval];
				if (PresentModeSupported(presentMode))
				{
					createInfo.presentMode = presentMode;
				}
				else
				{
					// Fall back to vsync. This is always available.
					createInfo.presentMode = VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR;
				}

				// Clip stuff outside the visible area
				// FIXME: This will interfere with readback
				createInfo.clipped = 1;

				// Out with the old...
				createInfo.oldSwapchain = swapchainHandle;

				// ...and in with the new!
				VkResult res = vkDevice.vkCreateSwapchainKHR(
					vkDevice.Device,
					&createInfo,
					IntPtr.Zero,
					out swapchainHandle
				);
				if (res != VkResult.VK_SUCCESS)
				{
					throw new Exception("Could not generate swapchain! Error: " + res);
				}

				// Store the images created by the new swapchain
				CreateImageViews();
			}

			private unsafe void CreateImageViews()
			{
				// Create an array of swapchain images
				VkResult res;
				uint numSwapchainImages;
				vkDevice.vkGetSwapchainImagesKHR(
					vkDevice.Device,
					SwapchainHandle,
					out numSwapchainImages,
					null
				);
				swapchainImages = new ulong[numSwapchainImages];
				fixed (ulong* swapchainImagesPtr = swapchainImages)
				{
					res = vkDevice.vkGetSwapchainImagesKHR(
						vkDevice.Device,
						SwapchainHandle,
						out numSwapchainImages,
						swapchainImagesPtr
					);
				}
				if (res != VkResult.VK_SUCCESS)
				{
					throw new Exception("Could not retrieve swapchain images! Error: " + res);
				}

				// Create image views to access the images
				swapchainImageViews = new ulong[numSwapchainImages];
				for (int i = 0; i < numSwapchainImages; i += 1)
				{
					VkImageViewCreateInfo createInfo = new VkImageViewCreateInfo
					{
						sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
						pNext = IntPtr.Zero,
						flags = 0,
						image = swapchainImages[i],
						viewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
						format = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
						components = VkComponentMapping.Identity,
						subresourceRange = new VkImageSubresourceRange
						{
							aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
							baseMipLevel = 0,
							levelCount = 1,
							baseArrayLayer = 0,
							layerCount = 1
						}
					};

					res = vkDevice.vkCreateImageView(
						vkDevice.Device,
						&createInfo,
						IntPtr.Zero,
						out swapchainImageViews[i]
					);
					if (res != VkResult.VK_SUCCESS)
					{
						throw new Exception("Could not create image view! Error: " + res);
					}
				}
			}

			public void Dispose()
			{
				// Destroy all image views
				foreach (ulong imageView in SwapchainImageViews)
				{
					vkDevice.vkDestroyImageView(
						vkDevice.Device,
						imageView,
						IntPtr.Zero
					);
				}
				swapchainImageViews = null;
			}
		}

		#endregion

		public void AddDisposeEffect(IGLEffect effect)
		{
			throw new NotImplementedException();
		}

		public void AddDisposeIndexBuffer(IGLBuffer buffer)
		{
			throw new NotImplementedException();
		}

		public void AddDisposeQuery(IGLQuery query)
		{
			throw new NotImplementedException();
		}

		public void AddDisposeRenderbuffer(IGLRenderbuffer renderbuffer)
		{
			throw new NotImplementedException();
		}

		public void AddDisposeTexture(IGLTexture texture)
		{
			throw new NotImplementedException();
		}

		public void AddDisposeVertexBuffer(IGLBuffer buffer)
		{
			throw new NotImplementedException();
		}

		public void ApplyEffect(IGLEffect effect, IntPtr technique, uint pass, IntPtr stateChanges)
		{
			throw new NotImplementedException();
		}

		public void ApplyVertexAttributes(VertexBufferBinding[] bindings, int numBindings, bool bindingsUpdated, int baseVertex)
		{
			throw new NotImplementedException();
		}

		public void ApplyVertexAttributes(VertexDeclaration vertexDeclaration, IntPtr ptr, int vertexOffset)
		{
			throw new NotImplementedException();
		}

		public void BeginPassRestore(IGLEffect effect, IntPtr stateChanges)
		{
			throw new NotImplementedException();
		}

		public void Clear(ClearOptions options, Vector4 color, float depth, int stencil)
		{
			//Console.WriteLine("CLEAR!");
			throw new NotImplementedException();
		}

		public IGLEffect CloneEffect(IGLEffect effect)
		{
			throw new NotImplementedException();
		}

		public IGLEffect CreateEffect(byte[] effectCode)
		{
			throw new NotImplementedException();
		}

		public IGLQuery CreateQuery()
		{
			throw new NotImplementedException();
		}

		public IGLTexture CreateTexture2D(SurfaceFormat format, int width, int height, int levelCount)
		{
			throw new NotImplementedException();
		}

		public IGLTexture CreateTexture3D(SurfaceFormat format, int width, int height, int depth, int levelCount)
		{
			throw new NotImplementedException();
		}

		public IGLTexture CreateTextureCube(SurfaceFormat format, int size, int levelCount)
		{
			throw new NotImplementedException();
		}

		public void DrawIndexedPrimitives(PrimitiveType primitiveType, int baseVertex, int minVertexIndex, int numVertices, int startIndex, int primitiveCount, IndexBuffer indices)
		{
			throw new NotImplementedException();
		}

		public void DrawInstancedPrimitives(PrimitiveType primitiveType, int baseVertex, int minVertexIndex, int numVertices, int startIndex, int primitiveCount, int instanceCount, IndexBuffer indices)
		{
			throw new NotImplementedException();
		}

		public void DrawPrimitives(PrimitiveType primitiveType, int vertexStart, int primitiveCount)
		{
			throw new NotImplementedException();
		}

		public void DrawUserIndexedPrimitives(PrimitiveType primitiveType, IntPtr vertexData, int vertexOffset, int numVertices, IntPtr indexData, int indexOffset, IndexElementSize indexElementSize, int primitiveCount)
		{
			throw new NotImplementedException();
		}

		public void DrawUserPrimitives(PrimitiveType primitiveType, IntPtr vertexData, int vertexOffset, int primitiveCount)
		{
			throw new NotImplementedException();
		}

		public void EndPassRestore(IGLEffect effect)
		{
			throw new NotImplementedException();
		}

		public IGLBuffer GenIndexBuffer(bool dynamic, int indexCount, IndexElementSize indexElementSize)
		{
			throw new NotImplementedException();
		}

		public IGLRenderbuffer GenRenderbuffer(int width, int height, SurfaceFormat format, int multiSampleCount)
		{
			throw new NotImplementedException();
		}

		public IGLRenderbuffer GenRenderbuffer(int width, int height, DepthFormat format, int multiSampleCount)
		{
			throw new NotImplementedException();
		}

		public IGLBuffer GenVertexBuffer(bool dynamic, int vertexCount, int vertexStride)
		{
			throw new NotImplementedException();
		}

		public void GetIndexBufferData(IGLBuffer buffer, int offsetInBytes, IntPtr data, int startIndex, int elementCount, int elementSizeInBytes)
		{
			throw new NotImplementedException();
		}

		public void GetTextureData2D(IGLTexture texture, SurfaceFormat format, int width, int height, int level, int subX, int subY, int subW, int subH, IntPtr data, int startIndex, int elementCount, int elementSizeInBytes)
		{
			throw new NotImplementedException();
		}

		public void GetTextureData3D(IGLTexture texture, SurfaceFormat format, int left, int top, int front, int right, int bottom, int back, int level, IntPtr data, int startIndex, int elementCount, int elementSizeInBytes)
		{
			throw new NotImplementedException();
		}

		public void GetTextureDataCube(IGLTexture texture, SurfaceFormat format, int size, CubeMapFace cubeMapFace, int level, int subX, int subY, int subW, int subH, IntPtr data, int startIndex, int elementCount, int elementSizeInBytes)
		{
			throw new NotImplementedException();
		}

		public void GetVertexBufferData(IGLBuffer buffer, int offsetInBytes, IntPtr data, int startIndex, int elementCount, int elementSizeInBytes, int vertexStride)
		{
			throw new NotImplementedException();
		}

		public void QueryBegin(IGLQuery query)
		{
			throw new NotImplementedException();
		}

		public bool QueryComplete(IGLQuery query)
		{
			throw new NotImplementedException();
		}

		public void QueryEnd(IGLQuery query)
		{
			throw new NotImplementedException();
		}

		public int QueryPixelCount(IGLQuery query)
		{
			throw new NotImplementedException();
		}

		public void ReadBackbuffer(IntPtr data, int dataLen, int startIndex, int elementCount, int elementSizeInBytes, int subX, int subY, int subW, int subH)
		{
			throw new NotImplementedException();
		}

		public void ResetBackbuffer(
			PresentationParameters presentationParameters,
			GraphicsAdapter adapter
		) {
			Backbuffer.ResetFramebuffer(presentationParameters);
		}

		public void ResolveTarget(RenderTargetBinding target)
		{
			throw new NotImplementedException();
		}

		public void SetIndexBufferData(IGLBuffer buffer, int offsetInBytes, IntPtr data, int dataLength, SetDataOptions options)
		{
			throw new NotImplementedException();
		}

		public void SetRenderTargets(RenderTargetBinding[] renderTargets, IGLRenderbuffer renderbuffer, DepthFormat depthFormat)
		{
			throw new NotImplementedException();
		}

		public void SetStringMarker(string text)
		{
			throw new NotImplementedException();
		}

		public void SetTextureData2D(IGLTexture texture, SurfaceFormat format, int x, int y, int w, int h, int level, IntPtr data, int dataLength)
		{
			throw new NotImplementedException();
		}

		public void SetTextureDataYUV(Texture2D[] textures, IntPtr ptr)
		{
			throw new NotImplementedException();
		}

		public void SetTextureData3D(IGLTexture texture, SurfaceFormat format, int level, int left, int top, int right, int bottom, int front, int back, IntPtr data, int dataLength)
		{
			throw new NotImplementedException();
		}

		public void SetTextureDataCube(IGLTexture texture, SurfaceFormat format, int xOffset, int yOffset, int width, int height, CubeMapFace cubeMapFace, int level, IntPtr data, int dataLength)
		{
			throw new NotImplementedException();
		}

		public void SetVertexBufferData(IGLBuffer buffer, int offsetInBytes, IntPtr data, int dataLength, SetDataOptions options)
		{
			throw new NotImplementedException();
		}

		public void SwapBuffers(Rectangle? sourceRectangle, Rectangle? destinationRectangle, IntPtr overrideWindowHandle)
		{
			//Console.WriteLine("SWAP!");
			throw new NotImplementedException();
		}

		#region XNA->Vulkan Enum Conversion Class

		private class XNAToVK
		{
			public static VkPresentModeKHR[] PresentMode =
			{
				VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR,	// PresentInterval.Default
				VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR,	// PresentInterval.One
				VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR,	// PresentInterval.Two
				VkPresentModeKHR.VK_PRESENT_MODE_IMMEDIATE_KHR	// PresentInterval.Immediate
			};
		}

		#endregion
	}
}
