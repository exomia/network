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

namespace Exomia.Network
{
    /// <summary>
    ///     Constants
    /// </summary>
    public static class Constants
    {
        #region Variables

        internal const int HEADER_SIZE = 8;
        internal const int PACKET_SIZE_MAX = 16000;

        internal const double UDP_IDLE_TIME = 20000.0;

        internal const uint COMMANDID_MASK = 0xFFC00000;
        internal const uint TYPE_MASK = 0x3FC000;
        internal const uint DATA_LENGTH_MASK = 0x3FFF;

        internal const uint CLIENTINFO_STRUCT_TYPE_ID = 255;
        internal const uint UDP_CONNECT_STRUCT_TYPE_ID = 255;
        internal const uint PING_STRUCT_TYPE_ID = 255;

        /// <summary>
        ///     CLIENTINFO_COMMAND_ID
        /// </summary>
        public const uint CLIENTINFO_COMMAND_ID = 1023;

        /// <summary>
        ///     UDP_CONNEC_COMMAND_ID
        /// </summary>
        public const uint UDP_CONNECT_COMMAND_ID = 1022;

        /// <summary>
        ///     PING_COMMAND_ID
        /// </summary>
        public const uint PING_COMMAND_ID = 1021;

        #endregion
    }
}