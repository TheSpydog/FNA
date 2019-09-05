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
using VulkanMemoryAllocator;
#endregion

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
		#region Opaque Vulkan Handles

		private IntPtr Instance;
		private ulong WindowSurface;
		private IntPtr PhysicalDevice;
		private IntPtr Device;
		private ulong DebugMessenger;
		private IntPtr Allocator;

		#endregion

		#region Command Pools and Buffers

		private ulong GraphicsCommandPool;
		private ulong PresentCommandPool;

		private IntPtr graphicsCommandBuffer;
		private IntPtr presentCommandBuffer;

		#endregion

		#region Queue Families

		private IntPtr GraphicsQueue;
		private IntPtr PresentationQueue;

		private uint graphicsQueueFamilyIndex;
		private uint presentQueueFamilyIndex;

		#endregion

		#region Synchronization Primitives

		private ulong imageAvailableSemaphore;
		private ulong renderFinishedSemaphore;

		#endregion

		#region Environment Variable Cache

		private bool renderdocEnabled;
		private bool validationEnabled;
		private bool verboseValidationEnabled;

		#endregion

		#region Blending State Variables

		public Color BlendFactor
		{
			get
			{
				return blendColor;
			}
			set
			{
				if (value != blendColor)
				{
					blendColor = value;
					// FIXME
					//glBlendColor(
					//	blendColor.R / 255.0f,
					//	blendColor.G / 255.0f,
					//	blendColor.B / 255.0f,
					//	blendColor.A / 255.0f
					//);
				}
			}
		}

		public int MultiSampleMask
		{
			get
			{
				return multisampleMask;
			}
			set
			{
				if (value != multisampleMask)
				{
					// FIXME
					//if (value == -1)
					//{
					//	glDisable(GLenum.GL_SAMPLE_MASK);
					//}
					//else
					//{
					//	if (multisampleMask == -1)
					//	{
					//		glEnable(GLenum.GL_SAMPLE_MASK);
					//	}
					//	// FIXME: index...? -flibit
					//	glSampleMaski(0, (uint)value);
					//}
					multisampleMask = value;
				}
			}
		}

		private bool alphaBlendEnable = false;
		private Color blendColor = Color.Transparent;
		private BlendFunction blendOp = BlendFunction.Add;
		private BlendFunction blendOpAlpha = BlendFunction.Add;
		private Blend srcBlend = Blend.One;
		private Blend dstBlend = Blend.Zero;
		private Blend srcBlendAlpha = Blend.One;
		private Blend dstBlendAlpha = Blend.Zero;
		private ColorWriteChannels colorWriteEnable = ColorWriteChannels.All;
		private ColorWriteChannels colorWriteEnable1 = ColorWriteChannels.All;
		private ColorWriteChannels colorWriteEnable2 = ColorWriteChannels.All;
		private ColorWriteChannels colorWriteEnable3 = ColorWriteChannels.All;
		private int multisampleMask = -1; // AKA 0xFFFFFFFF

		#endregion

		#region Depth State Variables

		private bool zEnable = false;
		private bool zWriteEnable = false;
		private CompareFunction depthFunc = CompareFunction.Less;

		#endregion

		#region Stencil State Variables

		public int ReferenceStencil
		{
			get
			{
				return stencilRef;
			}
			set
			{
				if (value != stencilRef)
				{
					stencilRef = value;
					// FIXME
					//if (separateStencilEnable)
					//{
					//	glStencilFuncSeparate(
					//		GLenum.GL_FRONT,
					//		XNAToGL.CompareFunc[(int)stencilFunc],
					//		stencilRef,
					//		stencilMask
					//	);
					//	glStencilFuncSeparate(
					//		GLenum.GL_BACK,
					//		XNAToGL.CompareFunc[(int)ccwStencilFunc],
					//		stencilRef,
					//		stencilMask
					//	);
					//}
					//else
					//{
					//	glStencilFunc(
					//		XNAToGL.CompareFunc[(int)stencilFunc],
					//		stencilRef,
					//		stencilMask
					//	);
					//}
				}
			}
		}

		private bool stencilEnable = false;
		private int stencilWriteMask = -1; // AKA 0xFFFFFFFF, ugh -flibit
		private bool separateStencilEnable = false;
		private int stencilRef = 0;
		private int stencilMask = -1; // AKA 0xFFFFFFFF, ugh -flibit
		private CompareFunction stencilFunc = CompareFunction.Always;
		private StencilOperation stencilFail = StencilOperation.Keep;
		private StencilOperation stencilZFail = StencilOperation.Keep;
		private StencilOperation stencilPass = StencilOperation.Keep;
		private CompareFunction ccwStencilFunc = CompareFunction.Always;
		private StencilOperation ccwStencilFail = StencilOperation.Keep;
		private StencilOperation ccwStencilZFail = StencilOperation.Keep;
		private StencilOperation ccwStencilPass = StencilOperation.Keep;

		#endregion

		#region Rasterizer State Variables

		private PrimitiveType primitive = PrimitiveType.TriangleList;
		private bool scissorTestEnable = false;
		private CullMode cullFrontFace = CullMode.None;
		private FillMode fillMode = FillMode.Solid;
		private float depthBias = 0.0f;
		private float slopeScaleDepthBias = 0.0f;
		private bool multiSampleEnable = true;

		#endregion

		#region Viewport State Variables

		private Rectangle scissorRectangle = new Rectangle();
		private Rectangle viewport = new Rectangle();
		private float depthRangeMin = 0.0f;
		private float depthRangeMax = 1.0f;

		#endregion

		#region Render Target Cache Variables

		int maxAttachments;
		private VKFramebuffer currentFramebuffer;
		private DepthFormat currentDepthStencilFormat;

		#endregion

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
			InitializeDynamicStateCreateInfo();
			InitializeAllocator();

			// Generate the command pools
			GraphicsCommandPool = GenCommandPool(graphicsQueueFamilyIndex);
			PresentCommandPool = GenCommandPool(presentQueueFamilyIndex);

			// Generate the semaphores
			renderFinishedSemaphore = GenSemaphore();
			imageAvailableSemaphore = GenSemaphore();

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

			// Create the swapchain and faux-backbuffer
			Backbuffer = new VKBackbuffer(
				this,
				presentationParameters.BackBufferWidth,
				presentationParameters.BackBufferHeight,
				presentationParameters.DepthStencilFormat,
				presentationParameters.MultiSampleCount
			);

			// Get attachment info
			maxAttachments = Math.Min(
				(int) deviceProperties.limits.maxColorAttachments,
				GraphicsDevice.MAX_RENDERTARGET_BINDINGS
			);
			currentFramebuffer = null;
			currentDepthStencilFormat = DepthFormat.None;

			// Create the graphics command buffer
			GenGraphicsCommandBuffer();
			ResetGraphicsCommandBuffer();

			// FIXME: Just for testing...
			//GetPipeline();
		}

		#endregion

		#region Dispose Method

		public void Dispose()
		{
			// Destroy the swapchain and backbuffer images
			(Backbuffer as VKBackbuffer).Dispose();

			// See ya later, allocator!
			VMA.vmaDestroyAllocator(Allocator);

			// Destroy everything in reverse dependency order
			vkDestroyDevice(
				Device,
				IntPtr.Zero
			);

			if (validationEnabled)
			{
				vkDestroyDebugUtilsMessengerEXT(
					Instance,
					DebugMessenger,
					IntPtr.Zero
				);
			}

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
					presentQueueFamilyIndex = (uint) presentationQueueFamilyIndices[i];
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
			bool differentPresentationQueue = (graphicsQueueFamilyIndex != presentQueueFamilyIndex);
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
					queueFamilyIndex = presentQueueFamilyIndex
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
			vkGetDeviceQueue(Device, presentQueueFamilyIndex, 0, out PresentationQueue);
		}

		private unsafe void InitializeAllocator()
		{
			VMA.VmaDeviceMemoryCallbacks callbacks = new VMA.VmaDeviceMemoryCallbacks
			{
				pfnAllocate = Marshal.GetFunctionPointerForDelegate(
					(VMA.PFN_vmaAllocateDeviceMemoryFunction) AllocCallback
				),
				pfnFree = Marshal.GetFunctionPointerForDelegate(
					(VMA.PFN_vmaFreeDeviceMemoryFunction) FreeCallback
				)
			};

			VMA.VmaVulkanFunctions vkFuncs = new VMA.VmaVulkanFunctions
			{
				vkAllocateMemory = Marshal.GetFunctionPointerForDelegate(
					vkAllocateMemory
				),
				vkBindBufferMemory = Marshal.GetFunctionPointerForDelegate(
					vkBindBufferMemory
				),
				vkBindImageMemory = Marshal.GetFunctionPointerForDelegate(
					vkBindImageMemory
				),
				vkCmdCopyBuffer = Marshal.GetFunctionPointerForDelegate(
					vkCmdCopyBuffer
				),
				vkCreateBuffer = Marshal.GetFunctionPointerForDelegate(
					vkCreateBuffer
				),
				vkCreateImage = Marshal.GetFunctionPointerForDelegate(
					vkCreateImage
				),
				vkDestroyBuffer = Marshal.GetFunctionPointerForDelegate(
					vkDestroyBuffer
				),
				vkDestroyImage = Marshal.GetFunctionPointerForDelegate(
					vkDestroyImage
				),
				vkFlushMappedMemoryRanges = Marshal.GetFunctionPointerForDelegate(
					vkFlushMappedMemoryRanges
				),
				vkFreeMemory = Marshal.GetFunctionPointerForDelegate(
					vkFreeMemory
				),
				vkGetBufferMemoryRequirements = Marshal.GetFunctionPointerForDelegate(
					vkGetBufferMemoryRequirements
				),
				vkGetImageMemoryRequirements = Marshal.GetFunctionPointerForDelegate(
					vkGetImageMemoryRequirements
				),
				vkGetPhysicalDeviceMemoryProperties = Marshal.GetFunctionPointerForDelegate(
					vkGetPhysicalDeviceMemoryProperties
				),
				vkGetPhysicalDeviceProperties = Marshal.GetFunctionPointerForDelegate(
					vkGetPhysicalDeviceProperties
				),
				vkInvalidateMappedMemoryRanges = Marshal.GetFunctionPointerForDelegate(
					vkInvalidateMappedMemoryRanges
				),
				vkMapMemory = Marshal.GetFunctionPointerForDelegate(
					vkMapMemory
				),
				vkUnmapMemory = Marshal.GetFunctionPointerForDelegate(
					vkUnmapMemory
				)
			};

			VMA.VmaAllocatorCreateInfo createInfo = new VMA.VmaAllocatorCreateInfo
			{
				flags = 0,
				physicalDevice = PhysicalDevice,
				device = Device,
				preferredLargeHeapBlockSize = 0, // Use the default value
				pAllocationCallbacks = IntPtr.Zero,
				pDeviceMemoryCallbacks = (IntPtr) (&callbacks),
				frameInUseCount = 0, // FIXME: Is this right?
				pHeapSizeLimit = IntPtr.Zero,
				pVulkanFunctions = (IntPtr) (&vkFuncs),
				pRecordSettings = IntPtr.Zero
			};

			// Create the allocator!
			int res = VMA.vmaCreateAllocator(
				(IntPtr) (&createInfo),
				out Allocator
			);
			if (res != 0)
			{
				throw new Exception("Could not create allocator! Error: " + ((VkResult) res));
			}
		}

		private unsafe ulong GenCommandPool(uint queueFamilyIndex)
		{
			ulong pool;

			VkCommandPoolCreateInfo createInfo = new VkCommandPoolCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
				pNext = IntPtr.Zero,
				flags = VkCommandPoolCreateFlags.VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT,
				queueFamilyIndex = queueFamilyIndex
			};
			
			VkResult res = vkCreateCommandPool(
				Device,
				&createInfo,
				IntPtr.Zero,
				out pool
			);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception("Could not create command pool!");
			}

			return pool;
		}

		// FIXME: Clean this up
		private unsafe ulong GenSemaphore()
		{
			VkSemaphoreCreateInfo createInfo = new VkSemaphoreCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO,
				pNext = IntPtr.Zero,
				flags = 0
			};

			// Create the imageAvailableSemaphore
			ulong result;
			VkResult res = vkCreateSemaphore(
				Device,
				&createInfo,
				IntPtr.Zero,
				out result
			);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception("Could not create semaphore! Error: " + res);
			}

			return result;
		}

		#endregion

		#region Callback Methods

		private unsafe uint DebugCallback(
			VkDebugUtilsMessageSeverityFlagsEXT messageSeverity,
			VkDebugUtilsMessageTypeFlagsEXT messageType,
			VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
			IntPtr pUserData
		) {
			string message = UTF8_ToManaged(pCallbackData->pMessage);

			if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT)
			{
				// This is serious, so throw an exception.
				throw new Exception(message);
			}
			else if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT)
			{
				FNALoggerEXT.LogWarn("WARNING: " + message);
			}
			else
			{
				FNALoggerEXT.LogInfo("INFO: " + message);
			}

			return 0;
		}

		private void AllocCallback(
			IntPtr allocator,
			uint memoryType,
			ulong memory,
			ulong size
		) {
			FNALoggerEXT.LogInfo(
				"Allocated memory type " + memoryType +
				" at address " + memory +
				" of size " + size
			);
		}

		private void FreeCallback(
			IntPtr allocator,
			uint memoryType,
			ulong memory,
			ulong size
		) {
			FNALoggerEXT.LogInfo(
				"Freed memory type " + memoryType +
				" at address " + memory +
				" of size " + size
			);
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

		private int GetSampleCount(VkSampleCountFlags flags)
		{
			if ((flags & VkSampleCountFlags.VK_SAMPLE_COUNT_64_BIT) != 0)
			{
				return 64;
			}
			else if ((flags & VkSampleCountFlags.VK_SAMPLE_COUNT_32_BIT) != 0)
			{
				return 32;
			}
			else if ((flags & VkSampleCountFlags.VK_SAMPLE_COUNT_16_BIT) != 0)
			{
				return 16;
			}
			else if ((flags & VkSampleCountFlags.VK_SAMPLE_COUNT_8_BIT) != 0)
			{
				return 8;
			}
			else if ((flags & VkSampleCountFlags.VK_SAMPLE_COUNT_4_BIT) != 0)
			{
				return 4;
			}
			else if ((flags & VkSampleCountFlags.VK_SAMPLE_COUNT_2_BIT) != 0)
			{
				return 2;
			}
			else if ((flags & VkSampleCountFlags.VK_SAMPLE_COUNT_1_BIT) != 0)
			{
				return 1;
			}

			return 0;
		}

		private VkSampleCountFlags GetSampleCountFlags(int samples)
		{
			if (samples >= 64)
			{
				return VkSampleCountFlags.VK_SAMPLE_COUNT_64_BIT;
			}
			else if (samples >= 32)
			{
				return VkSampleCountFlags.VK_SAMPLE_COUNT_32_BIT;
			}
			else if (samples >= 16)
			{
				return VkSampleCountFlags.VK_SAMPLE_COUNT_16_BIT;
			}
			else if (samples >= 8)
			{
				return VkSampleCountFlags.VK_SAMPLE_COUNT_8_BIT;
			}
			else if (samples >= 4)
			{
				return VkSampleCountFlags.VK_SAMPLE_COUNT_4_BIT;
			}
			else if (samples >= 2)
			{
				return VkSampleCountFlags.VK_SAMPLE_COUNT_2_BIT;
			}

			// Vulkan whines if there's ever 0 samples, so return 1.
			return VkSampleCountFlags.VK_SAMPLE_COUNT_1_BIT;
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
			VkDebugUtilsMessageSeverityFlagsEXT severityFlags =
				VkDebugUtilsMessageSeverityFlagsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT |
				VkDebugUtilsMessageSeverityFlagsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT |
				VkDebugUtilsMessageSeverityFlagsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_VERBOSE_BIT_EXT;

			if (verboseValidationEnabled)
			{
				/* This will spew a TON of crap into the output.
				 * Some of it is useful, most of it not so much.
				 * -caleb
				 */
				severityFlags |= VkDebugUtilsMessageSeverityFlagsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_INFO_BIT_EXT;
			}

			VkDebugUtilsMessageTypeFlagsEXT messageFlags =
				VkDebugUtilsMessageTypeFlagsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT |
				VkDebugUtilsMessageTypeFlagsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT |
				VkDebugUtilsMessageTypeFlagsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT;

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
			return (family.queueFlags & VkQueueFlags.VK_QUEUE_GRAPHICS_BIT) != 0;
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

		private unsafe void GenGraphicsCommandBuffer()
		{
			VkCommandBufferAllocateInfo cmdBufAlloc = new VkCommandBufferAllocateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
				pNext = IntPtr.Zero,
				commandPool = GraphicsCommandPool,
				level = VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY,
				commandBufferCount = 1,
			};
			fixed (IntPtr* bufPtr = &graphicsCommandBuffer)
			{
				vkAllocateCommandBuffers(
					Device,
					&cmdBufAlloc,
					bufPtr
				);
			}
		}

		private unsafe void ResetGraphicsCommandBuffer()
		{
			vkResetCommandBuffer(
				graphicsCommandBuffer,
				VkCommandBufferResetFlags.VK_COMMAND_BUFFER_RESET_RELEASE_RESOURCES_BIT
			);

			VkCommandBufferBeginInfo beginInfo = new VkCommandBufferBeginInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
				pNext = IntPtr.Zero,
				flags = VkCommandBufferUsageFlags.VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT,
				pInheritanceInfo = null
			};
			vkBeginCommandBuffer(graphicsCommandBuffer, &beginInfo);
		}

		private unsafe void BeginRenderPass()
		{
			VkRenderPassBeginInfo beginInfo = new VkRenderPassBeginInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO,
				pNext = IntPtr.Zero,
				renderPass = currentFramebuffer.RenderPass,
				framebuffer = currentFramebuffer.Handle,
				renderArea = new VkRect2D
				{
					extent = new VkExtent2D
					{
						width = (uint) currentFramebuffer.Width,
						height = (uint) currentFramebuffer.Height
					},
					offset = new VkOffset2D
					{
						x = 0,
						y = 0
					}
				},
				clearValueCount = 0,
				pClearValues = null
			};

			vkCmdBeginRenderPass(
				graphicsCommandBuffer,
				&beginInfo,
				VkSubpassContents.VK_SUBPASS_CONTENTS_INLINE
			);
		}

		#endregion

		#region Vulkan Framebuffer Container Class

		private class VKFramebuffer
		{
			public ulong Handle
			{
				get;
				private set;
			}

			public VKRenderbuffer[] ColorAttachments
			{
				get;
				private set;
			}

			public VKRenderbuffer DepthStencilAttachment
			{
				get;
				private set;
			}

			public ulong RenderPass
			{
				get;
				private set;
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

			private VulkanDevice vkDevice;

			public VKFramebuffer(
				VulkanDevice vkDevice,
				VKRenderbuffer[] colorAttachments,
				VKRenderbuffer depthStencilAttachment
			) {
				this.vkDevice = vkDevice;

				ColorAttachments = colorAttachments;
				DepthStencilAttachment = depthStencilAttachment;

				if (colorAttachments[0] != null)
				{
					Width = colorAttachments[0].Width;
					Height = colorAttachments[0].Height;
				}
				else if (depthStencilAttachment != null)
				{
					Width = depthStencilAttachment.Width;
					Height = depthStencilAttachment.Height;
				}
				else
				{
					throw new InvalidOperationException(
						"Attempted to create framebuffer with no attachments!"
					);
				}

				CreateRenderPass();

				// Create an array to hold handles to all the attachments
				int numAttachments = ColorAttachments.Length + (
					(depthStencilAttachment == null) ? 0 : 1
				);
				ulong[] attachments = new ulong[numAttachments];
				GCHandle attachmentsPinned = GCHandle.Alloc(attachments, GCHandleType.Pinned);

				for (int i = 0; i < ColorAttachments.Length; i += 1)
				{
					attachments[i] = ColorAttachments[i].Handle;
				}
				if (DepthStencilAttachment != null)
				{
					attachments[numAttachments - 1] = DepthStencilAttachment.Handle;
				}

				// Create the actual VkFramebuffer
				unsafe
				{
					VkFramebufferCreateInfo fboCreateInfo = new VkFramebufferCreateInfo
					{
						sType = VkStructureType.VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO,
						pNext = IntPtr.Zero,
						flags = 0,
						renderPass = RenderPass,
						attachmentCount = (uint) numAttachments,
						pAttachments = (ulong*) attachmentsPinned.AddrOfPinnedObject(),
						width = (uint) Width,
						height = (uint) Height,
						layers = 1
					};

					ulong fbo;
					VkResult res = vkDevice.vkCreateFramebuffer(
						vkDevice.Device,
						&fboCreateInfo,
						IntPtr.Zero,
						out fbo
					);
					if (res != VkResult.VK_SUCCESS)
					{
						throw new Exception(
							"Could not create framebuffer! Error: " + res
						);
					}

					Handle = fbo;
				}

				// Clean up
				attachmentsPinned.Free();
			}

			private unsafe void CreateRenderPass()
			{
				// This array holds ALL attachment descriptions
				int numAttachments = (
					ColorAttachments.Length +
					(DepthStencilAttachment == null ? 0 : 1)
				);
				VkAttachmentDescription[] attachmentDescs =
					new VkAttachmentDescription[numAttachments];

				GCHandle attachmentDescsPinned =
					GCHandle.Alloc(attachmentDescs, GCHandleType.Pinned);

				// This array holds all COLOR attachment references
				VkAttachmentReference[] colorAttachmentRefs =
					new VkAttachmentReference[ColorAttachments.Length];

				GCHandle colorRefsPinned =
					GCHandle.Alloc(colorAttachmentRefs, GCHandleType.Pinned);

				// This holds the DEPTH-STENCIL attachment reference
				VkAttachmentReference depthStencilAttachmentRef;

				// Process all color attachments
				for (int i = 0; i < ColorAttachments.Length; i += 1)
				{
					attachmentDescs[i] =
						ColorAttachments[i].GetAttachmentDescription();

					colorAttachmentRefs[i] = new VkAttachmentReference
					{
						 attachment = (uint) i,
						 layout = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL
					};
				}

				// Process depth-stencil, if needed
				if (DepthStencilAttachment != null)
				{
					attachmentDescs[numAttachments - 1] =
						DepthStencilAttachment.GetAttachmentDescription();

					depthStencilAttachmentRef = new VkAttachmentReference
					{
						attachment = (uint) (numAttachments - 1),
						layout = VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL
					};
				}

				// Create subpass description
				VkSubpassDescription subpassDesc = new VkSubpassDescription
				{
					flags = 0,
					pipelineBindPoint = VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS,

					// FIXME: What should these be?
					inputAttachmentCount = 0,
					pInputAttachments = null,

					colorAttachmentCount = (uint) ColorAttachments.Length,
					pColorAttachments = (
						(VkAttachmentReference*) colorRefsPinned.AddrOfPinnedObject()
					),

					// FIXME: What should this be?
					pResolveAttachments = null,

					pDepthStencilAttachment = &depthStencilAttachmentRef,

					// FIXME: What should these be?
					preserveAttachmentCount = 0,
					pPreserveAttachments = null
				};

				// Add the dependencies
				// FIXME: This doesn't handle depth-stencil...?
				// FIXME: Taken from https://www.reddit.com/r/vulkan/comments/6f60pj/problem_with_displaying_triangle/difx5h7/
				VkSubpassDependency[] dependencies = new VkSubpassDependency[2];
				dependencies[0] = new VkSubpassDependency
				{
					srcSubpass = VK_SUBPASS_EXTERNAL,
					dstSubpass = 0,

					srcStageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT,
					dstStageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,

					srcAccessMask = VkAccessFlags.VK_ACCESS_MEMORY_READ_BIT,
					dstAccessMask = (
						VkAccessFlags.VK_ACCESS_COLOR_ATTACHMENT_READ_BIT |
						VkAccessFlags.VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT
					),

					dependencyFlags = VkDependencyFlags.VK_DEPENDENCY_BY_REGION_BIT
				};
				dependencies[1] = new VkSubpassDependency
				{
					srcSubpass = 0,
					dstSubpass = VK_SUBPASS_EXTERNAL,

					srcStageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
					dstStageMask = VkPipelineStageFlags.VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT,

					srcAccessMask = (
						VkAccessFlags.VK_ACCESS_COLOR_ATTACHMENT_READ_BIT |
						VkAccessFlags.VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT
					),
					dstAccessMask = VkAccessFlags.VK_ACCESS_MEMORY_READ_BIT,

					dependencyFlags = VkDependencyFlags.VK_DEPENDENCY_BY_REGION_BIT
				};

				GCHandle depsPinned = GCHandle.Alloc(dependencies, GCHandleType.Pinned);

				// Create the render pass
				VkRenderPassCreateInfo passCreateInfo = new VkRenderPassCreateInfo
				{
					sType = VkStructureType.VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO,
					pNext = IntPtr.Zero,
					flags = 0,
					attachmentCount = (uint) numAttachments,
					pAttachments = (
						(VkAttachmentDescription*) attachmentDescsPinned.AddrOfPinnedObject()
					),
					subpassCount = 1,
					pSubpasses = &subpassDesc,
					dependencyCount = (uint) dependencies.Length,
					pDependencies = (VkSubpassDependency*) depsPinned.AddrOfPinnedObject()
				};

				ulong renderPass;
				VkResult res = vkDevice.vkCreateRenderPass(
					vkDevice.Device,
					&passCreateInfo,
					IntPtr.Zero,
					out renderPass
				);
				if (res != VkResult.VK_SUCCESS)
				{
					throw new Exception("Could not create render pass! Error: " + res);
				}

				// Make it official
				RenderPass = renderPass;

				// Clean up
				attachmentDescsPinned.Free();
				colorRefsPinned.Free();
				depsPinned.Free();
			}

			public void Dispose()
			{
				// Destroy framebuffer
				vkDevice.vkDestroyFramebuffer(
					vkDevice.Device,
					Handle,
					IntPtr.Zero
				);

				// Destroy render pass
				vkDevice.vkDestroyRenderPass(
					vkDevice.Device,
					RenderPass,
					IntPtr.Zero
				);

				// Destroy all color attachments
				foreach (VKRenderbuffer rbo in ColorAttachments)
				{
					rbo.Dispose();
				}

				// Destroy depth-stencil attachment
				if (DepthStencilAttachment != null)
				{
					DepthStencilAttachment.Dispose();
				}
			}
		}

		#endregion

		#region Dynamic Pipeline States

		private VkDynamicState[] dynamicStates =
		{
			VkDynamicState.VK_DYNAMIC_STATE_BLEND_CONSTANTS,
			VkDynamicState.VK_DYNAMIC_STATE_DEPTH_BIAS,
			VkDynamicState.VK_DYNAMIC_STATE_DEPTH_BOUNDS,
			VkDynamicState.VK_DYNAMIC_STATE_SAMPLE_LOCATIONS_EXT,
			VkDynamicState.VK_DYNAMIC_STATE_SCISSOR,
			VkDynamicState.VK_DYNAMIC_STATE_STENCIL_COMPARE_MASK,
			VkDynamicState.VK_DYNAMIC_STATE_STENCIL_REFERENCE,
			VkDynamicState.VK_DYNAMIC_STATE_STENCIL_WRITE_MASK,
			VkDynamicState.VK_DYNAMIC_STATE_VIEWPORT
		};
		private VkPipelineDynamicStateCreateInfo dynamicStateCreateInfo;

		private unsafe void InitializeDynamicStateCreateInfo()
		{
			dynamicStateCreateInfo = new VkPipelineDynamicStateCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO,
				dynamicStateCount = (uint) dynamicStates.Length
			};
			fixed (VkDynamicState* statesPtr = dynamicStates)
			{
				dynamicStateCreateInfo.pDynamicStates = statesPtr;
			}
		}

		#endregion

		#region Graphics Pipeline

		private struct PSO
		{
			public ulong Pipeline;
			public ulong Layout;
		}

		// FIXME: Add PipelineProperties struct
		// FIXME: Add <PipelineProperties, PSO> pipeline cache

		private unsafe PSO GetPipeline()
		{
			// TODO: If pipeline is already cached, return pipeline

			// Create a new graphics pipeline
			VkGraphicsPipelineCreateInfo pipeline = new VkGraphicsPipelineCreateInfo();
			pipeline.sType = VkStructureType.VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO;
			pipeline.pNext = IntPtr.Zero;
			pipeline.flags = 0;

			/* Throughout this method we pin a bunch of instance
			 * variables and local arrays so their pointers are
			 * guaranteed to be valid when we create the pipeline.
			 */

			// Define the shader stages
			VkPipelineShaderStageCreateInfo[] shaderStages =
			{
				// Vertex
				new VkPipelineShaderStageCreateInfo
				{
					sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
					pNext = IntPtr.Zero,
					flags = 0,
					stage = VkShaderStageFlags.VK_SHADER_STAGE_VERTEX_BIT,

					// FIXME: Fill this in from MojoShader
					module = 0,
					pName = IntPtr.Zero,
					pSpecializationInfo = null
				},

				// Fragment
				new VkPipelineShaderStageCreateInfo
				{
					sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
					pNext = IntPtr.Zero,
					flags = 0,
					stage = VkShaderStageFlags.VK_SHADER_STAGE_FRAGMENT_BIT,

					// FIXME: Fill this in from MojoShader
					module = 0,
					pName = IntPtr.Zero,
					pSpecializationInfo = null
				}
			};
			GCHandle shaderStagesPin = GCHandle.Alloc(shaderStages, GCHandleType.Pinned);
			pipeline.stageCount = (uint) shaderStages.Length;
			pipeline.pStages = (VkPipelineShaderStageCreateInfo*) shaderStagesPin.AddrOfPinnedObject();

			// Describe the vertex input (FIXME: Fill these in from MojoShader)
			VkVertexInputBindingDescription[] bindings = new VkVertexInputBindingDescription[0];
			VkVertexInputAttributeDescription[] attributes = new VkVertexInputAttributeDescription[0];
			GCHandle bindingsPin = GCHandle.Alloc(bindings, GCHandleType.Pinned);
			GCHandle attributesPin = GCHandle.Alloc(attributes, GCHandleType.Pinned);

			VkPipelineVertexInputStateCreateInfo vertexInput = new VkPipelineVertexInputStateCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO,
				pNext = IntPtr.Zero,
				flags = 0,
				vertexBindingDescriptionCount = (uint) bindings.Length,
				pVertexBindingDescriptions = (VkVertexInputBindingDescription*) bindingsPin.AddrOfPinnedObject(),
				vertexAttributeDescriptionCount = (uint) attributes.Length,
				pVertexAttributeDescriptions = (VkVertexInputAttributeDescription*) attributesPin.AddrOfPinnedObject()
			};
			pipeline.pVertexInputState = &vertexInput;

			// Define the input assembly state
			VkPipelineInputAssemblyStateCreateInfo inputAssembly = new VkPipelineInputAssemblyStateCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO,
				pNext = IntPtr.Zero,
				flags = 0,
				topology = XNAToVK.PrimitiveType[(int) primitive],
				primitiveRestartEnable = 0
			};
			pipeline.pInputAssemblyState = &inputAssembly;

			// FIXME: Are these even needed...?
			pipeline.pTessellationState = null;
			pipeline.pViewportState = null;

			// Describe the rasterization state
			VkPipelineRasterizationStateCreateInfo rasterization = new VkPipelineRasterizationStateCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO,
				pNext = IntPtr.Zero,
				flags = 0,
				depthClampEnable = 1, // FIXME: Need to enable this device feature
				rasterizerDiscardEnable = 1, // FIXME: Is this right?
				polygonMode = XNAToVK.FillMode[(int) fillMode], // FIXME: Need to enable device feature
				cullMode = XNAToVK.CullMode[(int) cullFrontFace],
				frontFace = XNAToVK.FrontFace[(int) cullFrontFace],
				depthBiasEnable = 1,
				depthBiasConstantFactor = depthBias,
				depthBiasClamp = depthRangeMax, // FIXME: Is this right?
				depthBiasSlopeFactor = slopeScaleDepthBias,
				lineWidth = 1
			};
			pipeline.pRasterizationState = &rasterization;

			// Describe multisample state
			GCHandle multisampleMaskPin = GCHandle.Alloc(multisampleMask, GCHandleType.Pinned);
			VkPipelineMultisampleStateCreateInfo multisample = new VkPipelineMultisampleStateCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO,
				pNext = IntPtr.Zero,
				flags = 0,
				rasterizationSamples = GetSampleCountFlags(1), // FIXME: What should this be?
				sampleShadingEnable = 0, // FIXME: What is this?
				minSampleShading = 1.0f, // FIXME: What is this?
				pSampleMask = (uint*) multisampleMaskPin.AddrOfPinnedObject(),
				alphaToCoverageEnable = 0,
				alphaToOneEnable = 0
			};
			pipeline.pMultisampleState = &multisample;

			// Describe depth-stencil state
			VkStencilOpState frontStencilState = new VkStencilOpState
			{
				failOp = XNAToVK.StencilOperation[(int) stencilFail],
				passOp = XNAToVK.StencilOperation[(int) stencilPass],
				depthFailOp = XNAToVK.StencilOperation[(int) stencilZFail],
				compareOp = XNAToVK.CompareFunction[(int) stencilFunc],
				compareMask = (uint) stencilMask,
				writeMask = (uint) stencilWriteMask,
				reference = (uint) stencilRef
			};
			VkStencilOpState backStencilState = new VkStencilOpState
			{
				failOp = (
					(separateStencilEnable)
					? XNAToVK.StencilOperation[(int) ccwStencilFail]
					: frontStencilState.failOp
				),
				passOp = (
					(separateStencilEnable)
					? XNAToVK.StencilOperation[(int) ccwStencilPass]
					: frontStencilState.passOp
				),
				depthFailOp = (
					(separateStencilEnable)
					? XNAToVK.StencilOperation[(int) ccwStencilZFail]
					: frontStencilState.depthFailOp
				),
				compareOp = (
					(separateStencilEnable)
					? XNAToVK.CompareFunction[(int) ccwStencilFunc]
					: frontStencilState.compareOp
				),
				compareMask = (uint) stencilMask,
				writeMask = (uint) stencilWriteMask,
				reference = (uint) stencilRef
			};
			VkPipelineDepthStencilStateCreateInfo depthStencil = new VkPipelineDepthStencilStateCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_DEPTH_STENCIL_STATE_CREATE_INFO,
				pNext = IntPtr.Zero,
				flags = 0,
				depthTestEnable = (zEnable) ? 1u : 0u,
				depthWriteEnable = (zWriteEnable) ? 1u : 0u,
				depthCompareOp = XNAToVK.CompareFunction[(int) depthFunc],
				depthBoundsTestEnable = 1,
				stencilTestEnable = (stencilEnable) ? 1u : 0u,
				front = frontStencilState,
				back = backStencilState,
				minDepthBounds = depthRangeMin,
				maxDepthBounds = depthRangeMax
			};
			pipeline.pDepthStencilState = &depthStencil;

			// Describe color blend state
			Vector4 normalizedBlendConstants = new Vector4(
				blendColor.R / 255f,
				blendColor.G / 255f,
				blendColor.B / 255f,
				blendColor.A / 255f
			);
			VkPipelineColorBlendStateCreateInfo blendState = new VkPipelineColorBlendStateCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO,
				pNext = IntPtr.Zero,
				flags = 0,
				logicOpEnable = 0,
				logicOp = 0,
				attachmentCount = 0, // FIXME: What should this be?
				pAttachments = null, // FIXME: What should this be?
				blendConstants_r = normalizedBlendConstants.X,
				blendConstants_g = normalizedBlendConstants.Y,
				blendConstants_b = normalizedBlendConstants.Z,
				blendConstants_a = normalizedBlendConstants.W,
			};
			pipeline.pColorBlendState = null;

			// Pin and reuse the pre-initialized dynamic state
			GCHandle dynamicStatesPin = GCHandle.Alloc(dynamicStateCreateInfo, GCHandleType.Pinned);
			pipeline.pDynamicState = (VkPipelineDynamicStateCreateInfo*) dynamicStatesPin.AddrOfPinnedObject();

			// Create pipeline layout
			VkPipelineLayoutCreateInfo layoutCreateInfo = new VkPipelineLayoutCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO,
				pNext = IntPtr.Zero,
				flags = 0,

				// FIXME: This is where shader uniform descriptors will go
				setLayoutCount = 0,
				pSetLayouts = null,

				pushConstantRangeCount = 0,
				pPushConstantRanges = null
			};
			VkResult res = vkCreatePipelineLayout(
				Device,
				&layoutCreateInfo,
				IntPtr.Zero,
				out pipeline.layout
			);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception("Could not create pipeline layout! Error: " + res);
			}

			// FIXME: Specify compatible render pass
			pipeline.renderPass = 0;

			// FIXME: Do we always want the first subpass?
			pipeline.subpass = 0;

			// Don't bother with deriving from another pipeline
			pipeline.basePipelineHandle = 0;
			pipeline.basePipelineIndex = 0;

			// Bake the pipeline
			ulong bakedPipeline;
			res = vkCreateGraphicsPipelines(
				Device,
				0, // FIXME: Do we want a VkPipelineCache?
				1,
				&pipeline,
				IntPtr.Zero,
				&bakedPipeline
			);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception("Could not create graphics pipeline! Error: " + res);
			}

			// Clean up the pinned objects
			attributesPin.Free();
			bindingsPin.Free();
			dynamicStatesPin.Free();
			multisampleMaskPin.Free();
			shaderStagesPin.Free();

			// We're finished!
			return new PSO
			{
				Pipeline = bakedPipeline,
				Layout = pipeline.layout
			};
		}

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
				// FIXME: vkCmdSetScissor(CommandBuffer, 0, 1, <ptr to rect>)
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

		private class VKRenderbuffer : IGLRenderbuffer
		{
			public ulong Handle
			{
				get;
				private set;
			}

			public ulong ImageHandle
			{
				get;
				private set;
			}

			public IntPtr Allocation
			{
				get;
				private set;
			}

			public bool HasMipmaps
			{
				get;
				private set;
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

			public VkFormat Format
			{
				get;
				private set;
			}

			public int SampleCount
			{
				get;
				private set;
			}

			private VulkanDevice vkDevice;
			private bool preserveContents;
			private bool isDepthStencil;

			public unsafe VKRenderbuffer(
				VulkanDevice vkDevice,
				VkFormat format,
				int width,
				int height,
				int levelCount,
				int sampleCount,
				VkImageUsageFlags usage,
				VkImageAspectFlags aspect,
				bool preserveContents
			) {
				this.vkDevice = vkDevice;
				this.preserveContents = preserveContents;
				isDepthStencil = (
					(usage & VkImageUsageFlags.VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT) != 0
				);

				Width = width;
				Height = height;
				Format = format;
				SampleCount = sampleCount;

				// Create and allocate the image
				VkImageCreateInfo imgCreateInfo = new VkImageCreateInfo
				{
					sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO,
					pNext = IntPtr.Zero,
					flags = 0,
					imageType = VkImageType.VK_IMAGE_TYPE_2D,
					format = format,
					extent = new VkExtent3D(
						(uint) width,
						(uint) height,
						1
					),
					mipLevels = (uint) levelCount,
					arrayLayers = 1,
					samples = vkDevice.GetSampleCountFlags(sampleCount),
					tiling = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
					usage = usage,
					sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
					queueFamilyIndexCount = 0,
					pQueueFamilyIndices = null,
					initialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED
				};

				VMA.VmaAllocationCreateInfo allocCreateInfo = new VMA.VmaAllocationCreateInfo
				{
					// FIXME: Assumption! -caleb
					usage = VMA.VmaMemoryUsage.VMA_MEMORY_USAGE_GPU_ONLY
				};

				ulong imageHandle;
				IntPtr alloc;
				int res = VMA.vmaCreateImage(
					vkDevice.Allocator,
					(IntPtr) (&imgCreateInfo),
					ref allocCreateInfo,
					out imageHandle,
					out alloc,
					IntPtr.Zero
				);
				if (res != (int) VkResult.VK_SUCCESS)
				{
					throw new Exception(
						"Could not create texture! Error: " + (VkResult) res
					);
				}

				// Create the image view
				ulong viewHandle;
				VkImageViewCreateInfo viewCreateInfo = new VkImageViewCreateInfo
				{
					sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
					pNext = IntPtr.Zero,
					flags = 0,
					format = format,
					image = imageHandle,
					viewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
					components = VkComponentMapping.Identity,
					subresourceRange = new VkImageSubresourceRange
					{
						aspectMask = aspect,
						baseMipLevel = 0,
						levelCount = (uint) levelCount,
						baseArrayLayer = 0,
						layerCount = 1
					}
				};
				res = (int) vkDevice.vkCreateImageView(
					vkDevice.Device,
					&viewCreateInfo,
					IntPtr.Zero,
					out viewHandle
				);
				if (res != (int) VkResult.VK_SUCCESS)
				{
					throw new Exception(
						"Could not create image view! Error: " + (VkResult) res
					);
				}

				// Set the public properties
				Handle = viewHandle;
				ImageHandle = imageHandle;
				Allocation = alloc;
				HasMipmaps = levelCount > 1;
			}

			public void Dispose()
			{
				vkDevice.vkDestroyImageView(
					vkDevice.Device,
					Handle,
					IntPtr.Zero
				);

				VMA.vmaDestroyImage(
					vkDevice.Allocator,
					ImageHandle,
					Allocation
				);

				ImageHandle = 0;
				Handle = 0;
				Allocation = IntPtr.Zero;
			}

			public VkRect2D GetRect()
			{
				VkRect2D rect = new VkRect2D();
				rect.extent.width = (uint) Width;
				rect.extent.height = (uint) Height;
				rect.offset.x = 0;
				rect.offset.y = 0;
				return rect;
			}

			public VkAttachmentDescription GetAttachmentDescription()
			{
				return new VkAttachmentDescription
				{
					flags = 0,
					format = Format,
					samples = vkDevice.GetSampleCountFlags(SampleCount),
					loadOp = (
						preserveContents
						? VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_LOAD
						: VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_DONT_CARE
					),
					storeOp = (
						preserveContents
						? VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE
						: VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_DONT_CARE
					),

					// FIXME: Is this right?
					stencilLoadOp = (
						preserveContents
						? VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_LOAD
						: VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_DONT_CARE
					),
					stencilStoreOp = (
						preserveContents
						? VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE
						: VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_DONT_CARE
					),

					initialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
					finalLayout = (
						isDepthStencil
						? VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL //FIXME: DEPTH_STENCIL_ATTACHMENT_OPTIMAL
						: VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL //FIXME: COLOR_ATTACHMENT_OPTIMAL
					)
				};
			}
		}

		#endregion

		#region Faux-Backbuffer / Swapchain

		public IGLBackbuffer Backbuffer
		{
			get;
			private set;
		}

		private class VKBackbuffer : IGLBackbuffer
		{
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

			public ulong SwapchainHandle
			{
				get;
				private set;
			}

			public ulong[] SwapchainImages
			{
				get;
				private set;
			}

			public VKRenderbuffer ColorAttachment
			{
				get;
				private set;
			}

			public VKRenderbuffer DepthStencilAttachment
			{
				get;
				private set;
			}

			private VulkanDevice vkDevice;
			private VkPresentModeKHR[] supportedPresentModes;
			private SurfaceFormat colorFormat;

			private ulong oldSwapchain;

			private VKFramebuffer framebuffer;

			public VKBackbuffer(
				VulkanDevice device,
				int width,
				int height,
				DepthFormat depthFormat,
				int multiSampleCount
			) {
				vkDevice = device;
				Width = width;
				Height = height;
				DepthFormat = depthFormat;
				MultiSampleCount = multiSampleCount;

				colorFormat = SurfaceFormat.Color;

				CacheSupportedPresentModes();
			}

			private unsafe void CacheSupportedPresentModes()
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

			private unsafe void GenSwapchain(PresentInterval presentInterval)
			{
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
				if (	surfaceCapabilities.maxImageCount > 0 &&
					imageCount > surfaceCapabilities.maxImageCount	)
				{
					imageCount = surfaceCapabilities.maxImageCount;
				}
				createInfo.minImageCount = imageCount;

				// Specify the swapchain image format
				createInfo.imageFormat = XNAToVK.SurfaceFormat[(int) colorFormat];
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
					VkImageUsageFlags.VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT |
					VkImageUsageFlags.VK_IMAGE_USAGE_TRANSFER_DST_BIT
				);

				// Define concurrency details
				uint[] indices =
				{
					vkDevice.graphicsQueueFamilyIndex,
					vkDevice.presentQueueFamilyIndex
				};
				GCHandle indicesPinned = GCHandle.Alloc(indices, GCHandleType.Pinned);

				if (vkDevice.graphicsQueueFamilyIndex != vkDevice.presentQueueFamilyIndex)
				{
					createInfo.imageSharingMode = VkSharingMode.VK_SHARING_MODE_CONCURRENT;
					createInfo.queueFamilyIndexCount = 2;
					createInfo.pQueueFamilyIndices = (
						(uint*) indicesPinned.AddrOfPinnedObject()
					);
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
				createInfo.compositeAlpha =
					VkCompositeAlphaFlagsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;

				// Check if the requested present interval is supported
				VkPresentModeKHR presentMode = XNAToVK.PresentInterval[(int) presentInterval];
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
				createInfo.clipped = 1;

				// FIXME: I suspect there's more to it than this...
				createInfo.oldSwapchain = SwapchainHandle;
				oldSwapchain = SwapchainHandle;

				// Create the new swapchain!
				ulong newSwapchain;
				VkResult res = vkDevice.vkCreateSwapchainKHR(
					vkDevice.Device,
					&createInfo,
					IntPtr.Zero,
					out newSwapchain
				);
				if (res != VkResult.VK_SUCCESS)
				{
					throw new Exception("Could not generate swapchain! Error: " + res);
				}

				SwapchainHandle = newSwapchain;

				// Get the swapchain images
				uint numSwapchainImages;
				vkDevice.vkGetSwapchainImagesKHR(
					vkDevice.Device,
					SwapchainHandle,
					out numSwapchainImages,
					null
				);
				ulong[] images = new ulong[numSwapchainImages];
				fixed (ulong* imagesPtr = images)
				{
					vkDevice.vkGetSwapchainImagesKHR(
						vkDevice.Device,
						SwapchainHandle,
						out numSwapchainImages,
						imagesPtr
					);
				}

				SwapchainImages = images;
				Console.WriteLine("Created " + SwapchainImages.Length + "images!");

				// Clean up
				indicesPinned.Free();

				/* !!! FIXME !!!
				 * To correctly transition the image layouts,
				 * we need to make a framebuffer for the
				 * swapchain images themselves!
				 */
			}

			public void ResetFramebuffer(
				PresentationParameters presentationParameters
			) {
				// FIXME: Need to handle detatching/freeing old resources
				GenSwapchain(presentationParameters.PresentationInterval);

				// Create the color buffer image/view
				VkImageUsageFlags colorUsageFlags = (
					VkImageUsageFlags.VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT |
					VkImageUsageFlags.VK_IMAGE_USAGE_TRANSFER_SRC_BIT
				);
				ColorAttachment = new VKRenderbuffer(
					vkDevice,
					XNAToVK.SurfaceFormat[(int) colorFormat],
					Width,
					Height,
					1,
					MultiSampleCount,
					colorUsageFlags,
					VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
					false
				);

				// Create the depth-stencil image/view
				VkImageUsageFlags depthUsageFlags = (
					VkImageUsageFlags.VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT |
					VkImageUsageFlags.VK_IMAGE_USAGE_TRANSFER_SRC_BIT
				);
				VkImageAspectFlags aspectFlags = VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT;
				if (DepthFormat == DepthFormat.Depth24Stencil8)
				{
					// We don't always need a stencil, but when we do...
					aspectFlags |= VkImageAspectFlags.VK_IMAGE_ASPECT_STENCIL_BIT;
				}
				DepthStencilAttachment = new VKRenderbuffer(
					vkDevice,
					XNAToVK.DepthFormat[(int) DepthFormat],
					Width,
					Height,
					1,
					MultiSampleCount,
					depthUsageFlags,
					aspectFlags,
					false
				);

				// Create the framebuffer
				framebuffer = new VKFramebuffer(
					vkDevice,
					new VKRenderbuffer[] { ColorAttachment },
					DepthStencilAttachment
				);

				// If this is the first run, perform initialization tasks
				if (vkDevice.currentFramebuffer == null)
				{
					// Default to this framebuffer.
					vkDevice.currentFramebuffer = framebuffer;
					vkDevice.currentDepthStencilFormat = DepthFormat;

					// Set initial render pass
					vkDevice.BeginRenderPass();
				}
			}

			public void Dispose()
			{
				// Destroy the framebuffer and attachments
				framebuffer.Dispose();

				// Destroy the swapchain itself
				vkDevice.vkDestroySwapchainKHR(
					vkDevice.Device,
					SwapchainHandle,
					IntPtr.Zero
				);
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
			// FIXME: Do scissor rectangle / stencil mask / etc matter?

			bool clearTarget = (options & ClearOptions.Target) == ClearOptions.Target;
			bool clearDepth = (options & ClearOptions.DepthBuffer) == ClearOptions.DepthBuffer;
			bool clearStencil = (options & ClearOptions.Stencil) == ClearOptions.Stencil;

			// Set up arrays for holding clear info
			int numAttachments = currentFramebuffer.ColorAttachments.Length;
			uint numCleared = 0;

			VkClearAttachment[] clearAttachments =
				new VkClearAttachment[numAttachments + 1];

			VkClearRect[] clearRects =
				new VkClearRect[numAttachments + 1];

			// Clear color
			if (clearTarget)
			{
				for (int i = 0; i < numAttachments; i += 1)
				{
					// Define what clear operations we want
					VkClearAttachment colorAtt = new VkClearAttachment();
					colorAtt.aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT;
					colorAtt.colorAttachment = (uint) i;
					unsafe
					{
						colorAtt.clearValue.color.float32[0] = color.X;
						colorAtt.clearValue.color.float32[1] = color.Y;
						colorAtt.clearValue.color.float32[2] = color.Z;
						colorAtt.clearValue.color.float32[3] = color.W;
					}
					clearAttachments[numCleared] = colorAtt;

					// Define the area to clear
					VkClearRect clearRect = new VkClearRect();
					clearRect.rect = currentFramebuffer.ColorAttachments[i].GetRect();
					clearRect.baseArrayLayer = 0;
					clearRect.layerCount = 1;
					clearRects[numCleared] = clearRect;

					numCleared += 1;
				}
			}

			// Clear depth and/or stencil
			if ((clearDepth || clearStencil) && currentDepthStencilFormat != DepthFormat.None)
			{
				VkClearAttachment dsAtt = new VkClearAttachment();

				// Clear depth?
				if (clearDepth)
				{
					dsAtt.aspectMask |= VkImageAspectFlags.VK_IMAGE_ASPECT_DEPTH_BIT;
				}

				// Clear stencil?
				if (clearStencil && currentDepthStencilFormat == DepthFormat.Depth24Stencil8)
				{
					dsAtt.aspectMask |= VkImageAspectFlags.VK_IMAGE_ASPECT_STENCIL_BIT;
				}

				// Were we able to do anything here?
				if (dsAtt.aspectMask != 0)
				{
					// Define the clear values
					dsAtt.colorAttachment = 0; // ignored
					dsAtt.clearValue.depthStencil.depth = depth;
					dsAtt.clearValue.depthStencil.stencil = (uint) stencil;
					clearAttachments[numCleared] = dsAtt;

					// Define the area to clear
					VkClearRect clearRect = new VkClearRect();
					clearRect.rect = currentFramebuffer.DepthStencilAttachment.GetRect();
					clearRect.baseArrayLayer = 0;
					clearRect.layerCount = 1;
					clearRects[numCleared] = clearRect;

					numCleared += 1;
				}
			}

			// FIXME: In theory this shouldn't be necessary.
			if (numCleared == 0)
			{
				// Something weird happened.
				throw new Exception("Attempted to clear but there were no attachments!");
			}

			// CLEAR!
			unsafe
			{
				fixed (VkClearAttachment* attachments = clearAttachments)
				{
					fixed (VkClearRect* rects = clearRects)
					{
						vkCmdClearAttachments(
							graphicsCommandBuffer,
							numCleared,
							attachments,
							numCleared,
							rects
						);
					}
				}
			}
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

		public unsafe void SwapBuffers(
			Rectangle? sourceRectangle,
			Rectangle? destinationRectangle,
			IntPtr overrideWindowHandle
		) {
			Console.WriteLine("SWAP");

			// End the render pass
			vkCmdEndRenderPass(graphicsCommandBuffer);

			// Get the next swapchain image
			uint imageIndex;
			vkAcquireNextImageKHR(
				Device,
				(Backbuffer as VKBackbuffer).SwapchainHandle,
				ulong.MaxValue,
				imageAvailableSemaphore,
				0,
				out imageIndex
			);

			// Blit the backbuffer to the swapchain image
			VkImageBlit region = new VkImageBlit
			{
				srcSubresource = new VkImageSubresourceLayers
				{
					aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
					baseArrayLayer = 0,
					layerCount = 1,
					mipLevel = 0
				},
				dstSubresource = new VkImageSubresourceLayers
				{
					aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
					baseArrayLayer = 0,
					layerCount = 1,
					mipLevel = 0
				},

				// FIXME
				srcOffsets_0 = new VkOffset3D { x = 0, y = 0, z = 0 },
				srcOffsets_1 = new VkOffset3D { x = 100, y = 100, z = 1 },
				dstOffsets_0 = new VkOffset3D { x = 0, y = 0, z = 0 },
				dstOffsets_1 = new VkOffset3D { x = 100, y = 100, z = 1 }
			};
			vkCmdBlitImage(
				graphicsCommandBuffer,
				currentFramebuffer.ColorAttachments[0].ImageHandle,
				VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
				(Backbuffer as VKBackbuffer).SwapchainImages[imageIndex],
				VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
				1,
				&region,
				VkFilter.VK_FILTER_LINEAR
			);

			// End command buffer recording
			VkResult res = vkEndCommandBuffer(graphicsCommandBuffer);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception("Failed to end command buffer! Error: " + res);
			}

			// Prepare for submission
			uint[] flags =
			{
				(uint) VkPipelineStageFlags.VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT
			};
			GCHandle flagsPinned = GCHandle.Alloc(
				flags,
				GCHandleType.Pinned
			);
			GCHandle imageAvailableSemPinned = GCHandle.Alloc(
				imageAvailableSemaphore,
				GCHandleType.Pinned
			);
			GCHandle renderFinishedSemPinned = GCHandle.Alloc(
				renderFinishedSemaphore,
				GCHandleType.Pinned
			);
			GCHandle cmdBufPinned = GCHandle.Alloc(
				graphicsCommandBuffer,
				GCHandleType.Pinned
			);

			// Create the synchronization fence to wait on the command buffer
			ulong fence;
			VkFenceCreateInfo fenceCreateInfo = new VkFenceCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_FENCE_CREATE_INFO,
				pInfo = IntPtr.Zero,
				flags = 0
			};
			vkCreateFence(Device, &fenceCreateInfo, IntPtr.Zero, out fence);

			// Submit the command buffer
			VkSubmitInfo submitInfo = new VkSubmitInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO,
				pNext = IntPtr.Zero,
				waitSemaphoreCount = 1,
				pWaitSemaphores = (ulong*) imageAvailableSemPinned.AddrOfPinnedObject(),
				pWaitDstStageMask = (VkPipelineStageFlags*) flagsPinned.AddrOfPinnedObject(),
				commandBufferCount = 1,
				pCommandBuffers = (IntPtr*) cmdBufPinned.AddrOfPinnedObject(),
				signalSemaphoreCount = 1,
				pSignalSemaphores = (ulong*) renderFinishedSemPinned.AddrOfPinnedObject()
			};
			res = vkQueueSubmit(
				GraphicsQueue,
				1,
				&submitInfo,
				fence
			);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception("Could not submit queue! Error: " + res);
			}

			// Wait for the submission to be completed...
			res = vkWaitForFences(Device, 1, &fence, 1, ulong.MaxValue);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception(
					"Error waiting for fence! Error: " + res
				);
			}

			// Present the swapchain image!
			ulong swapchainHandle = (Backbuffer as VKBackbuffer).SwapchainHandle;
			VkPresentInfoKHR presentInfo = new VkPresentInfoKHR
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_PRESENT_INFO_KHR,
				pNext = IntPtr.Zero,
				waitSemaphoreCount = 1,
				pWaitSemaphores = (ulong*) renderFinishedSemPinned.AddrOfPinnedObject(),
				swapchainCount = 1,
				pSwapchains = &swapchainHandle,
				pImageIndices = &imageIndex,
				pResults = null
			};
			res = vkQueuePresentKHR(
				PresentationQueue,
				&presentInfo
			);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception("Error presenting image! Error: " + res);
			}

			// Free pinned objects
			flagsPinned.Free();
			imageAvailableSemPinned.Free();
			renderFinishedSemPinned.Free();
			cmdBufPinned.Free();

			// Begin anew~
			ResetGraphicsCommandBuffer();
			BeginRenderPass();
		}

		#region XNA->Vulkan Enum Conversion Class

		private class XNAToVK
		{
			public static VkFormat[] SurfaceFormat =
			{
				VkFormat.VK_FORMAT_R8G8B8A8_UNORM,		// SurfaceFormat.Color
				VkFormat.VK_FORMAT_R5G6B5_UNORM_PACK16,		// SurfaceFormat.Bgr565
				VkFormat.VK_FORMAT_R5G5B5A1_UNORM_PACK16,	// SurfaceFormat.Bgra5551
				VkFormat.VK_FORMAT_B4G4R4A4_UNORM_PACK16,	// SurfaceFormat.Bgra4444
				VkFormat.VK_FORMAT_BC1_RGBA_UNORM_BLOCK,	// SurfaceFormat.Dxt1
				VkFormat.VK_FORMAT_BC2_UNORM_BLOCK,		// SurfaceFormat.Dxt3
				VkFormat.VK_FORMAT_BC3_UNORM_BLOCK,		// SurfaceFormat.Dxt5
				VkFormat.VK_FORMAT_R8G8_SNORM,			// SurfaceFormat.NormalizedByte2
				VkFormat.VK_FORMAT_R4G4B4A4_UNORM_PACK16,	// SurfaceFormat.NormalizedByte4
				VkFormat.VK_FORMAT_A2R10G10B10_UNORM_PACK32,	// SurfaceFormat.Rgba1010102
				VkFormat.VK_FORMAT_R16G16_UNORM,		// SurfaceFormat.Rg32
				VkFormat.VK_FORMAT_R16G16B16A16_UNORM,		// SurfaceFormat.Rgba64
				VkFormat.VK_FORMAT_R8_UNORM,			// SurfaceFormat.Alpha8
				VkFormat.VK_FORMAT_R32_SFLOAT,			// SurfaceFormat.Single
				VkFormat.VK_FORMAT_R32G32_SFLOAT,		// SurfaceFormat.Vector2
				VkFormat.VK_FORMAT_R32G32B32A32_SFLOAT,		// SurfaceFormat.Vector4
				VkFormat.VK_FORMAT_R16_SFLOAT,			// SurfaceFormat.HalfSingle
				VkFormat.VK_FORMAT_R16G16_SFLOAT,		// SurfaceFormat.HalfVector2
				VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT,		// SurfaceFormat.HalfVector4
				VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT,		// SurfaceFormat.HdrBlendable
				VkFormat.VK_FORMAT_R8G8B8A8_UNORM		// SurfaceFormat.ColorBgraEXT
			};

			public static VkFormat[] DepthFormat =
			{
				VkFormat.VK_FORMAT_UNDEFINED,		// DepthFormat.None
				VkFormat.VK_FORMAT_D16_UNORM,		// DepthFormat.Depth16
				VkFormat.VK_FORMAT_X8_D24_UNORM_PACK32,	// DepthFormat.Depth24 (FIXME: Is this right?)
				VkFormat.VK_FORMAT_D24_UNORM_S8_UINT,	// DepthFormat.Depth24Stencil8
			};

			public static VkPresentModeKHR[] PresentInterval =
			{
				VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR,	// PresentInterval.Default
				VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR,	// PresentInterval.One
				VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR,	// PresentInterval.Two
				VkPresentModeKHR.VK_PRESENT_MODE_IMMEDIATE_KHR	// PresentInterval.Immediate
			};

			public static VkPrimitiveTopology[] PrimitiveType =
			{
				VkPrimitiveTopology.VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST,	// PrimitiveType.TriangleList
				VkPrimitiveTopology.VK_PRIMITIVE_TOPOLOGY_TRIANGLE_STRIP,	// PrimitiveType.TriangleStrip
				VkPrimitiveTopology.VK_PRIMITIVE_TOPOLOGY_LINE_LIST,		// PrimitiveType.LineList
				VkPrimitiveTopology.VK_PRIMITIVE_TOPOLOGY_LINE_STRIP,		// PrimitiveType.LineStrip
				VkPrimitiveTopology.VK_PRIMITIVE_TOPOLOGY_POINT_LIST		// PrimitiveType.PointListEXT
			};

			public static VkPolygonMode[] FillMode =
			{
				VkPolygonMode.VK_POLYGON_MODE_FILL,	// FillMode.Solid
				VkPolygonMode.VK_POLYGON_MODE_LINE	// FillMode.Wireframe
			};

			public static VkCullModeFlags[] CullMode =
			{
				VkCullModeFlags.VK_CULL_MODE_NONE,	// CullMode.None
				VkCullModeFlags.VK_CULL_MODE_FRONT_BIT,	// CullMode.CullClockwiseFace
				VkCullModeFlags.VK_CULL_MODE_FRONT_BIT	// CullMode.CullCounterClockwiseFace
			};

			public static VkFrontFace[] FrontFace =
			{
				VkFrontFace.VK_FRONT_FACE_CLOCKWISE,		// CullMode.None
				VkFrontFace.VK_FRONT_FACE_CLOCKWISE,		// CullMode.CullClockwiseFace
				VkFrontFace.VK_FRONT_FACE_COUNTER_CLOCKWISE	// CullMode.CullCounterClockwiseFace
			};

			public static VkCompareOp[] CompareFunction =
			{
				VkCompareOp.VK_COMPARE_OP_ALWAYS,	// CompareFunction.Always
				VkCompareOp.VK_COMPARE_OP_NEVER,	// CompareFunction.Never
				VkCompareOp.VK_COMPARE_OP_LESS,		// CompareFunction.Less
				VkCompareOp.VK_COMPARE_OP_LESS_OR_EQUAL,// CompareFunction.LessOrEqual
				VkCompareOp.VK_COMPARE_OP_EQUAL,	// CompareFunction.Equal
				VkCompareOp.VK_COMPARE_OP_GREATER_OR_EQUAL, // CompareFunction.GreaterOrEqual
				VkCompareOp.VK_COMPARE_OP_GREATER,	// CompareFunction.Greater
				VkCompareOp.VK_COMPARE_OP_NOT_EQUAL	// CompareFunction.NotEqual
			};

			public static VkStencilOp[] StencilOperation =
			{
				VkStencilOp.VK_STENCIL_OP_KEEP,			// StencilOperation.Keep
				VkStencilOp.VK_STENCIL_OP_ZERO,			// StencilOperation.Zero
				VkStencilOp.VK_STENCIL_OP_REPLACE,		// StencilOperation.Replace
				VkStencilOp.VK_STENCIL_OP_INCREMENT_AND_WRAP,	// StencilOperation.Increment
				VkStencilOp.VK_STENCIL_OP_DECREMENT_AND_WRAP,	// StencilOperation.Decrement
				VkStencilOp.VK_STENCIL_OP_INCREMENT_AND_CLAMP,	// StencilOperation.IncrementSaturation
				VkStencilOp.VK_STENCIL_OP_DECREMENT_AND_CLAMP,	// StencilOperation.DecrementSaturation
				VkStencilOp.VK_STENCIL_OP_INVERT		// StencilOperation.Invert
			};
		}

		#endregion
	}
}
