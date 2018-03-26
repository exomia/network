using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exomia.Network.Extensions.Struct
{
    /// <summary>
    ///     StructExt class
    /// </summary>
    public static class StructExt
    {
        #region ToBytes

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <returns>byte array</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToBytes<T>(this T data) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(data, ptr, true);

                byte[] arr = new byte[size];
                Marshal.Copy(ptr, arr, 0, size);
                return arr;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="arr">out byte array</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToBytes<T>(this T data, out byte[] arr) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(data, ptr, true);
                arr = new byte[size];
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <returns>byte array</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe byte[] ToBytesUnsafe<T>(this T data) where T : struct
        {
            byte[] arr = new byte[Marshal.SizeOf(typeof(T))];
            fixed (byte* ptr = arr)
            {
                Marshal.StructureToPtr(data, new IntPtr(ptr), true);
            }
            return arr;
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="arr">out byte array</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ToBytesUnsafe<T>(this T data, out byte[] arr) where T : struct
        {
            arr = new byte[Marshal.SizeOf(typeof(T))];
            fixed (byte* ptr = arr)
            {
                Marshal.StructureToPtr(data, new IntPtr(ptr), true);
            }
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <returns>byte array</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToBytes2<T>(this T data) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] arr = new byte[size];
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
                return arr;
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        ///     converts a struct into a byte array
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="data">data</param>
        /// <param name="arr">out byte array</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToBytes2<T>(this T data, out byte[] arr) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            arr = new byte[size];
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }
        }

        #endregion

        #region FromBytes

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="obj">out struct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes<T>(this byte[] arr, out T obj) where T : struct
        {
            //int len = Marshal.SizeOf(typeof(T));
            int len = arr.Length;

            IntPtr ptr = Marshal.AllocHGlobal(len);
            try
            {
                Marshal.Copy(arr, 0, ptr, len);
                obj = Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <returns>struct</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes<T>(this byte[] arr) where T : struct
        {
            //int len = Marshal.SizeOf(typeof(T));
            int len = arr.Length;

            IntPtr ptr = Marshal.AllocHGlobal(len);
            try
            {
                Marshal.Copy(arr, 0, ptr, len);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="obj">out struct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void FromBytesUnsafe<T>(this byte[] arr, out T obj) where T : struct
        {
            fixed (byte* ptr = arr)
            {
                obj = Marshal.PtrToStructure<T>(new IntPtr(ptr));
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <returns>struct</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T FromBytesUnsafe<T>(this byte[] arr) where T : struct
        {
            fixed (byte* ptr = arr)
            {
                return Marshal.PtrToStructure<T>(new IntPtr(ptr));
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="obj">out struct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromBytes2<T>(this byte[] arr, out T obj) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                obj = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        ///     converts a byte array into a struct
        /// </summary>
        /// <typeparam name="T">struct type</typeparam>
        /// <param name="arr">byte array</param>
        /// <returns>struct</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FromBytes2<T>(this byte[] arr) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        #endregion

        /*
         * NO GENERIC SOLUTION
         * 
         *  FromBytes(byte[] data)
         *  unsafe
         *  {
         *      fixed (byte* ptr = data)
         *      {
         *          STRUCT str = *(STRUCT*)ptr;
         *      }
         *  }
         *  
         *  
         *  ToBytes(STRUCT data)
         *  int len = Marshal.SizeOf(typeof(STRUCT));
         *  byte[] arr = new byte[len];
         *  unsafe
         *  {   
         *      //Marshal.Copy(new IntPtr(&data), arr, 0, len);
         *      fixed (byte* ptr = arr)
         *      {
         *          Memcpy(ptr, &ts, len);
         *      }
         *  }
         */
    }
}