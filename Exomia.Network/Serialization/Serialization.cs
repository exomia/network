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

namespace Exomia.Network.Serialization
{
    /// <summary>
    ///     A serialization.
    /// </summary>
    static partial class Serialization
    {
        /// <summary>
        ///     The response bit mask.
        /// </summary>
        internal const uint RESPONSE_BIT_MASK = 0b01000000;

        /// <summary>
        ///     The response bit.
        /// </summary>
        private const byte RESPONSE_1_BIT = 1 << 6;

        /// <summary>
        ///     The compressed mode mask.
        /// </summary>
        internal const byte COMPRESSED_MODE_MASK = 0b00111000;

        /// <summary>
        ///     The command identifier shift.
        /// </summary>
        internal const int COMMAND_ID_SHIFT = 16;

        /// <summary>
        ///     The data length mask.
        /// </summary>
        internal const int DATA_LENGTH_MASK = 0xFFFF;

        /// <summary>
        ///     LENGTH_THRESHOLD 4096.
        /// </summary>
        private const int LENGTH_THRESHOLD = 1 << 12;
    }
}