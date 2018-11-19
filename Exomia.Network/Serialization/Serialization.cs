#region MIT License

// Copyright (c) 2018 exomia - Daniel Bätz
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
    static partial class Serialization
    {
        internal const uint UNUSED_BIT_MASK = 0b10000000;

        internal const uint RESPONSE_BIT_MASK = 0b01000000;

        internal const uint COMPRESSED_BIT_MASK = 0b00100000;

        internal const uint ENCRYPT_BIT_MASK = 0b00010000;
        internal const uint ENCRYPT_MODE_MASK = 0b00001111;

        private const byte UNUSED_1_BIT = 1 << 7;
        private const byte RESPONSE_1_BIT = 1 << 6;
        private const byte COMPRESSED_1_BIT = 1 << 5;

        internal const int COMMAND_ID_SHIFT = 16;
        internal const int DATA_LENGTH_MASK = 0xFFFF;

        private const int LENGTH_THRESHOLD = 1 << 12; //4096
    }
}