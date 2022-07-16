using System;

namespace IpcRpcSharedMemory.Models
{
    /// <summary>
    /// A raw RpcResponse that hasn't been deserialised
    /// </summary>
    internal class RpcUnprocessItem
    {
        /// <summary>
        /// Random ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Raw RpcResponse as byte array
        /// </summary>
        public byte[] Bytes { get; set; }
    }
}
