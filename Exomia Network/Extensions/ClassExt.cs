using Exomia.Network.Serialization;

namespace Exomia.Network.Extensions.Class
{
    /// <summary>
    ///     ClassExt class
    /// </summary>
    public static class ClassExt
    {
        /// <summary>
        ///     returns a new deserialized object from a byte array
        /// </summary>
        /// <typeparam name="T">ISerializable</typeparam>
        /// <param name="arr">byte array</param>
        /// <param name="obj">out object</param>
        public static void FromBytes<T>(this byte[] arr, out T obj)
            where T : ISerializable, new()
        {
            obj = new T();
            obj.Deserialize(arr);
        }

        /// <summary>
        ///     returns a new deserialized object from a byte array
        /// </summary>
        /// <typeparam name="T">ISerializable</typeparam>
        /// <param name="arr">byte array</param>
        /// <returns>returns a new deserialized object from a byte array</returns>
        public static T FromBytes<T>(this byte[] arr)
            where T : ISerializable, new()
        {
            T obj = new T();
            obj.Deserialize(arr);
            return obj;
        }
    }
}