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

using System;
using System.Linq;
using Exomia.Network.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Exomia.Network.UnitTest
{
    [TestClass]
    public unsafe class CircularBuffer_UnitTest1
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
        public void CircularBuffer_Initialize_With_898_Capacity_ShouldBe_4096()
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
            CircularBuffer cb = new CircularBuffer(1024);
            byte[] buffer = { 45, 48, 72, 15 };
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
            CircularBuffer cb = new CircularBuffer(1024);
            byte[] buffer = { 45, 48, 72, 15 };
            fixed (byte* src = buffer)
            {
                cb.Write(src, 0, 4);

                Assert.AreEqual(cb.Count, 4);

                cb.Write(src, 2, 2);

                Assert.AreEqual(cb.Count, 6);

                cb.Write(src, 1, 2);

                Assert.AreEqual(cb.Count, 8);
            }
        }

        [TestMethod]
        public void SafeReadTest()
        {
            CircularBuffer cb = new CircularBuffer(1024);

            byte[] buffer = { 45, 48, 72, 15 };
            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 4);

            byte[] readBuffer = new byte[4];
            cb.Read(readBuffer, 0, readBuffer.Length, 0);

            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            Assert.IsTrue(readBuffer.SequenceEqual(buffer));

            Assert.ThrowsException<InvalidOperationException>(
                () =>
                {
                    cb.Read(readBuffer, 0, readBuffer.Length, 0);
                });

            byte[] buffer2 = { 45, 48, 72, 1, 4, 87, 95 };
            cb.Write(buffer2, 0, buffer2.Length);

            byte[] readBuffer2 = new byte[buffer2.Length];
            cb.Read(readBuffer2, 0, buffer2.Length - 2, 2);

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
                cb.Write(src, 0, buffer.Length);

                Assert.AreEqual(cb.Count, 77);

                cb.Write(src, 0, buffer.Length);

                Assert.AreEqual(cb.Count, 128);

                cb.Write(src, 0, buffer.Length);

                Assert.AreEqual(cb.Count, 128);
            }
        }

        [TestMethod]
        public void SafeReadTest_With_Overflow()
        {
            Random rnd = new Random(1337);

            CircularBuffer cb = new CircularBuffer(16);

            byte[] buffer = new byte[9];
            rnd.NextBytes(buffer);

            cb.Write(buffer, 0, buffer.Length);
            cb.Write(buffer, 0, buffer.Length);

            Assert.AreEqual(cb.Count, 16);

            byte[] readBuffer2 = new byte[16];
            cb.Read(readBuffer2, 0, readBuffer2.Length, 0);

            Assert.AreEqual(cb.Count, 0);
            Assert.IsTrue(cb.IsEmpty);

            Assert.IsTrue(readBuffer2.Take(7).SequenceEqual(buffer.Skip(2)));

            byte[] shouldbe = buffer.Skip(2).Concat(buffer).ToArray();

            Assert.IsTrue(readBuffer2.SequenceEqual(shouldbe));
        }

        [TestMethod]
        public void UnsafeReadTest_With_Overflow()
        {
            Random rnd = new Random(1337);

            CircularBuffer cb = new CircularBuffer(16);

            byte[] buffer = new byte[9];
            rnd.NextBytes(buffer);

            fixed (byte* src = buffer)
            {
                cb.Write(src, 0, buffer.Length);
                cb.Write(src, 0, buffer.Length);
            }

            Assert.AreEqual(cb.Count, 16);

            byte[] readBuffer = new byte[16];

            fixed (byte* dest = readBuffer)
            {
                cb.Read(dest, 0, readBuffer.Length, 0);

                Assert.AreEqual(cb.Count, 0);
                Assert.IsTrue(cb.IsEmpty);

                Assert.IsTrue(readBuffer.Take(7).SequenceEqual(buffer.Skip(2)));

                byte[] shouldbe = buffer.Skip(2).Concat(buffer).ToArray();

                Assert.IsTrue(readBuffer.SequenceEqual(shouldbe));
            }
        }
    }
}