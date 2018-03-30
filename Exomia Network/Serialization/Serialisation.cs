using System;
using System.Runtime.CompilerServices;
using Exomia.Network.Buffers;

namespace Exomia.Network.Serialization
{
    internal static class Serialization
    {
        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte[] Serialize(uint commandid, uint type, byte[] data, int lenght)
        {
            // uint = 32bit
            // 
            // | COMMANDID 0-9 (10)bit | TYPE 10-17 (8)bit       | DATALENGTH 18-31 (14)bit                  |
            // | 0 1 2 3 4 5 6 7 8 9   | 10 11 12 13 14 15 16 17 | 18 19 20 21 22 23 24 25 26 27 28 29 30 31 |
            // | VR: 0-1023            | VR: 0-255               | VR: 0-16383                               | VR = VALUE RANGE
            // 
            // | 0 0 0 0 0 0 0 0 0 0   |  0  0  0  0  0  0  0  0 |  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0x3FFF
            // | 0 0 0 0 0 0 0 0 0 0   |  1  1  1  1  1  1  1  1 |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | TYPE_MASK 0x3FC000
            // | 1 1 1 1 1 1 1 1 1 1   |  0  0  0  0  0  0  0  0 |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFC00000

            byte[] header = BitConverter.GetBytes((commandid << 22) | (type << 14) | (ushort)lenght);
            byte[] buffer = ByteArrayPool.Rent(header.Length + lenght);

            //HEADER
            Buffer.BlockCopy(header, 0, buffer, 0, header.Length);

            //DATA
            Buffer.BlockCopy(data, 0, buffer, header.Length, lenght);

            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void GetHeader(this byte[] header, out uint commandID, out uint type, out int dataLenght)
        {
            // uint = 32bit
            // 
            // | COMMANDID 0-9 (10)bit | TYPE 10-17 (8)bit       | DATALENGTH 18-31 (14)bit                  |
            // | 0 1 2 3 4 5 6 7 8 9   | 10 11 12 13 14 15 16 17 | 18 19 20 21 22 23 24 25 26 27 28 29 30 31 |
            // | VR: 0-1023            | VR: 0-255               | VR: 0-16383                               | VR = VALUE RANGE
            // 
            // | 0 0 0 0 0 0 0 0 0 0   |  0  0  0  0  0  0  0  0 |  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0x3FFF
            // | 0 0 0 0 0 0 0 0 0 0   |  1  1  1  1  1  1  1  1 |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | TYPE_MASK 0x3FC000
            // | 1 1 1 1 1 1 1 1 1 1   |  0  0  0  0  0  0  0  0 |  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFC00000

            uint h = BitConverter.ToUInt32(header, 0);

            commandID = (Constants.COMMANDID_MASK & h) >> 22;
            type = (Constants.TYPE_MASK & h) >> 14;
            dataLenght = (int)(Constants.DATA_LENGTH_MASK & h);
        }

        #endregion
    }
}