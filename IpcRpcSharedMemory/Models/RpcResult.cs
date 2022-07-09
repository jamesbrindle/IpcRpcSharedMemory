using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace IpcRpcSharedMemory.Models
{
    /// <summary>
    /// Result object that is transferred back to the RcpRequest
    /// </summary>
    [XmlInclude(typeof(char))]
    [XmlInclude(typeof(char[]))]
    [XmlInclude(typeof(List<char>))]
    [XmlInclude(typeof(string))]
    [XmlInclude(typeof(string[]))]
    [XmlInclude(typeof(List<string>))]
    [XmlInclude(typeof(int))]
    [XmlInclude(typeof(int[]))]
    [XmlInclude(typeof(List<int>))]
    [XmlInclude(typeof(long))]
    [XmlInclude(typeof(long[]))]
    [XmlInclude(typeof(List<long>))]
    [XmlInclude(typeof(double))]
    [XmlInclude(typeof(double[]))]
    [XmlInclude(typeof(List<double>))]
    [XmlInclude(typeof(float))]
    [XmlInclude(typeof(float[]))]
    [XmlInclude(typeof(List<float>))]
    [XmlInclude(typeof(decimal))]
    [XmlInclude(typeof(decimal[]))]
    [XmlInclude(typeof(List<decimal>))]
    [XmlInclude(typeof(double))]
    [XmlInclude(typeof(double[]))]
    [XmlInclude(typeof(List<double>))]
    [XmlInclude(typeof(DateTime))]
    [XmlInclude(typeof(DateTime[]))]
    [XmlInclude(typeof(List<DateTime>))]
    [XmlInclude(typeof(DateTime))]
    [XmlInclude(typeof(DateTime[]))]
    [XmlInclude(typeof(List<DateTime>))]
    [XmlInclude(typeof(byte[]))]
    [XmlInclude(typeof(byte[][]))]
    [XmlInclude(typeof(List<byte[]>))]
    [XmlInclude(typeof(bool))]
    [XmlInclude(typeof(bool[]))]
    [XmlInclude(typeof(List<bool>))]
    public class RpcResult
    {
        /// <summary>
        /// The data the listener has sent back
        /// </summary>
        public object Content { get; set; }
    }
}
