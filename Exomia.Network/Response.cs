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
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    public readonly struct Response<TResult>
    {
        /// <summary>
        ///     Result
        /// </summary>
        public readonly TResult Result;

        /// <summary>
        ///     SendError
        /// </summary>
        public readonly SendError SendError;

        internal Response(in TResult result, SendError sendError)
        {
            Result    = result;
            SendError = sendError;
        }

        /// <summary>
        ///     <c>true</c> if no SendError occured; <c>false</c> otherwise
        /// </summary>
        /// <param name="r">instance of Response{TResult}</param>
        public static implicit operator bool(in Response<TResult> r)
        {
            return r.SendError == SendError.None;
        }

        /// <summary>
        ///     <c>true</c> if no SendError occured; <c>false</c> otherwise
        /// </summary>
        /// <param name="r">instance of Response{TResult}</param>
        public static implicit operator TResult(in Response<TResult> r)
        {
            return r.Result;
        }
    }
}