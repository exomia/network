#region License

// Copyright (c) 2018-2020, exomia
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
        ///     The response id.
        /// </summary>
        /// <remarks>
        ///     <p>Specify this id instead of a command id in case you need to respond to a request.</p>
        /// </remarks>
        public readonly ushort ID;

        /// <summary>
        ///     The result.
        /// </summary>
        public readonly TResult Result;

        /// <summary>
        ///     The send error.
        /// </summary>
        public readonly SendError SendError;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Response{TResult}" /> struct.
        /// </summary>
        /// <param name="result">     The result. </param>
        /// <param name="responseID"> The result. </param>
        /// <param name="sendError">  The send error. </param>
        internal Response(in TResult result, ushort responseID, SendError sendError)
        {
            Result    = result;
            ID        = responseID;
            SendError = sendError;
        }

        /// <summary>
        ///     <c>true</c> if no SendError occurred; <c>false</c> otherwise.
        /// </summary>
        /// <param name="r"> The in <see cref="Response{TResult}" /> to process. </param>
        /// <returns>
        ///     The result of the operation.
        /// </returns>
        public static implicit operator bool(in Response<TResult> r)
        {
            return r.SendError == SendError.None;
        }

        /// <summary>
        ///     Implicit cast that converts the given in Response&lt;TResult&gt; to a TResult.
        /// </summary>
        /// <param name="r"> The in <see cref="Response{TResult}" /> to process. </param>
        /// <returns>
        ///     The result of the operation.
        /// </returns>
        public static implicit operator TResult(in Response<TResult> r)
        {
            return r.Result;
        }
    }
}