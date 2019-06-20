#region License

// Copyright (c) 2018-2019, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

namespace Exomia.Network
{
    /// <summary>
    ///     A response.
    /// </summary>
    /// <typeparam name="TResult"> Type of the result. </typeparam>
    public readonly struct Response<TResult>
    {
        /// <summary>
        ///     Result.
        /// </summary>
        public readonly TResult Result;

        /// <summary>
        ///     SendError.
        /// </summary>
        public readonly SendError SendError;

        /// <summary>
        ///     Initializes a new instance of the &lt;see cref="Response&lt;TResult&gt;"/&gt; struct.
        /// </summary>
        /// <param name="result">    Result. </param>
        /// <param name="sendError"> SendError. </param>
        internal Response(in TResult result, SendError sendError)
        {
            Result    = result;
            SendError = sendError;
        }

        /// <summary>
        ///     <c>true</c> if no SendError occured; <c>false</c> otherwise.
        /// </summary>
        /// <param name="r"> instance of Response{TResult} </param>
        /// <returns>
        ///     The result of the operation.
        /// </returns>
        public static implicit operator bool(in Response<TResult> r)
        {
            return r.SendError == SendError.None;
        }

        /// <summary>
        ///     implicit operator TResult.
        /// </summary>
        /// <param name="r"> instance of Response{TResult} </param>
        /// <returns>
        ///     The result of the operation.
        /// </returns>
        public static implicit operator TResult(in Response<TResult> r)
        {
            return r.Result;
        }
    }
}