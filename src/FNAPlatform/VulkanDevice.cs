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
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using SDL2;
#endregion

namespace Microsoft.Xna.Framework.Graphics
{
	internal partial class VulkanDevice : IGLDevice
	{
		private IntPtr Instance;
		private IntPtr Device;

		public VulkanDevice(
			PresentationParameters presentationParameters,
			GraphicsAdapter adapter
		) {
			LoadGlobalEntryPoints();
			InitVulkanInstance(presentationParameters.DeviceWindowHandle);
			LoadInstanceEntryPoints(Instance);
			Device = CreateLogicalDevice(Instance);
		}

		private unsafe void InitVulkanInstance(IntPtr windowHandle)
		{
			// Describe app metadata
			VkApplicationInfo appInfo = new VkApplicationInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
				pNext = IntPtr.Zero,
				pApplicationName = UTF8_ToNative("FNA Game"), // FIXME: What should we say here?
				applicationVersion = VK_MAKE_VERSION(1, 0, 0),
				pEngineName = UTF8_ToNative("FNA"),
				engineVersion = VK_MAKE_VERSION(19, 9, 0), // FIXME: What version should go here?
				apiVersion = VK_MAKE_VERSION(1, 0, 0),
			};

			// Get all available instance extensions
			uint propertyCount;
			vkEnumerateInstanceExtensionProperties(null, out propertyCount, IntPtr.Zero);
			VkExtensionProperties[] allProperties = new VkExtensionProperties[propertyCount];
			vkEnumerateInstanceExtensionProperties(
				null,
				out propertyCount,
				Marshal.UnsafeAddrOfPinnedArrayElement(allProperties, 0)
			);

			foreach (VkExtensionProperties extprop in allProperties)
			{
				Console.WriteLine(UTF8_ToManaged(extprop.extensionName));
			}

			// List any instance extensions needed specifically for FNA
			IntPtr[] fnaExtensions = {
				// FIXME: What do we need?
			};

			// Get all extensions required for SDL2
			uint sdlExtCount;
			SDL.SDL_Vulkan_GetInstanceExtensions(windowHandle, out sdlExtCount, null);
			IntPtr[] sdlExtensions = new IntPtr[sdlExtCount];
			SDL.SDL_Vulkan_GetInstanceExtensions(windowHandle, out sdlExtCount, sdlExtensions);

			// Combine all the extensions into a master list
			IntPtr[] allExtensions = new IntPtr[(uint) fnaExtensions.Length + sdlExtCount];
			sdlExtensions.CopyTo(allExtensions, 0);
			fnaExtensions.CopyTo(allExtensions, sdlExtCount);

			// Create the Vulkan instance
			VkInstanceCreateInfo appCreateInfo = new VkInstanceCreateInfo
			{
				sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
				pNext = IntPtr.Zero,
				flags = 0,
				pApplicationInfo = &appInfo,
				enabledLayerCount = 0,
				ppEnabledLayerNames = IntPtr.Zero,
				enabledExtensionCount = (uint) allExtensions.Length,
				ppEnabledExtensionNames = Marshal.UnsafeAddrOfPinnedArrayElement(allExtensions, 0) // FIXME: Is there a better way?
			};

			VkResult res = vkCreateInstance((IntPtr) (&appCreateInfo), IntPtr.Zero, out Instance);
			if (res != VkResult.VK_SUCCESS)
			{
				throw new Exception("Could not create Vulkan Instance! Error: " + res);
			}

			// Clean up
			UTF8_FreeNativeStrings();
		}

		private IntPtr CreateLogicalDevice(IntPtr instance)
		{
			return IntPtr.Zero;
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
