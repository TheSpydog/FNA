#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2020 Ethan Lee and the MonoGame Team
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

namespace Microsoft.Xna.Framework.Graphics
{
	internal partial class D3D11Device : IGLDevice
	{
		private class D3D11Texture : IGLTexture
		{
			public IntPtr RenderTargetView;
		}

		private class D3D11Renderbuffer : IGLRenderbuffer
		{
			public IntPtr DepthStencilView;
		}

		private class D3D11Backbuffer : IGLBackbuffer
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

			public int MultiSampleCount
			{
				get;
				private set;
			}

			public DepthFormat DepthFormat
			{
				get;
				private set;
			}

			public IntPtr ColorBuffer = IntPtr.Zero;
			public IntPtr MultiSampleColorBuffer = IntPtr.Zero;
			public IntPtr DepthStencilBuffer = IntPtr.Zero;

			public IntPtr ColorView = IntPtr.Zero;
			public IntPtr MultiSampleView = IntPtr.Zero;
			public IntPtr DepthStencilView = IntPtr.Zero;

			private D3D11Device device;

			public D3D11Backbuffer(
				D3D11Device d3dDevice,
				PresentationParameters presentationParameters
			) {
				device = d3dDevice;
				Width = presentationParameters.BackBufferWidth;
				Height = presentationParameters.BackBufferHeight;
			}

			public void Dispose()
			{
				D3D11_DisposeBackbuffer(
					device.ctx,
					ref ColorBuffer,
					ref ColorView,
					ref MultiSampleColorBuffer,
					ref MultiSampleView,
					ref DepthStencilBuffer,
					ref DepthStencilView
				);
			}

			public void ResetFramebuffer(PresentationParameters presentationParameters)
			{
				Dispose();
				CreateFramebuffer(presentationParameters);
			}

			public void CreateFramebuffer(PresentationParameters presentationParameters)
			{
				// Update the backbuffer size
				int newWidth = presentationParameters.BackBufferWidth;
				int newHeight = presentationParameters.BackBufferHeight;
				// FIXME: Do we need to do anything about this...?
				Width = newWidth;
				Height = newHeight;

				DepthFormat = presentationParameters.DepthStencilFormat;
				MultiSampleCount = presentationParameters.MultiSampleCount; // FIXME: Make sure this is compatible with the device!

				// Get the HWND
				IntPtr hwnd = IntPtr.Zero;
				SDL.SDL_SysWMinfo wmInfo = new SDL.SDL_SysWMinfo();
				SDL.SDL_VERSION(out wmInfo.version);
				SDL.SDL_GetWindowWMInfo(
					presentationParameters.DeviceWindowHandle,
					ref wmInfo
				);
				if (wmInfo.subsystem == SDL.SDL_SYSWM_TYPE.SDL_SYSWM_WINDOWS)
				{
					hwnd = wmInfo.info.win.window;
				}

				D3D11_CreateFramebuffer(
					device.ctx,
					hwnd,
					Width,
					Height,
					DepthFormat,
					MultiSampleCount,
					out ColorBuffer,
					out ColorView,
					out MultiSampleColorBuffer,
					out MultiSampleView,
					out DepthStencilBuffer,
					out DepthStencilView
				);

				// // Update the Texture representation
				// Texture = new MetalTexture(
				// 	ColorBuffer,
				// 	Width,
				// 	Height,
				// 	SurfaceFormat.Color,
				// 	1,
				// 	true
				// );

				// This is the default render target
				device.SetRenderTargets(null, null, DepthFormat.None);
			}
		}

		private class D3D11Effect : IGLEffect
		{
			public IntPtr EffectData => throw new NotImplementedException();
		}

		private class D3D11Query : IGLQuery
		{

		}

		private class D3D11Buffer : IGLBuffer
		{
			public IntPtr BufferSize => throw new NotImplementedException();
		}

