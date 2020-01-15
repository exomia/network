#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System.Runtime.CompilerServices;

namespace Exomia.Network.Lib
{
    /// <summary>
    ///     The mathematics 2.
    /// </summary>
    static class Math2
    {
        /// <summary>
        ///     The offset maximum.
        /// </summary>
        private const long L_OFFSET_MAX = int.MaxValue + 1L;

        /// <summary>
        ///     Returns the smallest integer greater than or equal to the specified floating-point number.
        /// </summary>
        /// <param name="f"> A floating-point number with single precision. </param>
        /// <returns>
        ///     The smallest integer, which is greater than or equal to f.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Ceiling(double f)
        {
            return (int)(L_OFFSET_MAX - (long)(L_OFFSET_MAX - f));
        }

        /// <summary>
        ///     R1.
        /// </summary>
        /// <param name="a"> The uint to process. </param>
        /// <param name="b"> The int to process. </param>
        /// <returns>
        ///     An uint.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint R1(uint a, int b)
        {
            return (a << b) | (a >> (32 - b));
        }
    }
}