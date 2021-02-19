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
    /// Performs CloudEvent conversions as part of encoding and decoding messages for protocol bindings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Event formatters are responsible for complete CloudEvent encoding and decoding for structured-mode messages (where
    /// all the CloudEvent information is represented within the message body), and data-only encoding and decoding
    /// for binary-mode messages (where CloudEvent attributes are represented in message metadata, and the CloudEvent data
    /// is represented in the message body).
    /// </para>
    /// <para>
    /// Each event formatter type is responsible for documenting what types of value are acceptable for the <see cref="CloudEvent.Data"/>
    /// property in CloudEvents it is asked to encode, and likewise what types of value will be present in the same property
    /// when it is asked to decode a message. Event formatters should aim to be as consistent as possible with respect to data handling
    /// between structured and binary modes, although this is not always possible as the structured mode representation may contain
    /// more hints around how to interpret the data than the binary mode representation. Inconsistencies should be carefully
    /// noted so that consumers can write robust code.
    /// </para>
    /// <para>
    /// An event format is often naturally associated with a particular kind of data, but it is not limited to working with
    /// that kind. For example, the JSON event format allows JSON data to be stored particularly naturally within the structured-mode
    /// message body (which is itself JSON), but it is still able to handle arbitrary binary or text data.
    /// </para>
    /// </remarks>
    public abstract class CloudEventFormatter
    {
        /// <summary>
        /// Asynchronously decodes a CloudEvent from a structured-mode message body, represented as a stream. The default implementation copies the
        /// content of the stream into a byte array before passing it to <see cref="DecodeStructuredModeMessage(byte[], ContentType, IEnumerable{CloudEventAttribute})"/>
        /// but this can be overridden by event formatters that can decode a stream more efficiently.
        /// </summary>
        /// <param name="data">The data within the message body. Must not be null.</param>
        /// <param name="contentType">The content type of the message, or null if no content type is known.
        /// Typically this is a content type with a media type of "application/cloudevents"; the additional
        /// information such as the charset parameter may be needed in order to decode the data.</param>
        /// <param name="extensions">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The decoded CloudEvent.</returns>
        public virtual CloudEvent DecodeStructuredModeMessage(Stream data, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            var bytes = BinaryDataUtilities.ToByteArray(data);
            return DecodeStructuredModeMessage(bytes, contentType, extensionAttributes);
        }

        /// <summary>
        /// Decodes a CloudEvent from a structured-mode message body, represented as a stream. The default implementation asynchronously copies the
        /// content of the stream into a byte array before passing it to <see cref="DecodeStructuredModeMessage(byte[], ContentType, IEnumerable{CloudEventAttribute})"/>
        /// but this can be overridden by event formatters that can decode a stream more efficiently.
        /// </summary>
        /// <param name="data">The data within the message body. Must not be null.</param>
        /// <param name="contentType">The content type of the message, or null if no content type is known.
        /// Typically this is a content type with a media type of "application/cloudevents"; the additional
        /// information such as the charset parameter may be needed in order to decode the data.</param>
        /// <param name="extensions">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The CloudEvent derived from the structured data.</returns>
        public virtual async Task<CloudEvent> DecodeStructuredModeMessageAsync(Stream data, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            var bytes = await BinaryDataUtilities.ToByteArrayAsync(data).ConfigureAwait(false);
            return DecodeStructuredModeMessage(bytes, contentType, extensionAttributes);
        }

        /// <summary>
        /// Decodes a CloudEvent from a structured-mode message body, represented as a byte array.
        /// </summary>
        /// <param name="data">The data within the message body. Must not be null.</param>
        /// <param name="contentType">The content type of the message, or null if no content type is known.
        /// Typically this is a content type with a media type of "application/cloudevents"; the additional
        /// information such as the charset parameter may be needed in order to decode the data.</param>
        /// <param name="extensions">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The CloudEvent derived from the structured data.</returns>
        public abstract CloudEvent DecodeStructuredModeMessage(byte[] data, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes);

        /// <summary>
        /// Encodes a CloudEvent as the body of a structured-mode message.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to encode. Must not be null.</param>
        /// <param name="contentType">On successful return, the content type of the structured-mode data.
        /// Must not be null (on return).</param>
        /// <returns>The structured-mode representation of the CloudEvent.</returns>
        public abstract byte[] EncodeStructuredModeMessage(CloudEvent cloudEvent, out ContentType contentType);

        /// <summary>
        /// Encodes the data from <paramref name="cloudEvent"/> in a manner suitable for a binary mode message.
        /// </summary>
        /// <exception cref="ArgumentException">The data in the given CloudEvent cannot be encoded by this
        /// event formatter.</exception>
        /// <returns>The binary-mode representation of the CloudEvent.</returns>
        public abstract byte[] EncodeBinaryModeEventData(CloudEvent cloudEvent);

        /// <summary>
        /// Decodes the given data obtained from a binary-mode message, populating the <see cref="CloudEvent.Data"/>
        /// property of <paramref name="cloudEvent"/>. Other attributes within the CloudEvent may be used to inform
        /// the interpretation of the data. This method is expected to be called after all other aspects of the CloudEvent
        /// have been populated.
        /// </summary>
        /// <param name="data">The data from the message. Must not be null, but may be empty.</param>
        /// <param name="cloudEvent">The CloudEvent whose Data property should be populated. Must not be null.</param>
        /// <exception cref="ArgumentException">The data in the given CloudEvent cannot be decoded by this
        /// event formatter.</exception>
        public abstract void DecodeBinaryModeEventData(byte[] data, CloudEvent cloudEvent);
    }
}