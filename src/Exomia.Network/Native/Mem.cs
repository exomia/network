#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Runtime.InteropServices;
using System.Security;

namespace Exomia.Network.Native
{
    static unsafe class Mem
    {
#if WINDOWS
        private const string DLL_NAME = "msvcrt.dll";
#elif LINUX
        private const string DLL_NAME = "libc";
#endif
        /// <summary>
        ///     memcpy call.
        ///     Copies the values of num bytes from the location pointed to by source
        ///     directly to the memory block pointed to by destination.
        /// </summary>
        /// <param name="dest">  [in,out] destination ptr. </param>
        /// <param name="src">   [in,out] source ptr. </param>
        /// <param name="count"> count of bytes to copy. </param>
        [SuppressUnmanagedCodeSecurity]
        [DllImport(
            DLL_NAME, EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern void Cpy(void* dest,
                                      void* src,
                                      int   count);

        /// <summary>
        ///     memset call.
        ///     Sets the first num bytes of the block of memory pointed by ptr to the
        ///     specified value (interpreted as an unsigned char).
        /// </summary>
        /// <param name="dest">  [in,out] destination addr. </param>
        /// <param name="value"> value to be set. </param>
        /// <param name="count"> count of bytes. </param>
        /// <returns>
        ///     Null if it fails, else a void*.
        /// </returns>
        [SuppressUnmanagedCodeSecurity]
        [DllImport(
            DLL_NAME, EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern void* Set(void* dest,
                                       int   value,
                                       int   count);
    }
}