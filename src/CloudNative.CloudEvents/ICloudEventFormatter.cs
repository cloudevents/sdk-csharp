// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents
{
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
        CloudEvent DecodeStructuredEvent(Stream data, IEnumerable<CloudEventAttribute> extensionAttributes);

        /// <summary>
        /// Decode a structured event from a stream asynchonously
        /// </summary>
        /// <param name="data"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        Task<CloudEvent> DecodeStructuredEventAsync(Stream data, IEnumerable<CloudEventAttribute> extensionAttributes);

        // TODO: Remove either this one or the stream one? It seems unnecessary to have both.

        /// <summary>
        /// Decode a structured event from a byte array
        /// </summary>
        /// <param name="data"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        CloudEvent DecodeStructuredEvent(byte[] data, IEnumerable<CloudEventAttribute> extensionAttributes);

        /// <summary>
        /// Encode an structured event into a byte array
        /// </summary>
        /// <param name="cloudEvent"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        byte[] EncodeStructuredEvent(CloudEvent cloudEvent, out ContentType contentType);
      
        // TODO: Work out whether this is what we want, and whether to potentially
        // separate it into a separate interface.
        byte[] EncodeData(object value);
        object DecodeData(byte[] value, string contentType);
    }
}