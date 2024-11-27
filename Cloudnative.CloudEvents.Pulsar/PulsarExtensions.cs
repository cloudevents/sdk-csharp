// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.Extensions;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Internal;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Reflection.PortableExecutable;
using System.Text;

namespace CloudNative.CloudEvents.Pulsar
{
    /// <summary>
    /// Extension methods to convert between CloudEvents and Pulsar messages.
    /// </summary>
    public static class PulsarExtensions
    {
        private const string PulsarHeaderPrefix = "ce-";

        // Visible for testing
        internal const string PulsarContentTypeAttributeName = "content-type";
        private const string SpecVersionPulsarHeader = PulsarHeaderPrefix + "specversion";

        /// <summary>
        /// Indicates whether this message holds a single CloudEvent.
        /// </summary>
        /// <remarks>
        /// This method returns false for batch requests, as they need to be parsed differently.
        /// </remarks>
        /// <param name="message">The message to check for the presence of a CloudEvent. Must not be null.</param>
        /// <returns>true, if the request is a CloudEvent</returns>
        public static bool IsCloudEvent(this IMessage<ReadOnlySequence<byte>> message) =>
            GetHeaderValue(message, SpecVersionPulsarHeader) is object ||
            MimeUtilities.IsCloudEventsContentType(GetHeaderValue(message, PulsarContentTypeAttributeName));

        /// <summary>
        /// Converts this Pulsar message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The Pulsar message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(this IMessage<ReadOnlySequence<byte>> message,
            CloudEventFormatter formatter, params CloudEventAttribute[]? extensionAttributes) =>
            ToCloudEvent(message, formatter, (IEnumerable<CloudEventAttribute>?) extensionAttributes);

        /// <summary>
        /// Converts this Pulsar message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The Pulsar message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(this IMessage<ReadOnlySequence<byte>> message,
            CloudEventFormatter formatter, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(message, nameof(message));
            Validation.CheckNotNull(formatter, nameof(formatter));

            if (!IsCloudEvent(message))
            {
                throw new InvalidOperationException();
            }

            var contentType = GetHeaderValue(message, PulsarContentTypeAttributeName);

            CloudEvent cloudEvent;

            // Structured mode
            if (MimeUtilities.IsCloudEventsContentType(contentType))
            {
                cloudEvent = formatter.DecodeStructuredModeMessage(message.Value().ToArray(), new ContentType(contentType), extensionAttributes);
            }
            else
            {
                // Binary mode
                if (!(GetHeaderValue(message, SpecVersionPulsarHeader) is string versionId))
                {
                    throw new ArgumentException("Request is not a CloudEvent");
                }
                CloudEventsSpecVersion version = CloudEventsSpecVersion.FromVersionId(versionId)
                    ?? throw new ArgumentException($"Unknown CloudEvents spec version '{versionId}'", nameof(message));

                cloudEvent = new CloudEvent(version, extensionAttributes)
                {
                    DataContentType = contentType
                };

                foreach (var header in message.Properties.Where(h => h.Key.StartsWith(PulsarHeaderPrefix)))
                {
                    var attributeName = header.Key.Substring(PulsarHeaderPrefix.Length).ToLowerInvariant();
                    if (attributeName == CloudEventsSpecVersion.SpecVersionAttribute.Name)
                    {
                        continue;
                    }
                    // TODO: Is this feasible?
                    var headerValue = header.Value;
                    if (headerValue is null)
                    {
                        continue;
                    }

                    cloudEvent.SetAttributeFromString(attributeName, headerValue);
                }
                formatter.DecodeBinaryModeEventData(message.Value().ToArray(), cloudEvent);
            }

            InitPartitioningKey(message, cloudEvent);
            return Validation.CheckCloudEventArgument(cloudEvent, nameof(message));
        }

        private static void InitPartitioningKey(IMessage<ReadOnlySequence<byte>> message, CloudEvent cloudEvent)
        {
            if (!string.IsNullOrEmpty(message.Key))
            {
                cloudEvent[Partitioning.PartitionKeyAttribute] = message.Key;
            }
        }

        /// <summary>
        /// Returns the last header value with the given name, or null if there is no such header.
        /// </summary>
        private static string? GetHeaderValue(IMessage<ReadOnlySequence<byte>> message, string headerName) =>
            Validation.CheckNotNull(message.Properties, nameof(message.Properties)) is null
            ? null
            : message.Properties.TryGetValue(headerName, out var headerValue) ? headerValue : null;


        public static MessageMetadata ToPulsarMessageMetadata(this CloudEvent cloudEvent)
        {
            var metadata = new MessageMetadata();
            metadata[SpecVersionPulsarHeader] = cloudEvent.SpecVersion.VersionId;
            foreach (var attribute in cloudEvent.GetPopulatedAttributes())
            {
                metadata[PulsarHeaderPrefix + attribute.Key.Name]=attribute.Value.ToString();
            }
            return metadata;
        }

        public static byte[] ToPulsarMessageBody(this CloudEvent cloudEvent, CloudEventFormatter formatter, ContentMode contentMode)
        {
            switch (contentMode)
            {
                case ContentMode.Structured:
                    return formatter.EncodeStructuredModeMessage(cloudEvent, out _).ToArray();
                case ContentMode.Binary:
                    return formatter.EncodeBinaryModeEventData(cloudEvent).ToArray();
                default:
                    throw new ArgumentOutOfRangeException(nameof(contentMode), $"Unsupported content mode: {contentMode}");
            }
        }
    }
}
