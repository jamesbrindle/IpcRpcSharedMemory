﻿using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace IpcRpcSharedMemory.Models
{
    /// <summary>
    /// Data object that gets transfer to listener
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
    public class RpcPayload : RpcRequestMessage
    {
        /// <summary>
        /// Individual message unique identifier
        /// </summary>
        public Guid MessageGuid { get; set; }
    }
}
