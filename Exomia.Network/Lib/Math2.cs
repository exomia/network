#region MIT License

// Copyright (c) 2019 exomia - Daniel Bätz
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

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