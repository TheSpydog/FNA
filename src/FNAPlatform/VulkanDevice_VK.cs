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
using SDL2;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Microsoft.Xna.Framework.Graphics
{
	internal partial class VulkanDevice : IGLDevice
	{
		#region Private Vulkan Entry Points

		private Delegate GetProcAddress(IntPtr instance, string name, Type type)
		{
			IntPtr addr = vkGetInstanceProcAddr(instance, name);
			if (addr == IntPtr.Zero)
			{
				throw new Exception(name);
			}
			return Marshal.GetDelegateForFunctionPointer(addr, type);
		}

		public void LoadGlobalEntryPoints()
		{
			// First load the function loader
			vkGetInstanceProcAddr = (GetInstanceProcAddr) Marshal.GetDelegateForFunctionPointer(
				SDL.SDL_Vulkan_GetVkGetInstanceProcAddr(),
				typeof(GetInstanceProcAddr)
			);

			// Now load global entry points
			vkCreateInstance = (CreateInstance) GetProcAddress(
				IntPtr.Zero,
				"vkCreateInstance",
				typeof(CreateInstance)
			);
		}

		public void LoadInstanceEntryPoints(IntPtr instance)
		{

		}

		private delegate IntPtr GetInstanceProcAddr(IntPtr instance, string name);
		private GetInstanceProcAddr vkGetInstanceProcAddr;

		private delegate IntPtr CreateInstance(IntPtr pCreateInfo, IntPtr pAllocator, IntPtr pInstance);
		private CreateInstance vkCreateInstance;

		#endregion
	}
}
