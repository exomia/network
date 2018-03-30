namespace Exomia.Network.Serialization
{
    /// <summary>
    ///     ISerializable interface
    /// </summary>
    public interface ISerializable
    {
        #region Methods

        /// <summary>
        ///     serialize the object to a byte array
        /// </summary>
        /// <returns></returns>
        byte[] Serialize();

        /// <summary>
        ///     deserialize the object from a byte array
        /// </summary>
        /// <param name="data"></param>
        void Deserialize(byte[] data);

        #endregion
    }
}