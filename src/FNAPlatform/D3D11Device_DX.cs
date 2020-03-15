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
		#region DirectX Libraries

		private const string d3d11Lib = "d3d11.dll";
		private const string dxgiLib = "dxgi.dll";

		#endregion

		#region Private D3D11 Enums

		private enum HRESULT : uint
		{
			S_OK = 0,
			S_FALSE = 1,
			E_NOTIMPL = 0x80004001,
			E_OUTOFMEMORY = 0x8007000E,
			E_INVALIDARG = 0x80070057,
			E_FAIL = 0x80004005,
			D3DERR_WAS_STILL_DRAWING = 0x887A000A,
			D3DERR_INVALID_CALL = 0x887A0001,
			D3D11_ERROR_DEFERRED_CONTEXT_MAP_WITHOUT_INITIAL_DISCARD = 0x887C0004,
			D3D11_ERROR_TOO_MANY_UNIQUE_VIEW_OBJECTS = 0x887C0003,
			D3D11_ERROR_TOO_MANY_UNIQUE_STATE_OBJECTS = 0x887C0001,
			D3D11_ERROR_FILE_NOT_FOUND = 0x887C0002
		}

		private enum D3D_DRIVER_TYPE
		{
			UNKNOWN,
			HARDWARE,
			REFERENCE,
			NULL,
			SOFTWARE,
			WARP
		}

		private enum D3D_FEATURE_LEVEL
		{
			LEVEL_9_1 = 37120,
			LEVEL_9_2 = 37376,
			LEVEL_9_3 = 37632,
			LEVEL_10_0 = 40960,
			LEVEL_10_1 = 41216,
			LEVEL_11_0 = 45056,
			LEVEL_11_1 = 45312,
			LEVEL_12_0 = 49152,
			LEVEL_12_1 = 49408
		}

		[Flags]
		private enum D3D11_CREATE_DEVICE_FLAG
		{
			SINGLETHREADED = 1,
			DEBUG = 2,
			SWITCH_TO_REF = 4,
			PREVENT_INTERNAL_THREADING_OPTIMIZATIONS = 8,
			BGRA_SUPPORT = 32,
			DEBUGGABLE = 64,
			PREVENT_ALTERING_LAYER_SETTINGS_FROM_REGISTRY = 128,
			DISABLE_GPU_TIMEOUT = 256,
			VIDEO_SUPPORT = 2048
		}

		private enum DXGI_SWAP_EFFECT
		{
			DISCARD,
			SEQUENTIAL,
			FLIP_SEQUENTIAL,
			FLIP_DISCARD
		}

		private enum DXGI_FORMAT : uint
		{
			UNKNOWN,
			R32G32B32A32_TYPELESS,
			R32G32B32A32_FLOAT,
			R32G32B32A32_UINT,
			R32G32B32A32_SINT,
			R32G32B32_TYPELESS,
			R32G32B32_FLOAT,
			R32G32B32_UINT,
			R32G32B32_SINT,
			R16G16B16A16_TYPELESS,
			R16G16B16A16_FLOAT,
			R16G16B16A16_UNORM,
			R16G16B16A16_UINT,
			R16G16B16A16_SNORM,
			R16G16B16A16_SINT,
			R32G32_TYPELESS,
			R32G32_FLOAT,
			R32G32_UINT,
			R32G32_SINT,
			R32G8X24_TYPELESS,
			D32_FLOAT_S8X24_UINT,
			R32_FLOAT_X8X24_TYPELESS,
			X32_TYPELESS_G8X24_UINT,
			R10G10B10A2_TYPELESS,
			R10G10B10A2_UNORM,
			R10G10B10A2_UINT,
			R11G11B10_FLOAT,
			R8G8B8A8_TYPELESS,
			R8G8B8A8_UNORM,
			R8G8B8A8_UNORM_SRGB,
			R8G8B8A8_UINT,
			R8G8B8A8_SNORM,
			R8G8B8A8_SINT,
			R16G16_TYPELESS,
			R16G16_FLOAT,
			R16G16_UNORM,
			R16G16_UINT,
			R16G16_SNORM,
			R16G16_SINT,
			R32_TYPELESS,
			D32_FLOAT,
			R32_FLOAT,
			R32_UINT,
			R32_SINT,
			R24G8_TYPELESS,
			D24_UNORM_S8_UINT,
			R24_UNORM_X8_TYPELESS,
			X24_TYPELESS_G8_UINT,
			R8G8_TYPELESS,
			R8G8_UNORM,
			R8G8_UINT,
			R8G8_SNORM,
			R8G8_SINT,
			R16_TYPELESS,
			R16_FLOAT,
			D16_UNORM,
			R16_UNORM,
			R16_UINT,
			R16_SNORM,
			R16_SINT,
			R8_TYPELESS,
			R8_UNORM,
			R8_UINT,
			R8_SNORM,
			R8_SINT,
			A8_UNORM,
			R1_UNORM,
			R9G9B9E5_SHAREDEXP,
			R8G8_B8G8_UNORM,
			G8R8_G8B8_UNORM,
			BC1_TYPELESS,
			BC1_UNORM,
			BC1_UNORM_SRGB,
			BC2_TYPELESS,
			BC2_UNORM,
			BC2_UNORM_SRGB,
			BC3_TYPELESS,
			BC3_UNORM,
			BC3_UNORM_SRGB,
			BC4_TYPELESS,
			BC4_UNORM,
			BC4_SNORM,
			BC5_TYPELESS,
			BC5_UNORM,
			BC5_SNORM,
			B5G6R5_UNORM,
			B5G5R5A1_UNORM,
			B8G8R8A8_UNORM,
			B8G8R8X8_UNORM,
			R10G10B10_XR_BIAS_A2_UNORM,
			B8G8R8A8_TYPELESS,
			B8G8R8A8_UNORM_SRGB,
			B8G8R8X8_TYPELESS,
			B8G8R8X8_UNORM_SRGB,
			BC6H_TYPELESS,
			BC6H_UF16,
			BC6H_SF16,
			BC7_TYPELESS,
			BC7_UNORM,
			BC7_UNORM_SRGB,
			AYUV,
			Y410,
			Y416,
			NV12,
			P010,
			P016,
			OPAQUE420,
			YUY2,
			Y210,
			Y216,
			NV11,
			AI44,
			IA44,
			P8,
			A8P8,
			B4G4R4A4_UNORM,
			P208,
			V208,
			V408
		}

		private enum DXGI_MODE_SCANLINE_ORDER
		{ 
			DXGI_MODE_SCANLINE_ORDER_UNSPECIFIED,
			DXGI_MODE_SCANLINE_ORDER_PROGRESSIVE,
			DXGI_MODE_SCANLINE_ORDER_UPPER_FIELD_FIRST,
			DXGI_MODE_SCANLINE_ORDER_LOWER_FIELD_FIRST
		}

		private enum DXGI_MODE_SCALING
		{
			UNSPECIFIED,
			CENTERED,
			STRETCHED
		}

		private enum DXGI_SCALING
		{
			DXGI_SCALING_STRETCH,
			DXGI_SCALING_NONE,
			DXGI_SCALING_ASPECT_RATIO_STRETCH
		}

		[Flags]
		private enum DXGI_USAGE : long
		{
			SHADER_INPUT = 1L << (0 + 4),
			RENDER_TARGET_OUTPUT = 1L << (1 + 4),
			BACK_BUFFER = 1L << (2 + 4),
			SHARED = 1L << (3 + 4),
			READ_ONLY = 1L << (4 + 4),
			DISCARD_ON_PRESENT = 1L << (5 + 4),
			UNORDERED_ACCESS = 1L << (6 + 4)
		}

		private enum DXGI_ALPHA_MODE
		{
			UNSPECIFIED,
			PREMULTIPLIED,
			STRAIGHT,
			IGNORE,
			FORCE_DWORD
		}

		// FIXME: These probably have different values!
		[Flags]
		private enum DXGI_SWAP_CHAIN_FLAG
		{
			NONPREROTATED = 1,
			ALLOW_MODE_SWITCH,
			GDI_COMPATIBLE,
			RESTRICTED_CONTENT,
			RESTRICT_SHARED_RESOURCE_DRIVER,
			DISPLAY_ONLY,
			FRAME_LATENCY_WAITABLE_OBJECT,
			FOREGROUND_LAYER,
			FULLSCREEN_VIDEO,
			YUV_VIDEO,
			HW_PROTECTED,
			ALLOW_TEARING,
			RESTRICTED_TO_ALL_HOLOGRAPHIC_DISPLAYS
		}

		#endregion

		#region Private D3D11 Structs

		private struct DXGI_RATIONAL
		{
			public uint Numerator;
			public uint Denominator;
		}

		private struct DXGI_SAMPLE_DESC
		{
			public uint Count;
			public uint Quality;
		}

		struct DXGI_SWAP_CHAIN_DESC1
		{
			public uint Width;
			public uint Height;
			public DXGI_FORMAT Format;
			public bool Stereo;
			public DXGI_SAMPLE_DESC SampleDesc;
			public DXGI_USAGE BufferUsage;
			public uint BufferCount;
			public DXGI_SCALING Scaling;
			public DXGI_SWAP_EFFECT SwapEffect;
			public DXGI_ALPHA_MODE AlphaMode;
			public DXGI_SWAP_CHAIN_FLAG Flags;
		}

		struct DXGI_SWAP_CHAIN_FULLSCREEN_DESC
		{
			public DXGI_RATIONAL RefreshRate;
			public DXGI_MODE_SCANLINE_ORDER ScanlineOrdering;
			public DXGI_MODE_SCALING Scaling;
			public bool Windowed;
		}

		#endregion

		#region Private D3D Entry Points

		[DllImport(d3d11Lib, CallingConvention = CallingConvention.StdCall)]
		private static extern HRESULT D3D11CreateDevice(
			IntPtr adapter,			/* IDXGIAdapter* */
			D3D_DRIVER_TYPE driverType,
			IntPtr software,		/* HMODULE */
			D3D11_CREATE_DEVICE_FLAG Flags,
			IntPtr featureLevels,		/* D3D_FEATURE_LEVEL* */
			uint numFeatureLevels,
			uint SDKVersion,
			out IntPtr device,		/* ID3D11Device** */
			out D3D_FEATURE_LEVEL featureLevel,
			out IntPtr immediateContext	/* ID3D11DeviceContext** */
		);

		[DllImport(dxgiLib, EntryPoint = "IDXGIFactory2_CreateSwapChainForHwnd", CallingConvention = CallingConvention.StdCall)]
		private static extern HRESULT CreateSwapChainForHwnd(
			IntPtr pDevice,
			IntPtr hWnd,
			ref DXGI_SWAP_CHAIN_DESC1 pDesc,
			ref DXGI_SWAP_CHAIN_FULLSCREEN_DESC pFullscreenDesc,
			IntPtr pRestrictToOutput,	/* IDXGIOutput* */
			out IntPtr ppSwapChain		/* IDXGISwapChain1** */
		);

		#endregion

		// #region XNA->D3D11 Enum Conversion Class

		// private static class XNAToD3D
		// {
		// 	public static readonly DXGI_FORMAT[] TextureFormat = new DXGI_FORMAT[]
		// 	{
		// 		DXGI_FORMAT.R8G8B8A8_UNORM,	// SurfaceFormat.Color
		// 		DXGI_FORMAT.B5G6R5_UNORM,	// SurfaceFormat.Bgr565
		// 		DXGI_FORMAT.B5G5R5A1_UNORM,	// SurfaceFormat.Bgra5551
		// 		DXGI_FORMAT.B4G4R4A4_UNORM,	// SurfaceFormat.Bgra4444
		// 		DXGI_FORMAT.BC1_UNORM,          // SurfaceFormat.Dxt1
		// 		DXGI_FORMAT.BC2_UNORM,          // SurfaceFormat.Dxt3
		// 		DXGI_FORMAT.BC3_UNORM,          // SurfaceFormat.Dxt5
		// 		DXGI_FORMAT.R8G8_SNORM,         // SurfaceFormat.NormalizedByte2
		// 		DXGI_FORMAT.R16G16_SNORM,	// SurfaceFormat.NormalizedByte4
		// 		DXGI_FORMAT.R10G10B10A2_UNORM,	// SurfaceFormat.Rgba1010102
		// 		DXGI_FORMAT.R16G16_UNORM,	// SurfaceFormat.Rg32
		// 		DXGI_FORMAT.R16G16B16A16_UNORM,	// SurfaceFormat.Rgba64
		// 		DXGI_FORMAT.A8_UNORM,		// SurfaceFormat.Alpha8
		// 		DXGI_FORMAT.R32_FLOAT,	        // SurfaceFormat.Single
		// 		DXGI_FORMAT.R32G32_FLOAT,	// SurfaceFormat.Vector2
		// 		DXGI_FORMAT.R32G32B32A32_FLOAT,	// SurfaceFormat.Vector4
		// 		DXGI_FORMAT.R16_FLOAT,	        // SurfaceFormat.HalfSingle
		// 		DXGI_FORMAT.R16G16_FLOAT,	// SurfaceFormat.HalfVector2
		// 		DXGI_FORMAT.R16G16B16A16_FLOAT,	// SurfaceFormat.HalfVector4
		// 		DXGI_FORMAT.R16G16B16A16_FLOAT,	// SurfaceFormat.HdrBlendable
		// 		DXGI_FORMAT.B8G8R8A8_UNORM	// SurfaceFormat.ColorBgraEXT
		// 	};

		// 	public static readonly MojoShader.MOJOSHADER_usage[] VertexAttribUsage = new MojoShader.MOJOSHADER_usage[]
		// 	{
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_POSITION,		// VertexElementUsage.Position
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_COLOR,		// VertexElementUsage.Color
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_TEXCOORD,		// VertexElementUsage.TextureCoordinate
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_NORMAL,		// VertexElementUsage.Normal
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_BINORMAL,		// VertexElementUsage.Binormal
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_TANGENT,		// VertexElementUsage.Tangent
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_BLENDINDICES,	// VertexElementUsage.BlendIndices
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_BLENDWEIGHT,	// VertexElementUsage.BlendWeight
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_DEPTH,		// VertexElementUsage.Depth
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_FOG,		// VertexElementUsage.Fog
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_POINTSIZE,		// VertexElementUsage.PointSize
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_SAMPLE,		// VertexElementUsage.Sample
		// 		MojoShader.MOJOSHADER_usage.MOJOSHADER_USAGE_TESSFACTOR		// VertexElementUsage.TessellateFactor
		// 	};

		// 	// public static readonly MTLVertexFormat[] VertexAttribType = new MTLVertexFormat[]
		// 	// {
		// 	// 	MTLVertexFormat.Float,			// VertexElementFormat.Single
		// 	// 	MTLVertexFormat.Float2,			// VertexElementFormat.Vector2
		// 	// 	MTLVertexFormat.Float3,			// VertexElementFormat.Vector3
		// 	// 	MTLVertexFormat.Float4,			// VertexElementFormat.Vector4
		// 	// 	MTLVertexFormat.UChar4Normalized,	// VertexElementFormat.Color
		// 	// 	MTLVertexFormat.UChar4,			// VertexElementFormat.Byte4
		// 	// 	MTLVertexFormat.Short2,			// VertexElementFormat.Short2
		// 	// 	MTLVertexFormat.Short4,			// VertexElementFormat.Short4
		// 	// 	MTLVertexFormat.Short2Normalized,	// VertexElementFormat.NormalizedShort2
		// 	// 	MTLVertexFormat.Short4Normalized,	// VertexElementFormat.NormalizedShort4
		// 	// 	MTLVertexFormat.Half2,			// VertexElementFormat.HalfVector2
		// 	// 	MTLVertexFormat.Half4			// VertexElementFormat.HalfVector4
		// 	// };

		// 	// public static readonly MTLIndexType[] IndexType = new MTLIndexType[]
		// 	// {
		// 	// 	MTLIndexType.UInt16,	// IndexElementSize.SixteenBits
		// 	// 	MTLIndexType.UInt32	// IndexElementSize.ThirtyTwoBits
		// 	// };

		// 	public static readonly int[] IndexSize = new int[]
		// 	{
		// 		2,	// IndexElementSize.SixteenBits
		// 		4	// IndexElementSize.ThirtyTwoBits
		// 	};

		// 	// public static readonly MTLBlendFactor[] BlendMode = new MTLBlendFactor[]
		// 	// {
		// 	// 	MTLBlendFactor.One,			// Blend.One
		// 	// 	MTLBlendFactor.Zero,			// Blend.Zero
		// 	// 	MTLBlendFactor.SourceColor,		// Blend.SourceColor
		// 	// 	MTLBlendFactor.OneMinusSourceColor,	// Blend.InverseSourceColor
		// 	// 	MTLBlendFactor.SourceAlpha,		// Blend.SourceAlpha
		// 	// 	MTLBlendFactor.OneMinusSourceAlpha,	// Blend.InverseSourceAlpha
		// 	// 	MTLBlendFactor.DestinationColor,	// Blend.DestinationColor
		// 	// 	MTLBlendFactor.OneMinusDestinationColor,// Blend.InverseDestinationColor
		// 	// 	MTLBlendFactor.DestinationAlpha,	// Blend.DestinationAlpha
		// 	// 	MTLBlendFactor.OneMinusDestinationAlpha,// Blend.InverseDestinationAlpha
		// 	// 	MTLBlendFactor.BlendColor,		// Blend.BlendFactor
		// 	// 	MTLBlendFactor.OneMinusBlendColor,	// Blend.InverseBlendFactor
		// 	// 	MTLBlendFactor.SourceAlphaSaturated	// Blend.SourceAlphaSaturation
		// 	// };

		// 	// public static readonly MTLBlendOperation[] BlendOperation = new MTLBlendOperation[]
		// 	// {
		// 	// 	MTLBlendOperation.Add,			// BlendFunction.Add
		// 	// 	MTLBlendOperation.Subtract,		// BlendFunction.Subtract
		// 	// 	MTLBlendOperation.ReverseSubtract,	// BlendFunction.ReverseSubtract
		// 	// 	MTLBlendOperation.Max,			// BlendFunction.Max
		// 	// 	MTLBlendOperation.Min			// BlendFunction.Min
		// 	// };

		// 	// public static int ColorWriteMask(ColorWriteChannels channels)
		// 	// {
		// 	// 	if (channels == ColorWriteChannels.None)
		// 	// 	{
		// 	// 		return 0x0;
		// 	// 	}
		// 	// 	if (channels == ColorWriteChannels.All)
		// 	// 	{
		// 	// 		return 0xf;
		// 	// 	}

		// 	// 	int ret = 0;
		// 	// 	if ((channels & ColorWriteChannels.Red) != 0)
		// 	// 	{
		// 	// 		ret |= (0x1 << 3);
		// 	// 	}
		// 	// 	if ((channels & ColorWriteChannels.Green) != 0)
		// 	// 	{
		// 	// 		ret |= (0x1 << 2);
		// 	// 	}
		// 	// 	if ((channels & ColorWriteChannels.Blue) != 0)
		// 	// 	{
		// 	// 		ret |= (0x1 << 1);
		// 	// 	}
		// 	// 	if ((channels & ColorWriteChannels.Alpha) != 0)
		// 	// 	{
		// 	// 		ret |= (0x1 << 0);
		// 	// 	}
		// 	// 	return ret;
		// 	// }

		// 	// public static readonly MTLCompareFunction[] CompareFunc = new MTLCompareFunction[]
		// 	// {
		// 	// 	MTLCompareFunction.Always,	// CompareFunction.Always
		// 	// 	MTLCompareFunction.Never,	// CompareFunction.Never
		// 	// 	MTLCompareFunction.Less,	// CompareFunction.Less
		// 	// 	MTLCompareFunction.LessEqual,	// CompareFunction.LessEqual
		// 	// 	MTLCompareFunction.Equal,	// CompareFunction.Equal
		// 	// 	MTLCompareFunction.GreaterEqual,// CompareFunction.GreaterEqual
		// 	// 	MTLCompareFunction.Greater,	// CompareFunction.Greater
		// 	// 	MTLCompareFunction.NotEqual	// CompareFunction.NotEqual
		// 	// };

		// 	// public static readonly MTLStencilOperation[] StencilOp = new MTLStencilOperation[]
		// 	// {
		// 	// 	MTLStencilOperation.Keep,		// StencilOperation.Keep
		// 	// 	MTLStencilOperation.Zero,		// StencilOperation.Zero
		// 	// 	MTLStencilOperation.Replace,		// StencilOperation.Replace
		// 	// 	MTLStencilOperation.IncrementWrap,	// StencilOperation.Increment
		// 	// 	MTLStencilOperation.DecrementWrap,	// StencilOperation.Decrement
		// 	// 	MTLStencilOperation.IncrementClamp,	// StencilOperation.IncrementSaturation
		// 	// 	MTLStencilOperation.DecrementClamp,	// StencilOperation.DecrementSaturation
		// 	// 	MTLStencilOperation.Invert		// StencilOperation.Invert
		// 	// };

		// 	// public static readonly MTLTriangleFillMode[] FillMode = new MTLTriangleFillMode[]
		// 	// {
		// 	// 	MTLTriangleFillMode.Fill,	// FillMode.Solid
		// 	// 	MTLTriangleFillMode.Lines	// FillMode.WireFrame
		// 	// };

		// 	// public static float DepthBiasScale(MTLPixelFormat format)
		// 	// {
		// 	// 	switch (format)
		// 	// 	{
		// 	// 		case MTLPixelFormat.Depth16Unorm:
		// 	// 			return (float) ((1 << 16) - 1);

		// 	// 		case MTLPixelFormat.Depth24Unorm_Stencil8:
		// 	// 			return (float) ((1 << 24) - 1);

		// 	// 		case MTLPixelFormat.Depth32Float:
		// 	// 		case MTLPixelFormat.Depth32Float_Stencil8:
		// 	// 			return (float) ((1 << 23) - 1);
		// 	// 	}

		// 	// 	return 0.0f;
		// 	// }

		// 	// public static readonly MTLCullMode[] CullingEnabled = new MTLCullMode[]
		// 	// {
		// 	// 	MTLCullMode.None,	// CullMode.None
		// 	// 	MTLCullMode.Front,	// CullMode.CullClockwiseFace
		// 	// 	MTLCullMode.Back	// CullMode.CullCounterClockwiseFace
		// 	// };

		// 	// public static readonly MTLSamplerAddressMode[] Wrap = new MTLSamplerAddressMode[]
		// 	// {
		// 	// 	MTLSamplerAddressMode.Repeat,		// TextureAddressMode.Wrap
		// 	// 	MTLSamplerAddressMode.ClampToEdge,	// TextureAddressMode.Clamp
		// 	// 	MTLSamplerAddressMode.MirrorRepeat	// TextureAddressMode.Mirror
		// 	// };

		// 	// public static readonly MTLSamplerMinMagFilter[] MagFilter = new MTLSamplerMinMagFilter[]
		// 	// {
		// 	// 	MTLSamplerMinMagFilter.Linear,	// TextureFilter.Linear
		// 	// 	MTLSamplerMinMagFilter.Nearest,	// TextureFilter.Point
		// 	// 	MTLSamplerMinMagFilter.Linear,	// TextureFilter.Anisotropic
		// 	// 	MTLSamplerMinMagFilter.Linear,	// TextureFilter.LinearMipPoint
		// 	// 	MTLSamplerMinMagFilter.Nearest,	// TextureFilter.PointMipLinear
		// 	// 	MTLSamplerMinMagFilter.Nearest,	// TextureFilter.MinLinearMagPointMipLinear
		// 	// 	MTLSamplerMinMagFilter.Nearest,	// TextureFilter.MinLinearMagPointMipPoint
		// 	// 	MTLSamplerMinMagFilter.Linear,	// TextureFilter.MinPointMagLinearMipLinear
		// 	// 	MTLSamplerMinMagFilter.Linear	// TextureFilter.MinPointMagLinearMipPoint
		// 	// };

		// 	// public static readonly MTLSamplerMipFilter[] MipFilter = new MTLSamplerMipFilter[]
		// 	// {
		// 	// 	MTLSamplerMipFilter.Linear,	// TextureFilter.Linear
		// 	// 	MTLSamplerMipFilter.Nearest,	// TextureFilter.Point
		// 	// 	MTLSamplerMipFilter.Linear,	// TextureFilter.Anisotropic
		// 	// 	MTLSamplerMipFilter.Nearest,	// TextureFilter.LinearMipPoint
		// 	// 	MTLSamplerMipFilter.Linear,	// TextureFilter.PointMipLinear
		// 	// 	MTLSamplerMipFilter.Linear,	// TextureFilter.MinLinearMagPointMipLinear
		// 	// 	MTLSamplerMipFilter.Nearest,	// TextureFilter.MinLinearMagPointMipPoint
		// 	// 	MTLSamplerMipFilter.Linear,	// TextureFilter.MinPointMagLinearMipLinear
		// 	// 	MTLSamplerMipFilter.Nearest	// TextureFilter.MinPointMagLinearMipPoint
		// 	// };

		// 	// public static readonly MTLSamplerMinMagFilter[] MinFilter = new MTLSamplerMinMagFilter[]
		// 	// {
		// 	// 	MTLSamplerMinMagFilter.Linear,	// TextureFilter.Linear
		// 	// 	MTLSamplerMinMagFilter.Nearest,	// TextureFilter.Point
		// 	// 	MTLSamplerMinMagFilter.Linear,	// TextureFilter.Anisotropic
		// 	// 	MTLSamplerMinMagFilter.Linear,	// TextureFilter.LinearMipPoint
		// 	// 	MTLSamplerMinMagFilter.Nearest,	// TextureFilter.PointMipLinear
		// 	// 	MTLSamplerMinMagFilter.Linear,	// TextureFilter.MinLinearMagPointMipLinear
		// 	// 	MTLSamplerMinMagFilter.Linear,	// TextureFilter.MinLinearMagPointMipPoint
		// 	// 	MTLSamplerMinMagFilter.Nearest,	// TextureFilter.MinPointMagLinearMipLinear
		// 	// 	MTLSamplerMinMagFilter.Nearest	// TextureFilter.MinPointMagLinearMipPoint
		// 	// };

		// 	// public static readonly MTLPrimitiveType[] Primitive = new MTLPrimitiveType[]
		// 	// {
		// 	// 	MTLPrimitiveType.Triangle,	// PrimitiveType.TriangleList
		// 	// 	MTLPrimitiveType.TriangleStrip,	// PrimitiveType.TriangleStrip
		// 	// 	MTLPrimitiveType.Line,		// PrimitiveType.LineList
		// 	// 	MTLPrimitiveType.LineStrip,	// PrimitiveType.LineStrip
		// 	// 	MTLPrimitiveType.Point		// PrimitiveType.PointListEXT
		// 	// };

		// 	public static int PrimitiveVerts(PrimitiveType primitiveType, int primitiveCount)
		// 	{
		// 		switch (primitiveType)
		// 		{
		// 			case PrimitiveType.TriangleList:
		// 				return primitiveCount * 3;
		// 			case PrimitiveType.TriangleStrip:
		// 				return primitiveCount + 2;
		// 			case PrimitiveType.LineList:
		// 				return primitiveCount * 2;
		// 			case PrimitiveType.LineStrip:
		// 				return primitiveCount + 1;
		// 			case PrimitiveType.PointListEXT:
		// 				return primitiveCount;
		// 		}
		// 		throw new NotSupportedException();
		// 	}
		// }

		// #endregion
	}
}