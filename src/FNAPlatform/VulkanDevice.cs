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

		public VulkanDevice(
			PresentationParameters presentationParameters,
			GraphicsAdapter adapter
		) {
			// RenderDoc environment variable
			renderdocEnabled = Environment.GetEnvironmentVariable("FNA_VULKAN_ENABLE_RENDERDOC") == "1";

			// Validation environment variable
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
		}

		private unsafe bool ExtensionSupported(string extName, VkExtensionProperties[] extensions)
		{
			foreach (VkExtensionProperties ext in extensions)
			{
				if (UTF8_ToManaged((IntPtr) ext.extensionName) == extName)
				{
					return true;
				}
			}

			return false;
		}

		private unsafe bool LayerSupported(string layerName, VkLayerProperties[] layers)
		{
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

		private unsafe VkDebugUtilsMessengerCreateInfoEXT CreateDebugMessengerCreateInfo()
		{
			VkDebugUtilsMessageSeverityFlagBitsEXT severityFlags =
				VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT
			      | VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT
			      | VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_VERBOSE_BIT_EXT;

			if (verboseValidationEnabled)
			{
				/* This will spew a TON of crap into the output.
				 * Some of it is useful, most of it not so much.
				 * -caleb
				 */
				severityFlags |= VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_INFO_BIT_EXT;
			}

			VkDebugUtilsMessageTypeFlagBitsEXT messageFlags =
				  VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT
				| VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT
				| VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT;

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

		private bool QueueFamilySupportsGraphics(VkQueueFamilyProperties family)
		{
			return (family.queueFlags & VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT) != 0;
		}

		private bool QueueFamilySupportsPresentation(IntPtr physicalDevice, int queueFamilyIndex)
		{
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
				 * 2. At least one graphics queue family
				 * 3. At least one queue family that supports presentation
				 */
				bool supportsSwapchainExt = ExtensionSupported(
					"VK_KHR_swapchain",
					GetAllDeviceExtensions(physicalDevices[i])
				);
				graphicsQueueFamilyIndices[i] = -1;
				presentationQueueFamilyIndices[i] = -1;

				for (int j = 0; j < queueFamilies.Length; j += 1)
				{
					if (!supportsSwapchainExt)
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
				VkPhysicalDeviceProperties props;
				vkGetPhysicalDeviceProperties(physicalDevices[i], out props);

				// discrete GPU > integrated GPU > anything else
				if (props.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU)
				{
					scores[i] += 100;
				}
				else if (props.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU)
				{
					scores[i] += 50;
				}

				// FIXME: Other GPU properties to check?

				VkPhysicalDeviceFeatures features;
				vkGetPhysicalDeviceFeatures(physicalDevices[i], out features);

				/* FIXME: What features should we check for?
				 * And if they exist, should we store them for logical device creation?
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

						// FIXME: Should we keep track of which physical device features to enable?
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

		public Color BlendFactor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public int MultiSampleMask { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public int ReferenceStencil { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public bool SupportsDxt1 => throw new NotImplementedException();

		public bool SupportsS3tc => throw new NotImplementedException();

		public bool SupportsHardwareInstancing => throw new NotImplementedException();

		public int MaxTextureSlots => throw new NotImplementedException();

		public int MaxMultiSampleCount => throw new NotImplementedException();

		public IGLBackbuffer Backbuffer => throw new NotImplementedException();

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

		public void ApplyRasterizerState(RasterizerState rasterizerState)
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

		public void Dispose()
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

		public void ResetBackbuffer(PresentationParameters presentationParameters, GraphicsAdapter adapter)
		{
			throw new NotImplementedException();
		}

		public void ResolveTarget(RenderTargetBinding target)
		{
			throw new NotImplementedException();
		}

		public void SetBlendState(BlendState blendState)
		{
			throw new NotImplementedException();
		}

		public void SetDepthStencilState(DepthStencilState depthStencilState)
		{
			throw new NotImplementedException();
		}

		public void SetIndexBufferData(IGLBuffer buffer, int offsetInBytes, IntPtr data, int dataLength, SetDataOptions options)
		{
			throw new NotImplementedException();
		}

		public void SetPresentationInterval(PresentInterval presentInterval)
		{
			throw new NotImplementedException();
		}

		public void SetRenderTargets(RenderTargetBinding[] renderTargets, IGLRenderbuffer renderbuffer, DepthFormat depthFormat)
		{
			throw new NotImplementedException();
		}

		public void SetScissorRect(Rectangle scissorRect)
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

		public void SetTextureData2DPointer(Texture2D texture, IntPtr ptr)
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

		public void SetViewport(Viewport vp)
		{
			throw new NotImplementedException();
		}

		public void SwapBuffers(Rectangle? sourceRectangle, Rectangle? destinationRectangle, IntPtr overrideWindowHandle)
		{
			throw new NotImplementedException();
		}

		public void VerifySampler(int index, Texture texture, SamplerState sampler)
		{
			throw new NotImplementedException();
		}
	}
}
