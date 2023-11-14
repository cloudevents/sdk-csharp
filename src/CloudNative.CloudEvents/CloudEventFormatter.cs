// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
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
        /// Decodes a CloudEvent from a structured-mode message body, represented as a read-only memory segment.
        /// </summary>
        /// <param name="body">The message body (content).</param>
        /// <param name="contentType">The content type of the message, or null if no content type is known.
        /// Typically this is a content type with a media type of "application/cloudevents"; the additional
        /// information such as the charset parameter may be needed in order to decode the message body.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The CloudEvent derived from the structured message body.</returns>
        public abstract CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes);

        /// <summary>
        /// Decodes a CloudEvent from a structured-mode message body, represented as a stream. The default implementation copies the
        /// content of the stream into a read-only memory segment before passing it to <see cref="DecodeStructuredModeMessage(ReadOnlyMemory{byte}, ContentType, IEnumerable{CloudEventAttribute})"/>
        /// but this can be overridden by event formatters that can decode a stream more efficiently.
        /// </summary>
        /// <param name="messageBody">The message body (content). Must not be null.</param>
        /// <param name="contentType">The content type of the message, or null if no content type is known.
        /// Typically this is a content type with a media type of "application/cloudevents"; the additional
        /// information such as the charset parameter may be needed in order to decode the message body.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The decoded CloudEvent.</returns>
        public virtual CloudEvent DecodeStructuredModeMessage(Stream messageBody, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            var bytes = BinaryDataUtilities.ToReadOnlyMemory(messageBody);
            return DecodeStructuredModeMessage(bytes, contentType, extensionAttributes);
        }

        /// <summary>
        /// Asynchronously decodes a CloudEvent from a structured-mode message body, represented as a stream. The default implementation asynchronously copies the
        /// content of the stream into a read-only memory segment before passing it to <see cref="DecodeStructuredModeMessage(ReadOnlyMemory{byte}, ContentType, IEnumerable{CloudEventAttribute})"/>
        /// but this can be overridden by event formatters that can decode a stream more efficiently.
        /// </summary>
        /// <param name="body">The message body (content). Must not be null.</param>
        /// <param name="contentType">The content type of the message, or null if no content type is known.
        /// Typically this is a content type with a media type of "application/cloudevents"; the additional
        /// information such as the charset parameter may be needed in order to decode the message body.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The CloudEvent derived from the structured message body.</returns>
        public virtual async Task<CloudEvent> DecodeStructuredModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            var bytes = await BinaryDataUtilities.ToReadOnlyMemoryAsync(body).ConfigureAwait(false);
            return DecodeStructuredModeMessage(bytes, contentType, extensionAttributes);
        }

        /// <summary>
        /// Encodes a CloudEvent as the body of a structured-mode message.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to encode. Must not be null.</param>
        /// <param name="contentType">On successful return, the content type of the structured-mode message body.
        /// Must not be null (on return).</param>
        /// <returns>The structured-mode representation of the CloudEvent.</returns>
        public abstract ReadOnlyMemory<byte> EncodeStructuredModeMessage(CloudEvent cloudEvent, out ContentType contentType);

        /// <summary>
        /// Decodes the given data obtained from a binary-mode message, populating the <see cref="CloudEvent.Data"/>
        /// property of <paramref name="cloudEvent"/>. Other attributes within the CloudEvent may be used to inform
        /// the interpretation of the message body. This method is expected to be called after all other aspects of the CloudEvent
        /// have been populated.
        /// </summary>
        /// <param name="body">The message body (content). Must not be null, but may be empty.</param>
        /// <param name="cloudEvent">The CloudEvent whose Data property should be populated. Must not be null.</param>
        /// <exception cref="ArgumentException">The data in the given CloudEvent cannot be decoded by this
        /// event formatter.</exception>
        public abstract void DecodeBinaryModeEventData(ReadOnlyMemory<byte> body, CloudEvent cloudEvent);

        /// <summary>
        /// Encodes the data from <paramref name="cloudEvent"/> in a manner suitable for a binary mode message.
        /// </summary>
        /// <exception cref="ArgumentException">The data in the given CloudEvent cannot be encoded by this
        /// event formatter.</exception>
        /// <returns>The binary-mode representation of the CloudEvent.</returns>
        public abstract ReadOnlyMemory<byte> EncodeBinaryModeEventData(CloudEvent cloudEvent);

        /// <summary>
        /// Decodes a collection CloudEvents from a batch-mode message body, represented as a read-only memory segment.
        /// </summary>
        /// <param name="body">The message body (content).</param>
        /// <param name="contentType">The content type of the message, or null if no content type is known.
        /// Typically this is a content type with a media type with a prefix of "application/cloudevents-batch"; the additional
        /// information such as the charset parameter may be needed in order to decode the message body.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The collection of CloudEvents derived from the batch message body.</returns>
        public abstract IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes);

        /// <summary>
        /// Decodes a collection CloudEvents from a batch-mode message body, represented as a stream. The default implementation copies the
        /// content of the stream into a read-only memory segment before passing it to <see cref="DecodeBatchModeMessage(ReadOnlyMemory{byte}, ContentType, IEnumerable{CloudEventAttribute})"/>
        /// but this can be overridden by event formatters that can decode a stream more efficiently.
        /// </summary>
        /// <param name="body">The message body (content). Must not be null.</param>
        /// <param name="contentType">The content type of the message, or null if no content type is known.
        /// Typically this is a content type with a media type with a prefix of "application/cloudevents"; the additional
        /// information such as the charset parameter may be needed in order to decode the message body.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The collection of CloudEvents derived from the batch message body.</returns>
        public virtual IReadOnlyList<CloudEvent> DecodeBatchModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            var bytes = BinaryDataUtilities.ToReadOnlyMemory(body);
            return DecodeBatchModeMessage(bytes, contentType, extensionAttributes);
        }

        /// <summary>
        /// Asynchronously decodes a collection CloudEvents from a batch-mode message body, represented as a stream. The default implementation asynchronously copies the
        /// content of the stream into a read-only memory segment before passing it to <see cref="DecodeBatchModeMessage(ReadOnlyMemory{byte}, ContentType, IEnumerable{CloudEventAttribute})"/>
        /// but this can be overridden by event formatters that can decode a stream more efficiently.
        /// </summary>
        /// <param name="body">The message body (content). Must not be null.</param>
        /// <param name="contentType">The content type of the message, or null if no content type is known.
        /// Typically this is a content type with a media type with a prefix of "application/cloudevents"; the additional
        /// information such as the charset parameter may be needed in order to decode the message body.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The collection of CloudEvents derived from the batch message body.</returns>
        public virtual async Task<IReadOnlyList<CloudEvent>> DecodeBatchModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            var bytes = await BinaryDataUtilities.ToReadOnlyMemoryAsync(body).ConfigureAwait(false);
            return DecodeBatchModeMessage(bytes, contentType, extensionAttributes);
        }

        /// <summary>
        /// Encodes a sequence of CloudEvents as the body of a message.
        /// </summary>
        /// <param name="cloudEvents">The CloudEvents to encode. Must not be null.</param>
        /// <param name="contentType">On successful return, the content type of the batch message body.
        /// Must not be null (on return).</param>
        /// <returns>The batch representation of the CloudEvent.</returns>
        public abstract ReadOnlyMemory<byte> EncodeBatchModeMessage(IEnumerable<CloudEvent> cloudEvents, out ContentType contentType);

        /// <summary>
        /// Determines the effective data content type of the given CloudEvent.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This implementation validates that <paramref name="cloudEvent"/> is not null,
        /// returns the existing <see cref="CloudEvent.DataContentType"/> if that's not null,
        /// and otherwise returns null if <see cref="CloudEvent.Data"/> is null or
        /// delegates to <see cref="InferDataContentType(object)"/> to infer the data content type
        /// from the actual data.
        /// </para>
        /// <para>
        /// Derived classes may override this if additional information is needed from the CloudEvent
        /// in order to determine the effective data content type, but most cases can be handled by
        /// simply overriding <see cref="InferDataContentType(object)"/>.
        /// </para>
        /// </remarks>
        /// <param name="cloudEvent">The CloudEvent to get or infer the data content type from. Must not be null.</param>
        /// <returns>The data content type of the CloudEvent, or null for no data content type.</returns>
        public virtual string? GetOrInferDataContentType(CloudEvent cloudEvent)
        {
            Validation.CheckNotNull(cloudEvent, nameof(cloudEvent));
            return cloudEvent.DataContentType is string dataContentType ? dataContentType
                : cloudEvent.Data is not object data ? null
                : InferDataContentType(data);
        }

        /// <summary>
        /// Infers the effective data content type based on the actual data. This base implementation
        /// always returns null, but derived classes may override this method to effectively provide
        /// a default data content type based on the in-memory data type.
        /// </summary>
        /// <param name="data">The data within a CloudEvent. Should not be null.</param>
        /// <returns>The inferred content type, or null if no content type is inferred.</returns>
        protected virtual string? InferDataContentType(object data) => null;
    }
}