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

namespace Exomia.Network
{
    /// <summary>
    ///     DeserializePacket callback.
    /// </summary>
    /// <typeparam name="TResult"> Type of the result. </typeparam>
    /// <param name="packet"> The packet. </param>
    /// <returns>
    ///     A TResult.
    /// </returns>
    public delegate TResult DeserializePacketHandler<out TResult>(in Packet packet);

    /// <summary>
    ///     Packet readonly struct.
    /// </summary>
    public readonly struct Packet
    {
        /// <summary>
        ///     Buffer.
        /// </summary>
        public readonly byte[] Buffer;

        /// <summary>
        ///     Offset.
        /// </summary>
        public readonly int Offset;

        /// <summary>
        ///     Length.
        /// </summary>
        public readonly int Length;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Packet" /> struct.
        /// </summary>
        /// <param name="buffer"> . </param>
        /// <param name="offset"> . </param>
        /// <param name="length"> . </param>
        public Packet(byte[] buffer, int offset, int length)
        {
            Buffer = buffer;
            Offset = offset;
            Length = length;
        }
    }
}