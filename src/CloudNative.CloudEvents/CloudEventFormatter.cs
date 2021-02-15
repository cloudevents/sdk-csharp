// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents
{
    /// <summary>
    /// Implemented by formatters
    /// </summary>
    public abstract class CloudEventFormatter
    {
        /// <summary>
        /// Decode a structured event from a stream. The default implementation copies the
        /// content of the stream into a byte array before passing it to <see cref="DecodeStructuredEvent(byte[], IEnumerable{CloudEventAttribute})"/>,
        /// but this can be overridden by event formatters that can decode a stream more efficiently.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        public virtual CloudEvent DecodeStructuredEvent(Stream data, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            var bytes = BinaryDataUtilities.ToByteArray(data);
            return DecodeStructuredEvent(bytes, extensionAttributes);
        }

        /// <summary>
        /// Decode a structured event from a stream. The default implementation asynchronously copies the
        /// content of the stream into a byte array before passing it to <see cref="DecodeStructuredEvent(byte[], IEnumerable{CloudEventAttribute})"/>,
        /// but this can be overridden by event formatters that can decode a stream more efficiently.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        public virtual async Task<CloudEvent> DecodeStructuredEventAsync(Stream data, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            var bytes = await BinaryDataUtilities.ToByteArrayAsync(data).ConfigureAwait(false);
            return DecodeStructuredEvent(bytes, extensionAttributes);
        }

        // TODO: Remove either this one or the stream one? It seems unnecessary to have both.

        /// <summary>
        /// Decode a structured event from a byte array
        /// </summary>
        /// <param name="data"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        public virtual CloudEvent DecodeStructuredEvent(byte[] data, IEnumerable<CloudEventAttribute> extensionAttributes) =>
            throw new NotImplementedException();

        /// <summary>
        /// Encode an structured event into a byte array
        /// </summary>
        /// <param name="cloudEvent"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        public virtual byte[] EncodeStructuredEvent(CloudEvent cloudEvent, out ContentType contentType) =>
            throw new NotImplementedException();

        // TODO: Work out whether this is what we want, and whether to potentially
        // separate it into a separate interface.
        public virtual byte[] EncodeData(object value) => throw new NotImplementedException();
        public virtual object DecodeData(byte[] value, string contentType) => throw new NotImplementedException();
    }
}