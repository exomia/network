#region License

// Copyright (c) 2018-2020, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

using System;
using System.Linq;
using Exomia.Network.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Exomia.Network.UnitTest
{
    [TestClass]
    public unsafe class CircularBufferUnitTest1
    {
        [TestMethod]
        [DataRow(1024)]
        [DataRow(4096)]
        [DataRow(8192)]
        public void InitTest_CircularBuffer_Initialize_With_PowerOfTwo_ShouldPass(int count)
        {
            CircularBuffer t1 = new CircularBuffer(count);
            Assert.IsNotNull(t1);
            Assert.AreEqual(t1.Count, 0);
            Assert.AreEqual(t1.Capacity, count);
            Assert.IsTrue(t1.IsEmpty);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        [DataRow(int.MinValue)]
        [DataRow(0x7FFFFFFF)]
        public void InitTest_CircularBuffer_Initialize_With_InvalidNumbers_ShouldFail(int count)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () =>
                {
                    CircularBuffer t1 = new CircularBuffer(count);
                });
        }

        [TestMethod]
        public void CircularBuffer_Initialize_With_898_Capacity_ShouldBe_1024()
        {
            CircularBuffer t1 = new CircularBuffer(898);
            Assert.IsNotNull(t1);
            Assert.AreEqual(t1.Count, 0);
            Assert.AreEqual(t1.Capacity, 1024);
            Assert.IsTrue(t1.IsEmpty);
        }

        [TestMethod]
        public void CircularBuffer_Initialize_With_3000_Capacity_ShouldBe_4096()
        {
            CircularBuffer t1 = new CircularBuffer(3000);
            Assert.IsNotNull(t1);
            Assert.AreEqual(t1.Count, 0);
            Assert.AreEqual(t1.Capacity, 4096);
            Assert.IsTrue(t1.IsEmpty);
        }

        [TestMethod]
        public void SafeWriteTest()
        {
            CircularBuffer cb     = new CircularBuffer();
            byte[]         buffer = { 45, 48, 72, 15 };
            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 4);

            cb.Write(buffer, 2, 2);

            Assert.AreEqual(cb.Count, 6);

            cb.Write(buffer, 1, 2);

            Assert.AreEqual(cb.Count, 8);
        }

        [TestMethod]
        public void UnsafeWriteTest()
        {
            CircularBuffer cb     = new CircularBuffer();
            byte[]         buffer = { 45, 48, 72, 15 };
            fixed (byte* src = buffer)
            {
                cb.Write(src, 4);

                Assert.AreEqual(cb.Count, 4);

                cb.Write(src + 2, 2);

                Assert.AreEqual(cb.Count, 6);

                cb.Write(src + 1, 2);

                Assert.AreEqual(cb.Count, 8);
            }
        }

        [TestMethod]
        public void SafeReadTest()
        {
            CircularBuffer cb = new CircularBuffer();

            byte[] buffer = { 45, 48, 72, 15 };
            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 4);

            byte[] readBuffer = new byte[4];
            cb.Read(readBuffer, 0, readBuffer.Length, 0);

            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            Assert.IsTrue(readBuffer.SequenceEqual(buffer));

            Assert.AreEqual(0, cb.Read(readBuffer, 0, readBuffer.Length, 0));

            byte[] buffer2 = { 45, 48, 72, 1, 4, 87, 95 };
            cb.Write(buffer2, 0, buffer2.Length);

            byte[] readBuffer2 = new byte[buffer2.Length];
            cb.Read(readBuffer2, 0, buffer2.Length - 2, 2);

            Assert.IsTrue(readBuffer2.Take(buffer2.Length - 2).SequenceEqual(buffer2.Skip(2)));

            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);
        }

        [TestMethod]
        public void UnsafeReadTest()
        {
            CircularBuffer cb = new CircularBuffer();

            byte[] buffer = { 45, 48, 72, 15 };
            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 4);

            byte[] readBuffer = new byte[4];
            fixed (byte* src = readBuffer)
            {
                cb.Read(src, readBuffer.Length, 0);
            }

            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            Assert.IsTrue(readBuffer.SequenceEqual(buffer));
            fixed (byte* src = readBuffer)
            {
                Assert.AreEqual(0, cb.Read(src, readBuffer.Length, 0));
            }

            byte[] buffer2 = { 45, 48, 72, 1, 4, 87, 95 };
            cb.Write(buffer2, 0, buffer2.Length);

            byte[] readBuffer2 = new byte[buffer2.Length];
            fixed (byte* src = readBuffer2)
            {
                cb.Read(src, buffer2.Length - 2, 2);
            }
            Assert.IsTrue(readBuffer2.Take(buffer2.Length - 2).SequenceEqual(buffer2.Skip(2)));

            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);
        }

        [TestMethod]
        public void SafeWriteTest_With_Overflow()
        {
            Random rnd = new Random(1337);

            CircularBuffer cb = new CircularBuffer(128);

            byte[] buffer = new byte[77];
            rnd.NextBytes(buffer);

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 77);

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 128);

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 128);
        }

        [TestMethod]
        public void UnsafeWriteTest_With_Overflow()
        {
            Random rnd = new Random(1337);

            CircularBuffer cb = new CircularBuffer(128);

            byte[] buffer = new byte[77];
            rnd.NextBytes(buffer);

            fixed (byte* src = buffer)
            {
                cb.Write(src, buffer.Length);

                Assert.AreEqual(cb.Count, 77);

                cb.Write(src, buffer.Length);

                Assert.AreEqual(cb.Count, 128);

                cb.Write(src, buffer.Length);

                Assert.AreEqual(cb.Count, 128);
            }
        }

        [TestMethod]
        public void SafeReadTest_With_Overflow()
        {
            Random rnd = new Random(1337);

            byte[] buffer = new byte[9];
            rnd.NextBytes(buffer);

            CircularBuffer cb    = new CircularBuffer(16);
            byte[]         dummy = new byte[100];
            Assert.AreEqual(0, cb.Read(dummy, 0, 78, 0));

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(buffer.Length, cb.Read(dummy, 0, 78, 0));

            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 16);

            byte[] readBuffer2 = new byte[9];
            Assert.AreEqual(readBuffer2.Length, cb.Read(readBuffer2, 0, readBuffer2.Length, 0));

            Assert.AreEqual(cb.Count, 16 - 9);
            Assert.IsFalse(cb.IsEmpty);

            Assert.IsTrue(readBuffer2.Take(9).SequenceEqual(buffer));

            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);

            byte[] readBuffer4 = new byte[9];
            Assert.AreEqual(9, cb.Read(readBuffer4, 0, readBuffer4.Length, 0));

            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(9, cb.Read(readBuffer4, 0, readBuffer4.Length, 0));
            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(9, cb.Read(readBuffer4, 0, readBuffer4.Length, 0));

            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(1, cb.Read(readBuffer4, 0, 1, 8));
            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            Assert.IsTrue(readBuffer4.Take(1).SequenceEqual(buffer.Skip(8).Take(1)));
        }

        [TestMethod]
        public void UnsafeReadTest_With_Overflow()
        {
            Random rnd = new Random(1337);

            byte[] buffer = new byte[9];
            rnd.NextBytes(buffer);

            CircularBuffer cb    = new CircularBuffer(16);
            byte[]         dummy = new byte[100];
            fixed (byte* src = dummy)
            {
                Assert.AreEqual(0, cb.Read(src, 78, 0));
            }
            cb.Write(buffer, 0, buffer.Length);

            fixed (byte* src = dummy)
            {
                Assert.AreEqual(buffer.Length, cb.Read(src, 78, 0));
            }

            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 16);

            byte[] readBuffer2 = new byte[9];
            fixed (byte* src = readBuffer2)
            {
                Assert.AreEqual(readBuffer2.Length, cb.Read(src, readBuffer2.Length, 0));
            }

            Assert.AreEqual(cb.Count, 16 - 9);
            Assert.IsFalse(cb.IsEmpty);

            Assert.IsTrue(readBuffer2.Take(9).SequenceEqual(buffer));

            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);

            byte[] readBuffer4 = new byte[9];
            fixed (byte* src = readBuffer4)
            {
                Assert.AreEqual(9, cb.Read(src, readBuffer4.Length, 0));
            }

            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            cb.Write(buffer, 0, buffer.Length);
            fixed (byte* src = readBuffer4)
            {
                Assert.AreEqual(9, cb.Read(src, readBuffer4.Length, 0));
            }
            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);
            fixed (byte* src = readBuffer4)
            {
                Assert.AreEqual(9, cb.Read(src, readBuffer4.Length, 0));
            }
            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            cb.Write(buffer, 0, buffer.Length);
            fixed (byte* src = readBuffer4)
            {
                Assert.AreEqual(1, cb.Read(src, 1, 8));
            }
            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            Assert.IsTrue(readBuffer4.Take(1).SequenceEqual(buffer.Skip(8).Take(1)));
        }

        [TestMethod]
        public void SafePeekTest()
        {
            CircularBuffer cb = new CircularBuffer();

            byte[] buffer = { 45, 48, 72, 15 };
            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 4);

            byte[] peekBuffer = new byte[4];
            cb.Peek(peekBuffer, 0, peekBuffer.Length, 0);

            Assert.AreEqual(cb.Count, 4);
            Assert.IsFalse(cb.IsEmpty);

            Assert.IsTrue(peekBuffer.SequenceEqual(buffer));

            Assert.AreEqual(4, cb.Peek(peekBuffer, 0, 8, 0));

            byte[] buffer2 = { 45, 48, 72, 1, 4, 87, 95 };
            cb.Write(buffer2, 0, buffer2.Length);

            byte[] peekBuffer2 = new byte[buffer2.Length];
            cb.Peek(peekBuffer2, 0, buffer2.Length - 2, 4 + 2);

            Assert.IsTrue(peekBuffer2.Take(buffer2.Length - 2).SequenceEqual(buffer2.Skip(2)));

            Assert.AreEqual(cb.Count, 11);
            Assert.IsFalse(cb.IsEmpty);
        }

        [TestMethod]
        public void UnsafePeekTest()
        {
            CircularBuffer cb = new CircularBuffer();

            byte[] buffer = { 45, 48, 72, 15 };
            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 4);

            byte[] peekBuffer = new byte[4];

            fixed (byte* dest = peekBuffer)
            {
                cb.Peek(dest, peekBuffer.Length, 0);
            }
            Assert.AreEqual(cb.Count, 4);
            Assert.IsFalse(cb.IsEmpty);

            Assert.IsTrue(peekBuffer.SequenceEqual(buffer));

            Assert.AreEqual(4, cb.Peek(peekBuffer, 0, 8, 0));

            byte[] buffer2 = { 45, 48, 72, 1, 4, 87, 95 };
            cb.Write(buffer2, 0, buffer2.Length);

            byte[] peekBuffer2 = new byte[buffer2.Length];

            fixed (byte* dest = peekBuffer2)
            {
                cb.Peek(dest, buffer2.Length - 2, 4 + 2);
            }
            Assert.IsTrue(peekBuffer2.Take(buffer2.Length - 2).SequenceEqual(buffer2.Skip(2)));

            Assert.AreEqual(cb.Count, 11);
            Assert.IsFalse(cb.IsEmpty);
        }

        [TestMethod]
        public void SafePeekTest_With_Overflow()
        {
            Random rnd = new Random(1337);

            byte[] buffer = new byte[9];
            rnd.NextBytes(buffer);

            CircularBuffer cb    = new CircularBuffer(16);
            byte[]         dummy = new byte[100];
            Assert.AreEqual(0, cb.Peek(dummy, 0, 78, 0));

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(buffer.Length, cb.Peek(dummy, 0, 78, 0));

            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 16);

            byte[] readBuffer2 = new byte[9];
            Assert.AreEqual(7, cb.Peek(readBuffer2, 0, readBuffer2.Length, 9));

            Assert.AreEqual(cb.Count, 16);
            Assert.IsFalse(cb.IsEmpty);

            Assert.IsTrue(readBuffer2.Take(7).SequenceEqual(buffer.Take(7)));

            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);

            byte[] readBuffer4 = new byte[9];
            Assert.AreEqual(9, cb.Read(readBuffer4, 0, readBuffer4.Length, 0));

            Assert.AreEqual(0, cb.Count);
            Assert.IsTrue(cb.IsEmpty);

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(9, cb.Peek(readBuffer4, 0, readBuffer4.Length, 0));
            Assert.AreEqual(cb.Count, 9);
            Assert.IsFalse(cb.IsEmpty);

            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(9, cb.Read(readBuffer4, 0, readBuffer4.Length, 0));

            Assert.AreEqual(0, cb.Count);
            Assert.IsTrue(cb.IsEmpty);

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(1, cb.Peek(readBuffer4, 0, 1, 8));
            Assert.AreEqual(9, cb.Count);
            Assert.IsFalse(cb.IsEmpty);

            Assert.IsTrue(readBuffer4.Take(1).SequenceEqual(buffer.Skip(8).Take(1)));
        }

        [TestMethod]
        public void UnsafePeekTest_With_Overflow()
        {
            Random rnd = new Random(1337);

            byte[] buffer = new byte[9];
            rnd.NextBytes(buffer);

            CircularBuffer cb    = new CircularBuffer(16);
            byte[]         dummy = new byte[100];

            fixed (byte* src = dummy)
            {
                Assert.AreEqual(0, cb.Peek(src, 78, 0));
            }

            cb.Write(buffer, 0, buffer.Length);
            fixed (byte* src = dummy)
            {
                Assert.AreEqual(buffer.Length, cb.Peek(src, 78, 0));
            }
            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);

            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 16);

            byte[] readBuffer2 = new byte[9];
            fixed (byte* src = readBuffer2)
            {
                Assert.AreEqual(7, cb.Peek(src, readBuffer2.Length, 9));
            }
            Assert.AreEqual(cb.Count, 16);
            Assert.IsFalse(cb.IsEmpty);

            Assert.IsTrue(readBuffer2.Take(7).SequenceEqual(buffer.Take(7)));

            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);

            byte[] readBuffer4 = new byte[9];
            fixed (byte* src = readBuffer4)
            {
                Assert.AreEqual(9, cb.Read(src, readBuffer4.Length, 0));
            }
            Assert.AreEqual(0, cb.Count);
            Assert.IsTrue(cb.IsEmpty);

            cb.Write(buffer, 0, buffer.Length);
            fixed (byte* src = readBuffer4)
            {
                Assert.AreEqual(9, cb.Peek(src, readBuffer4.Length, 0));
            }
            Assert.AreEqual(cb.Count, 9);
            Assert.IsFalse(cb.IsEmpty);

            cb.Dispose();

            cb = new CircularBuffer(16);
            cb.Write(buffer, 0, buffer.Length);
            fixed (byte* src = readBuffer4)
            {
                Assert.AreEqual(9, cb.Read(src, readBuffer4.Length, 0));
            }
            Assert.AreEqual(0, cb.Count);
            Assert.IsTrue(cb.IsEmpty);

            cb.Write(buffer, 0, buffer.Length);
            fixed (byte* src = readBuffer4)
            {
                Assert.AreEqual(1, cb.Peek(src, 1, 8));
            }
            Assert.AreEqual(9, cb.Count);
            Assert.IsFalse(cb.IsEmpty);

            Assert.IsTrue(readBuffer4.Take(1).SequenceEqual(buffer.Skip(8).Take(1)));
        }

        [TestMethod]
        public void PeekByteTest()
        {
            CircularBuffer cb = new CircularBuffer();

            byte[] buffer = { 45, 48, 72, 15 };
            cb.Write(buffer, 0, buffer.Length);
            byte b;
            Assert.IsTrue(cb.PeekByte(0, out b));
            Assert.AreEqual(b, 45);
            Assert.IsTrue(cb.PeekByte(1, out b));
            Assert.AreEqual(b, 48);
            Assert.IsTrue(cb.PeekByte(2, out b));
            Assert.AreEqual(b, 72);
            Assert.IsTrue(cb.PeekByte(3, out b));
            Assert.AreEqual(b, 15);
            Assert.IsFalse(cb.PeekByte(4, out b));
        }

        [TestMethod]
        public void SkipUntilTest()
        {
            CircularBuffer cb = new CircularBuffer();
            Assert.IsFalse(cb.SkipUntil(0, 0));

            byte[] buffer = { 45, 48, 72, 15 };
            cb.Write(buffer, 0, buffer.Length);

            Assert.IsFalse(cb.SkipUntil(0, 0));

            byte[] peekBuffer = new byte[4];
            Assert.AreEqual(0, cb.Peek(peekBuffer, 0, 4, 0));

            Assert.AreEqual(cb.Count, 0);

            Assert.AreEqual(buffer.Length, cb.Write(buffer, 0, buffer.Length));

            Assert.IsTrue(cb.SkipUntil(0, 48));

            cb.Peek(peekBuffer, 0, 2, 0);

            Assert.IsTrue(peekBuffer.Take(2).SequenceEqual(buffer.Skip(2)));
            Assert.IsFalse(cb.SkipUntil(0, 0));
            Assert.AreEqual(cb.Count, 0);
        }

        [TestMethod]
        public void PeekHeaderTest()
        {
            CircularBuffer cb = new CircularBuffer(16);

            byte[] buffer = { 12, 200, 4, 45, 177, 78, 147 };

            Assert.IsFalse(cb.PeekHeader(0, out byte h, out uint c1, out int d, out ushort c2));
            cb.Write(buffer, 0, buffer.Length); // 7

            Assert.IsTrue(
                cb.PeekHeader(0, out byte packetHeader, out uint commandID, out int dataLength, out ushort checksum));

            Assert.AreEqual(packetHeader, buffer[0]);

            Assert.AreEqual(commandID, (uint)((buffer[4] << 8) | buffer[3]));
            Assert.AreEqual(dataLength, (buffer[2] << 8) | buffer[1]);
            Assert.AreEqual(checksum, (ushort)((buffer[6] << 8) | buffer[5]));

            cb.Write(buffer, 0, buffer.Length);                     // 14
            Assert.AreEqual(2, cb.Write(buffer, 0, buffer.Length)); // 16

            Assert.IsTrue(cb.PeekHeader(7, out packetHeader, out commandID, out dataLength, out checksum));
            Assert.AreEqual(packetHeader, buffer[0]);

            Assert.AreEqual(commandID, (uint)((buffer[4] << 8) | buffer[3]));
            Assert.AreEqual(dataLength, (buffer[2] << 8) | buffer[1]);
            Assert.AreEqual(checksum, (ushort)((buffer[6] << 8) | buffer[5]));

            cb.Skip(7);

            Assert.AreEqual(buffer.Length - 2, cb.Write(buffer, 2, buffer.Length - 2));
            Assert.IsTrue(cb.PeekHeader(7, out packetHeader, out commandID, out dataLength, out checksum));

            Assert.AreEqual(packetHeader, buffer[0]);

            Assert.AreEqual(commandID, (uint)((buffer[4] << 8) | buffer[3]));
            Assert.AreEqual(dataLength, (buffer[2] << 8) | buffer[1]);
            Assert.AreEqual(checksum, (ushort)((buffer[6] << 8) | buffer[5]));

            cb.Skip(7);

            Assert.AreEqual(buffer.Length, cb.Write(buffer, 0, buffer.Length));
            Assert.IsTrue(cb.PeekHeader(7, out packetHeader, out commandID, out dataLength, out checksum));

            Assert.AreEqual(packetHeader, buffer[0]);

            Assert.AreEqual(commandID, (uint)((buffer[4] << 8) | buffer[3]));
            Assert.AreEqual(dataLength, (buffer[2] << 8) | buffer[1]);
            Assert.AreEqual(checksum, (ushort)((buffer[6] << 8) | buffer[5]));
        }
    }
}