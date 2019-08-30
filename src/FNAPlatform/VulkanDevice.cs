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
using Microsoft.Xna.Framework.Graphics;
using SDL2;
#endregion

namespace Microsoft.Xna.Framework.Graphics
{
	internal partial class VulkanDevice : IGLDevice
	{
		private IntPtr Instance;
		private IntPtr PhysicalDevice;
		private IntPtr DebugMessenger;

		private int graphicsQueueFamilyIndex;

		private bool renderdocEnabled;
		private bool validationEnabled;

		public VulkanDevice(
			PresentationParameters presentationParameters,
			GraphicsAdapter adapter
		) {
			renderdocEnabled = Environment.GetEnvironmentVariable("FNA_VULKAN_ENABLE_RENDERDOC") == "1";
			validationEnabled = Environment.GetEnvironmentVariable("FNA_VULKAN_ENABLE_VALIDATION") == "1";

			LoadGlobalEntryPoints();
			InitVulkanInstance(presentationParameters.DeviceWindowHandle);
			LoadInstanceEntryPoints();
			if (validationEnabled)
			{
				InitDebugMessenger();
			}
			SelectPhysicalDevice();
			CreateLogicalDevice();
		}

		private unsafe bool InstanceExtensionSupported(string extName, VkExtensionProperties[] extensions)
		{
			foreach (VkExtensionProperties ext in extensions)
			{
				if (UTF8_ToManaged(ext.extensionName) == extName)
				{
					return true;
				}
			}

			return false;
		}

		private unsafe bool InstanceLayerSupported(string layerName, VkLayerProperties[] layers)
		{
			foreach (VkLayerProperties layer in layers)
			{
				if (UTF8_ToManaged(layer.layerName) == layerName)
				{
					return true;
				}
			}

			return false;
		}

		private unsafe void InitVulkanInstance(IntPtr windowHandle)
		{
			// Describe app metadata
			string appName = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
			VkApplicationInfo appInfo = new VkApplicationInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
				pNext = IntPtr.Zero,
				pApplicationName = UTF8_ToNative(appName),
				applicationVersion = VK_MAKE_VERSION(1, 0, 0),
				pEngineName = UTF8_ToNative("FNA"),
				engineVersion = VK_MAKE_VERSION(19, 9, 0), // FIXME: What version should go here?
				apiVersion = VK_MAKE_VERSION(1, 0, 0),
			};

			// Get all available instance extensions
			uint availableExtensionCount;
			vkEnumerateInstanceExtensionProperties(null, out availableExtensionCount, null);
			VkExtensionProperties[] availableExtensions = new VkExtensionProperties[availableExtensionCount];
			fixed (VkExtensionProperties* ptr = availableExtensions)
			{
				vkEnumerateInstanceExtensionProperties(null, out availableExtensionCount, ptr);
			}

			// Generate a list of all instance extensions we will use
			List<IntPtr> extensions = new List<IntPtr>();

			uint sdlExtCount;
			SDL.SDL_Vulkan_GetInstanceExtensions(windowHandle, out sdlExtCount, null);
			IntPtr[] sdlExtensions = new IntPtr[sdlExtCount];
			SDL.SDL_Vulkan_GetInstanceExtensions(windowHandle, out sdlExtCount, sdlExtensions);
			extensions.AddRange(sdlExtensions);

			if (validationEnabled)
			{
				string debugUtilsExt = "VK_EXT_debug_utils";
				if (InstanceExtensionSupported(debugUtilsExt, availableExtensions))
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
			fixed (VkLayerProperties* ptr = availableLayers)
			{
				vkEnumerateInstanceLayerProperties(out availableLayerCount, ptr);
			}

			// Generate a list of all validation layers we will use
			List<IntPtr> layers = new List<IntPtr>();

			if (renderdocEnabled)
			{
				string layername = "VK_LAYER_RENDERDOC_Capture";
				if (InstanceLayerSupported(layername, availableLayers))
				{
					layers.Add(UTF8_ToNative(layername));
				}
				else
				{
					FNALoggerEXT.LogWarn(layername + " not supported!");
				}
			}
			if (validationEnabled)
			{
				layers.Add(UTF8_ToNative("VK_LAYER_KHRONOS_validation"));
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
						pNext = IntPtr.Zero,
						flags = 0,
						pApplicationInfo = &appInfo,
						enabledLayerCount = (uint) layersArray.Length,
						ppEnabledLayerNames = (IntPtr) layerNamesPtr,
						enabledExtensionCount = (uint) extensionsArray.Length,
						ppEnabledExtensionNames = (IntPtr) extNamesPtr
					};
				}
			}

