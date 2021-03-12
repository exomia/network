#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Linq;
using Exomia.Network.Encoding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Exomia.Network.Tests.Encoding
{
    [TestClass]
    public unsafe class PayloadEncodingTests
    {
        [TestMethod]
        [DataRow(0, 0)]
        [DataRow(1, 2)]
        [DataRow(2, 3)]
        [DataRow(3, 4)]
        [DataRow(4, 5)]
        [DataRow(5, 6)]
        [DataRow(6, 7)]
        [DataRow(7, 8)]
        [DataRow(8, 10)]
        [DataRow(9, 11)]
        [DataRow(100, 115)]
        [DataRow(1024, 1171)]
        public void EncodedPayloadLength_ShouldReturnExpectedValue(int length, int expected)
        {
            Assert.AreEqual(expected, PayloadEncoding.EncodedPayloadLength(length));
        }

        [TestMethod]
        [DataRow(0, 0)]
        [DataRow(2, 1)]
        [DataRow(3, 2)]
        [DataRow(4, 3)]
        [DataRow(5, 4)]
        [DataRow(6, 5)]
        [DataRow(7, 6)]
        [DataRow(8, 7)]
        [DataRow(10, 8)]
        [DataRow(11, 9)]
        [DataRow(115, 100)]
        [DataRow(1171, 1024)]
        public void DecodedPayloadLength_ShouldReturnExpectedValue(int length, int expected)
        {
            Assert.AreEqual(expected, PayloadEncoding.DecodedPayloadLength(length));
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(16)]
        [DataRow(128)]
        [DataRow(166)]
        [DataRow(1024)]
        [DataRow(4096)]
        [DataRow(48574)]
        [DataRow(Constants.TCP_PAYLOAD_SIZE_MAX)]
        [DataRow(ushort.MaxValue)]
        public void Encode_WithRandomData_ShouldNotFail(int length)
        {
            Random r       = new Random((int)DateTime.Now.Ticks);
            byte[] buffer  = new byte[length];
            byte[] buffer2 = new byte[PayloadEncoding.EncodedPayloadLength(length)];
            r.NextBytes(buffer);
            fixed (byte* src = buffer)
            fixed (byte* dst = buffer2)
            {
                PayloadEncoding.Encode(src, length, dst, out int bufferLength);
                Assert.AreEqual(buffer2.Length, bufferLength);
                Assert.IsTrue(buffer2.All(b => b != 0));
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(16)]
        [DataRow(128)]
        [DataRow(166)]
        [DataRow(1024)]
        [DataRow(4096)]
        [DataRow(48574)]
        [DataRow(Constants.TCP_PAYLOAD_SIZE_MAX)]
        [DataRow(ushort.MaxValue)]
        public void Decode_WithEncodedRandomData_ShouldNotFail(int length)
        {
            Random r       = new Random((int)DateTime.Now.Ticks);
            byte[] buffer  = new byte[length];
            byte[] buffer2 = new byte[PayloadEncoding.EncodedPayloadLength(length)];

            r.NextBytes(buffer);
            fixed (byte* src = buffer)
            fixed (byte* dst = buffer2)
            {
                ushort checksum1 = PayloadEncoding.Encode(src, length, dst, out int bufferLength);

                byte[] buffer3 = new byte[bufferLength];
                fixed (byte* dcp = buffer3)
                {
                    ushort checksum2 = PayloadEncoding.Decode(dst, bufferLength, dcp, out int dstLength);
                    Assert.AreEqual(length, dstLength);
                    Assert.AreEqual(checksum1, checksum2);
                    Assert.IsTrue(buffer3.Take(dstLength).SequenceEqual(buffer));
                }
            }
        }
    }
}