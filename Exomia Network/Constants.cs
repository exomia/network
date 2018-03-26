namespace Exomia.Network
{
    /// <summary>
    ///     Constants
    /// </summary>
    public static class Constants
    {
        internal const int HEADER_SIZE = 4;
        internal const int PACKET_SIZE_MAX = 16383;

        internal const double UDP_IDLE_TIME = 20000.0;

        internal const uint COMMANDID_MASK = 0xFFC00000;
        internal const uint TYPE_MASK = 0x3FC000;
        internal const uint DATA_LENGTH_MASK = 0x3FFF;

        internal const uint CLIENTINFO_STRUCT_TYPE_ID = 255;
        internal const uint UDP_CONNECT_STRUCT_TYPE_ID = 255;
        internal const uint PING_STRUCT_TYPE_ID = 255;

        /// <summary>
        ///     CLIENTINFO_COMMAND_ID
        /// </summary>
        public const uint CLIENTINFO_COMMAND_ID = 1023;

        /// <summary>
        ///     UDP_CONNEC_COMMAND_ID
        /// </summary>
        public const uint UDP_CONNECT_COMMAND_ID = 1022;

        /// <summary>
        ///     PING_COMMAND_ID
        /// </summary>
        public const uint PING_COMMAND_ID = 1021;
    }
}