		public Color BlendFactor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public int MultiSampleMask { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public int ReferenceStencil { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

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

		public bool SupportsNoOverwrite
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

		public IGLBackbuffer Backbuffer
		{
			get;
			private set;
		}

		// An opaque pointer to an internal context struct
		private IntPtr ctx;

		public D3D11Device(
			PresentationParameters presentationParameters,
			GraphicsAdapter adapter
		) {
			int maxTextureSlots;
			bool supportsDxt1;
			bool supportsDxt3;
			bool supportsHardwareInstancing;
			int maxMultiSampleCount;
			bool supportsNoOverwrite;

			int result = D3D11_InitContext(
				out ctx,
				out maxTextureSlots,
				out maxMultiSampleCount,
				out supportsDxt1,
				out supportsDxt3,
				out supportsHardwareInstancing,
				out supportsNoOverwrite
			);
			if (result != 0)
			{
				throw new Exception("The DirectX device could not be created! Error code: " + result);
			}

			MaxTextureSlots = maxTextureSlots;
			SupportsDxt1 = supportsDxt1;
			SupportsS3tc = supportsDxt3;
			SupportsHardwareInstancing = supportsHardwareInstancing;
			MaxMultiSampleCount = maxMultiSampleCount;
			SupportsNoOverwrite = supportsNoOverwrite;

			InitializeFauxBackbuffer(presentationParameters);
		}

		private void InitializeFauxBackbuffer(
			PresentationParameters presentationParameters
		) {
			D3D11Backbuffer backbuffer = new D3D11Backbuffer(
				this,
				presentationParameters
			);
			Backbuffer = backbuffer;
			backbuffer.CreateFramebuffer(presentationParameters);
			D3D11_InitializeFauxBackbuffer(
				ctx,
				Environment.GetEnvironmentVariable("FNA_GRAPHICS_BACKBUFFER_SCALE_NEAREST") == "1"
			);
		}

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

		public void BeginFrame()
		{
			D3D11_BeginFrame(ctx);
		}

		public void BeginPassRestore(IGLEffect effect, IntPtr stateChanges)
		{
			throw new NotImplementedException();
		}

		public void Clear(ClearOptions options, Vector4 color, float depth, int stencil)
		{
			D3D11_Clear(ctx, options, color, depth, stencil);
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

		public IGLTexture CreateTexture2D(SurfaceFormat format, int width, int height, int levelCount, bool isRenderTarget)
		{
			throw new NotImplementedException();
		}

		public IGLTexture CreateTexture3D(SurfaceFormat format, int width, int height, int depth, int levelCount)
		{
			throw new NotImplementedException();
		}

		public IGLTexture CreateTextureCube(SurfaceFormat format, int size, int levelCount, bool isRenderTarget)
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			// FIXME: Dispose more stuff!
			(Backbuffer as D3D11Backbuffer).Dispose();
			//D3D11_DisposeContext();
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

		public IGLBuffer GenIndexBuffer(bool dynamic, BufferUsage usage, int indexCount, IndexElementSize indexElementSize)
		{
			throw new NotImplementedException();
		}

		public IGLRenderbuffer GenRenderbuffer(int width, int height, SurfaceFormat format, int multiSampleCount, IGLTexture texture)
		{
			throw new NotImplementedException();
		}

		public IGLRenderbuffer GenRenderbuffer(int width, int height, DepthFormat format, int multiSampleCount)
		{
			throw new NotImplementedException();
		}

		public IGLBuffer GenVertexBuffer(bool dynamic, BufferUsage usage, int vertexCount, int vertexStride)
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
			D3D11_SetPresentationInterval(ctx, presentInterval);
		}

		private IntPtr[] renderTargetViews = new IntPtr[4];

		public void SetRenderTargets(
			RenderTargetBinding[] renderTargets,
			IGLRenderbuffer renderbuffer,
			DepthFormat depthFormat
		) {
			// FIXME: MSAA?

			int len = 1;
			IntPtr dsView = IntPtr.Zero;

			if (renderTargets == null)
			{
				renderTargetViews[0] = (Backbuffer as D3D11Backbuffer).ColorView;
				dsView = (Backbuffer as D3D11Backbuffer).DepthStencilView;
			}
			else
			{
				// Stuff all the views into one array
				len = renderTargets.Length;
				for (int i = 0; i < len; i += 1)
				{
					D3D11Texture rt = (D3D11Texture) renderTargets[i].RenderTarget.texture;
					renderTargetViews[i] = rt.RenderTargetView;
				}

				// Don't forget the depth stencil view!
				if (renderbuffer != null)
				{
					dsView = (renderbuffer as D3D11Renderbuffer).DepthStencilView;
				}
			}

			// Set the targets
			GCHandle arrHandle = GCHandle.Alloc(renderTargetViews, GCHandleType.Pinned);
			D3D11_SetRenderTargets(
				ctx,
				len,
				Marshal.UnsafeAddrOfPinnedArrayElement(renderTargetViews, 0),
				dsView
			);
			arrHandle.Free();
		}

		public void SetScissorRect(Rectangle scissorRect)
		{
			D3D11_SetScissorRect(ctx, scissorRect);
		}

		public void SetStringMarker(string text)
		{
			throw new NotImplementedException();
		}

		public void SetTextureData2D(IGLTexture texture, SurfaceFormat format, int x, int y, int w, int h, int level, IntPtr data, int dataLength)
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

		public void SetTextureDataYUV(Texture2D[] textures, IntPtr ptr)
		{
			throw new NotImplementedException();
		}

		public void SetVertexBufferData(IGLBuffer buffer, int offsetInBytes, IntPtr data, int dataLength, SetDataOptions options)
		{
			throw new NotImplementedException();
		}

		public void SetViewport(Viewport vp)
		{
			D3D11_SetViewport(ctx, vp);
		}

		// FIXME: Move this!
		Rectangle fauxBackbufferDestBounds;
		bool fauxBackbufferSizeChanged;
		IntPtr fauxBackbufferDrawBuffer;

		public void SwapBuffers(
			Rectangle? sourceRectangle,
			Rectangle? destinationRectangle,
			IntPtr overrideWindowHandle
		) {
			// Determine the regions to present
			Rectangle srcRect;
			Rectangle dstRect;
			if (sourceRectangle.HasValue)
			{
				srcRect.X = sourceRectangle.Value.X;
				srcRect.Y = sourceRectangle.Value.Y;
				srcRect.Width = sourceRectangle.Value.Width;
				srcRect.Height = sourceRectangle.Value.Height;
			}
			else
			{
				srcRect.X = 0;
				srcRect.Y = 0;
				srcRect.Width = Backbuffer.Width;
				srcRect.Height = Backbuffer.Height;
			}
			if (destinationRectangle.HasValue)
			{
				dstRect.X = destinationRectangle.Value.X;
				dstRect.Y = destinationRectangle.Value.Y;
				dstRect.Width = destinationRectangle.Value.Width;
				dstRect.Height = destinationRectangle.Value.Height;
			}
			else
			{
				dstRect.X = 0;
				dstRect.Y = 0;
				SDL.SDL_GetWindowSize(
					overrideWindowHandle,
					out dstRect.Width,
					out dstRect.Height
				);
			}

			// Update cached vertex buffer if needed
			if (fauxBackbufferDestBounds != dstRect || fauxBackbufferSizeChanged)
			{
				fauxBackbufferDestBounds = dstRect;
				fauxBackbufferSizeChanged = false;

				// Scale the coordinates to (-1, 1)
				int dw, dh;
				SDL.SDL_GetWindowSize(overrideWindowHandle, out dw, out dh);
				float sx = -1 + (dstRect.X / (float) dw);
				float sy = -1 + (dstRect.Y / (float) dh);
				float sw = (dstRect.Width / (float) dw) * 2;
				float sh = (dstRect.Height / (float) dh) * 2;

				// Update the vertex buffer contents
				float[] data = new float[]
				{
					sx, sy,	0, 1,		0, 0,
					sx, sy + sh, 0, 1,	0, 1,
					sx + sw, sy + sh, 0, 1,	1, 1,
					sx + sw, sy, 0, 1,	1, 0
				};
				GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
				D3D11_SetFauxBackbufferData(
					ctx,
					Marshal.UnsafeAddrOfPinnedArrayElement(data, 0),
					sizeof(float) * data.Length
				);
				handle.Free();
			}

			D3D11_SwapBuffers(ctx, srcRect, dstRect);
		}

		public void VerifySampler(int index, Texture texture, SamplerState sampler)
		{
			throw new NotImplementedException();
		}

		// Imports
		private const string nativeLib = "FNA-D3D11.dll";

		[DllImport(nativeLib)]
		private static extern int D3D11_InitContext(
			out IntPtr ppContext,
			out int pMaxTextureSlots,
			out int pMaxMultiSampleCount,
			out bool pSupportsDxt1,
			out bool pSupportsDxt3,
			out bool pSupportsHardwareInstancing,
			out bool pSupportsNoOverwrite
		);

		[DllImport(nativeLib)]
		private static extern void D3D11_SetViewport(
			IntPtr pContext,
			Viewport viewport
		);

		[DllImport(nativeLib)]
		private static extern void D3D11_SetScissorRect(
			IntPtr pContext,
			Rectangle scissorRect
		);

		[DllImport(nativeLib)]
		private static extern void D3D11_SetPresentationInterval(
			IntPtr pContext,
			PresentInterval presentInterval
		);

		[DllImport(nativeLib)]
		private static extern void D3D11_BeginFrame(IntPtr pContext);

		[DllImport(nativeLib)]
		private static extern void D3D11_Clear(
			IntPtr pContext,
			ClearOptions clearOptions,
			Vector4 clearColor,
			float clearDepth,
			int clearStencil
		);

		[DllImport(nativeLib)]
		private static extern void D3D11_SetRenderTargets(
			IntPtr pContext,
			int numRenderTargets,
			IntPtr ppRenderTargetViews,
			IntPtr pDepthStencilView
		);

		[DllImport(nativeLib)]
		private static extern void D3D11_CreateFramebuffer(
			IntPtr pContext,
			IntPtr hwnd,
			int width,
			int height,
			DepthFormat depthFormat,
			int multiSampleCount,
			out IntPtr ppColorBuffer,
			out IntPtr ppColorView,
			out IntPtr ppMultiSampleColorBuffer,
			out IntPtr ppMultiSampleView,
			out IntPtr ppDepthStencilBuffer,
			out IntPtr ppDepthStencilView
		);

		[DllImport(nativeLib)]
		private static extern void D3D11_DisposeBackbuffer(
			IntPtr pContext,
			ref IntPtr ppColorBuffer,
			ref IntPtr ppColorView,
			ref IntPtr ppMultiSampleColorBuffer,
			ref IntPtr ppMultiSampleView,
			ref IntPtr ppDepthStencilBuffer,
			ref IntPtr ppDepthStencilView
		);

		[DllImport(nativeLib)]
		private static extern void D3D11_SwapBuffers(
			IntPtr pContext,
			Rectangle srcRect,
			Rectangle dstRect
		);

		[DllImport(nativeLib)]
		private static extern int D3D11_InitializeFauxBackbuffer(
			IntPtr pContext,
			bool scaleNearest
		);

		[DllImport(nativeLib)]
		private static extern void D3D11_SetFauxBackbufferData(
			IntPtr pContext,
			IntPtr pData,
			int dataLen
		);
	}
}