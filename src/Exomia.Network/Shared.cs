#region License

// Copyright (c) 2018-2021, exomia
// All rights reserved.
// 
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

#endregion

namespace Exomia.Network
{
    static class Shared
    {
        public static ulong Scramble(ulong input)
        {
            ulong o = input ^ 0x1BADBABEBADC0DEDul;
            return (((o & 0xF0F0F0F0F0F0F0F0) >> 5) | ((o & 0x0F0F0F0F0F0F0F0F) << 7)) ^
                   ((0x2AB5C59Dul << 32) | Constants.PROTOCOL_VERSION);
        }
    }
}