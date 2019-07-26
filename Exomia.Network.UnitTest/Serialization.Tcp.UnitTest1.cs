#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using Exomia.Network.Lib;
using Exomia.Network.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Exomia.Network.UnitTest
{
    [TestClass]
    public class SerializationTcpUnitTest1
    {
        [TestMethod]
        public unsafe void SerializeTcpNoResponseTest()
        {
            byte[] data = { 0b1000_0000, 0b1000_0000, 0b0000_0000, 0b1000_0000, 0b1000_0000, 0b0000_0000, 0b1000_0000 };
            fixed (byte* src = data)
            {
                Serialization.Serialization.SerializeTcp(
                    1337u, src, data.Length, 0, EncryptionMode.None, out byte[] send, out int size);
                Assert.AreEqual(size, Constants.TCP_HEADER_SIZE + data.Length + 1 + 1);

                CircularBuffer cb = new CircularBuffer();
                cb.Write(send, 0, size);

                Assert.IsTrue(
                    cb.PeekHeader(
                        0, out byte packetHeader, out uint commandID, out int dataLength, out ushort checksum));

                Assert.AreEqual((byte)0, packetHeader);
                Assert.AreEqual(1337u, commandID);
                Assert.AreEqual(data.Length + 1 + 1, dataLength);

                Assert.IsTrue(cb.PeekByte((Constants.TCP_HEADER_SIZE + dataLength) - 1, out byte b));
                Assert.AreEqual((byte)0, b);

                cb.Read(send, 0, dataLength, Constants.TCP_HEADER_SIZE);

                byte[] deserializeBuffer = new byte[128];
                /*int checksum2 = Serialization.Serialization.Deserialize(
                    send, 0, dataLength - 1, deserializeBuffer, out int bufferLength);
    
                Assert.AreEqual(checksum, checksum2);
    
                Assert.AreEqual(data.Length, bufferLength);
                Assert.IsTrue(data.SequenceEqual(deserializeBuffer.Take(bufferLength)));*/
            }
        }

        [TestMethod]
        public unsafe void SerializeTcpResponseTest()
        {
            byte[] data = { 0b0000_0000, 0b0000_0000, 0b0000_0000, 0b0000_0000, 0b0000_0000, 0b0000_0000, 0b0000_0000 };
            fixed (byte* src = data)
            {
                Serialization.Serialization.SerializeTcp(
                    1337u, src, data.Length, 654584478, EncryptionMode.None, out byte[] send,
                    out int size);
                Assert.AreEqual(size, Constants.TCP_HEADER_SIZE + 4 + data.Length + 1 + 1);

                CircularBuffer cb = new CircularBuffer();
                cb.Write(send, 0, size);

                Assert.IsTrue(
                    cb.PeekHeader(
                        0, out byte packetHeader, out uint commandID, out int dataLength, out ushort checksum));

                Assert.AreEqual((byte)64, packetHeader);
                Assert.AreEqual(1337u, commandID);
                Assert.AreEqual(4 + data.Length + 1 + 1, dataLength);

                Assert.IsTrue(cb.PeekByte((Constants.TCP_HEADER_SIZE + dataLength) - 1, out byte b));
                Assert.AreEqual((byte)0, b);

                cb.Read(send, 0, dataLength, Constants.TCP_HEADER_SIZE);

                byte[] deserializeBuffer = new byte[128];
                /*int checksum2 = Serialization.Serialization.Deserialize(
                    send, 4, dataLength - 5, deserializeBuffer, out int bufferLength);
    
                Assert.AreEqual(checksum, checksum2);
    
                Assert.AreEqual(data.Length, bufferLength);
                Assert.IsTrue(data.SequenceEqual(deserializeBuffer.Take(bufferLength)));*/
            }
        }

        [TestMethod]
        public unsafe void SerializeTcpNoResponseBigPayloadTest()
        {
            Random rnd  = new Random(7576);
            byte[] data = new byte[(1 << 12) + 200];

            //rnd.NextBytes(data);
            int expectedLength = data.Length + Math2.Ceiling(data.Length / 7.0f);

            fixed (byte* src = data)
            {
                Serialization.Serialization.SerializeTcp(
                    1337u, src, data.Length, 0, EncryptionMode.None, out byte[] send, out int size);
                Assert.IsTrue(size < Constants.TCP_HEADER_SIZE + 4 + expectedLength + 1 + 1);

                CircularBuffer cb = new CircularBuffer(data.Length);
                cb.Write(send, 0, size);

                Assert.IsTrue(
                    cb.PeekHeader(
                        0, out byte packetHeader, out uint commandID, out int dataLength, out ushort checksum));

                Assert.AreEqual((byte)32, packetHeader);
                Assert.AreEqual(1337u, commandID);
                Assert.IsTrue(4 + data.Length + 1 + 1 > dataLength);

                Assert.IsTrue(cb.PeekByte((Constants.TCP_HEADER_SIZE + dataLength) - 1, out byte b));
                Assert.AreEqual((byte)0, b);

                cb.Read(send, 0, dataLength, Constants.TCP_HEADER_SIZE);

                byte[] deserializeBuffer = new byte[128];
                /*int checksum2 = Serialization.Serialization.Deserialize(
                    send, 4, dataLength - 5, deserializeBuffer, out int bufferLength);
    
                Assert.AreEqual(27, bufferLength);
    
                Assert.AreEqual(checksum, checksum2);
                Assert.AreEqual(data.Length, BitConverter.ToInt32(send, 0));*/
            }
        }
    }
}