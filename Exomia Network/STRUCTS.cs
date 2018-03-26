using System.Runtime.InteropServices;

namespace Exomia.Network
{
    /// <summary>
    ///     PING_STRUCT
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    public struct PING_STRUCT
    {
        /// <summary>
        ///     TimeStamp
        /// </summary>
        public long TimeStamp;
    }

    /// <summary>
    ///     CLIENTINFO_STRUCT
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 72)]
    public struct CLIENTINFO_STRUCT
    {
        /// <summary>
        ///     ClientID
        /// </summary>
        public long ClientID;

        /// <summary>
        ///     ClientName (64)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ClientName;
    }

    /// <summary>
    ///     UDP_CONNECT_STRUCT
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct UDP_CONNECT_STRUCT
    {
        /// <summary>
        ///     Checksum(16)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Checksum;
    }
}