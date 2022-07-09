using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace IpcRpcSharedMemory.Models.Utilities
{
    /// <summary>
    /// Object / model to byte / byte to object / model
    /// </summary>
    internal static class Serialization
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        /// <summary>
        /// Compare byte array (quickly)
        /// </summary>
        /// <param name="b1">Byte array 1</param>
        /// <param name="b2">Byte array 2</param>
        /// <returns></returns>
        internal static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            if (b1 == null && b2 != null)
                return false;

            if (b1 != null && b2 == null)
                return false;

            if (b1 == null && b2 == null)
                return true;

            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.  
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }

        /// <summary>
        /// Serialise object (RpcResult / RpcPayload) to xml encoded string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static string SerializeToXml<T>(this T data)
        {
            XmlSerializer serializer = new XmlSerializer(data.GetType());
            using (var stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, data);
                string xmlData = stringWriter.ToString();
                stringWriter.Close();
                return xmlData;
            }
        }

        /// <summary>
        /// Convert string (serialised RpcResult / RpcPayload) to byte array
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static byte[] ConvertStringToByteArray(string value)
        {
            return new System.Text.ASCIIEncoding().GetBytes(value);
        }

        /// <summary>
        /// Deserialise serialised RpcResult / RpcPayload (in xml encoded format)  to object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        internal static T DeserializeFromXml<T>(string xmlData)
        {
            XmlSerializer deserializer = new XmlSerializer(typeof(T));
            using (var stringReader = new StringReader(xmlData))
            {
                var data = (T)deserializer.Deserialize(stringReader);
                stringReader.Close();
                return data;
            }
        }

        /// <summary>
        /// Convert byte array of RpcResult /RpcPayload to string
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        internal static string ConvertByteArrayToString(this byte[] values)
        {
            return new System.Text.ASCIIEncoding().GetString(values);
        }
    }
}
