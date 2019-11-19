// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Mime;
    using System.Threading.Tasks;


    /// <summary>
    /// Implemented by formatters
    /// </summary>
    public interface ICloudEventFormatter
    {
        /// <summary>
        /// Decode a structured event from a stream
        /// </summary>
        /// <param name="data"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        CloudEvent DecodeStructuredEvent(Stream data, IEnumerable<ICloudEventExtension> extensions);
        /// <summary>
        /// Decode a structured event from a stream asynchonously
        /// </summary>
        /// <param name="data"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        Task<CloudEvent> DecodeStructuredEventAsync(Stream data, IEnumerable<ICloudEventExtension> extensions);
        /// <summary>
        /// Decode a structured event from a byte array
        /// </summary>
        /// <param name="data"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        CloudEvent DecodeStructuredEvent(byte[] data, IEnumerable<ICloudEventExtension> extensions);
        /// <summary>
        /// Encode an structured event into a byte array
        /// </summary>
        /// <param name="cloudEvent"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        byte[] EncodeStructuredEvent(CloudEvent cloudEvent, out ContentType contentType);
      
        /// <summary>
        /// Decode an attribute from a byte array
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        object DecodeAttribute(CloudEventsSpecVersion specVersion, string name, byte[] data, IEnumerable<ICloudEventExtension> extensions);
        /// <summary>
        /// Encode an attribute into a byte array
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        byte[] EncodeAttribute(CloudEventsSpecVersion specVersion, string name, object value, IEnumerable<ICloudEventExtension> extensions);
    }
}