			VkResult res = vkCreateInstance((IntPtr) (&appCreateInfo), IntPtr.Zero, out Instance);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception("Could not create Vulkan Instance! Error: " + res);
			}

			// Clean up
			UTF8_FreeNativeStrings();
		}

		private unsafe void InitDebugMessenger()
		{
			PFN_vkDebugUtilsMessengerCallbackEXT callback = DebugCallback;

			VkDebugUtilsMessageSeverityFlagBitsEXT severityFlags =
				  VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT
				| VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_INFO_BIT_EXT
				| VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_VERBOSE_BIT_EXT
				| VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT;

			VkDebugUtilsMessageTypeFlagBitsEXT messageFlags =
				  VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT
				| VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT
				| VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT;

			VkDebugUtilsMessengerCreateInfoEXT createInfo = new VkDebugUtilsMessengerCreateInfoEXT
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_UTILS_MESSENGER_CREATE_INFO_EXT,
				pNext = IntPtr.Zero,
				flags = 0,
				messageSeverity = severityFlags,
				messageType = messageFlags,
				pfnUserCallback = Marshal.GetFunctionPointerForDelegate(callback),
				pUserData = IntPtr.Zero
			};

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
			string message = UTF8_ToManaged((byte*) pCallbackData->pMessage);

			if (messageSeverity == VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT)
			{
				// FIXME: Should we throw an exception here? -caleb
				FNALoggerEXT.LogError(message);
			}
			else if (messageSeverity == VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT)
			{
				FNALoggerEXT.LogWarn(message);
			}
			else
			{
				FNALoggerEXT.LogInfo(message);
			}

			UTF8_FreeNativeStrings();
			return 0;
		}

		private unsafe void SelectPhysicalDevice()
		{
			// Get all physical devices
			uint physicalDeviceCount;
			vkEnumeratePhysicalDevices(Instance, out physicalDeviceCount, null);
			IntPtr[] physicalDevices = new IntPtr[physicalDeviceCount];
			fixed (IntPtr* ptr = physicalDevices)
			{
				vkEnumeratePhysicalDevices(Instance, out physicalDeviceCount, ptr);
			}

			// Search for the ideal physical device
			int[] scores = new int[physicalDeviceCount];
			int[] graphicsQueueIndices = new int[physicalDeviceCount];
			for (int i = 0; i < scores.Length; i += 1)
			{
				// Get all supported queue families
				uint queueFamilyCount;
				vkGetPhysicalDeviceQueueFamilyProperties(physicalDevices[i], out queueFamilyCount, null);
				VkQueueFamilyProperties[] queueFamilies = new VkQueueFamilyProperties[queueFamilyCount];
				fixed (VkQueueFamilyProperties* ptr = queueFamilies)
				{
					vkGetPhysicalDeviceQueueFamilyProperties(physicalDevices[i], out queueFamilyCount, ptr);
				}

				// The physical device MUST have at least one graphics queue family
				bool supportsGraphicsQueues = false;
				for (int j = 0; j < queueFamilyCount; j += 1)
				{
					if ((queueFamilies[j].queueFlags & VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT) != 0)
					{
						// This queue family supports graphics!
						supportsGraphicsQueues = true;
						graphicsQueueIndices[i] = j;
						break;
					}
				}
				if (!supportsGraphicsQueues)
				{
					// There's no graphics support on this GPU. Skip!
					scores[i] = int.MinValue;
					continue;
				}

				// Check for optional properties and features
				VkPhysicalDeviceProperties props;
				VkPhysicalDeviceFeatures features;
				vkGetPhysicalDeviceProperties(physicalDevices[i], out props);
				vkGetPhysicalDeviceFeatures(physicalDevices[i], out features);

				// Discrete GPU > Integrated GPU > Anything else
				if (props.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU)
					scores[i] += 100;
				else if (props.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU)
					scores[i] += 50;

				// FIXME: What else do we care about...? -caleb
			}

			// Determine the winner
			int bestScore = -1;
			for (int i = 0; i < physicalDeviceCount; i += 1)
			{
				if (scores[i] > bestScore)
				{
					bestScore = scores[i];
					PhysicalDevice = physicalDevices[i];

					// We need this when we create the logical device.
					graphicsQueueFamilyIndex = graphicsQueueIndices[i];
				}
			}
			if (bestScore == -1)
			{
				throw new NoSuitableGraphicsDeviceException("No Vulkan compatible GPUs detected!");
			}
		}

		private void CreateLogicalDevice()
		{

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
