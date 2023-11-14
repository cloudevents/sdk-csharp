// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.Extensions;
using Confluent.Kafka;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;

namespace CloudNative.CloudEvents.Kafka
{
    /// <summary>
    /// Extension methods to convert between CloudEvents and Kafka messages.
    /// </summary>
    public static class KafkaExtensions
    {
        private const string KafkaHeaderPrefix = "ce_";

        // Visible for testing
        internal const string KafkaContentTypeAttributeName = "content-type";
        private const string SpecVersionKafkaHeader = KafkaHeaderPrefix + "specversion";

        /// <summary>
        /// Indicates whether this message holds a single CloudEvent.
        /// </summary>
        /// <remarks>
        /// This method returns false for batch requests, as they need to be parsed differently.
        /// </remarks>
        /// <param name="message">The message to check for the presence of a CloudEvent. Must not be null.</param>
        /// <returns>true, if the request is a CloudEvent</returns>
        public static bool IsCloudEvent(this Message<string?, byte[]> message) =>
            GetHeaderValue(message, SpecVersionKafkaHeader) is object ||
            MimeUtilities.IsCloudEventsContentType(GetHeaderValue(message, KafkaContentTypeAttributeName));

        /// <summary>
        /// Converts this Kafka message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The Kafka message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(this Message<string?, byte[]> message,
            CloudEventFormatter formatter, params CloudEventAttribute[]? extensionAttributes) =>
            ToCloudEvent(message, formatter, (IEnumerable<CloudEventAttribute>?) extensionAttributes);

        /// <summary>
        /// Converts this Kafka message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The Kafka message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(this Message<string?, byte[]> message,
            CloudEventFormatter formatter, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(message, nameof(message));
            Validation.CheckNotNull(formatter, nameof(formatter));

            if (!IsCloudEvent(message))
            {
                throw new InvalidOperationException();
            }

            var contentType = GetHeaderValue(message, KafkaContentTypeAttributeName);

            CloudEvent cloudEvent;

            // Structured mode
            if (MimeUtilities.IsCloudEventsContentType(contentType))
            {
                cloudEvent = formatter.DecodeStructuredModeMessage(message.Value, new ContentType(contentType), extensionAttributes);
            }
            else
            {
                // Binary mode
                if (!(GetHeaderValue(message, SpecVersionKafkaHeader) is string versionId))
                {
                    throw new ArgumentException("Request is not a CloudEvent");
                }
                CloudEventsSpecVersion version = CloudEventsSpecVersion.FromVersionId(versionId)
                    ?? throw new ArgumentException($"Unknown CloudEvents spec version '{versionId}'", nameof(message));

                cloudEvent = new CloudEvent(version, extensionAttributes)
                {
                    DataContentType = contentType
                };

                foreach (var header in message.Headers.Where(h => h.Key.StartsWith(KafkaHeaderPrefix)))
                {
                    var attributeName = header.Key.Substring(KafkaHeaderPrefix.Length).ToLowerInvariant();
                    if (attributeName == CloudEventsSpecVersion.SpecVersionAttribute.Name)
                    {
                        continue;
                    }
                    // TODO: Is this feasible?
                    var headerValue = header.GetValueBytes();
                    if (headerValue is null)
                    {
                        continue;
                    }
                    string attributeValue = Encoding.UTF8.GetString(headerValue);

                    cloudEvent.SetAttributeFromString(attributeName, attributeValue);
                }
                formatter.DecodeBinaryModeEventData(message.Value, cloudEvent);
            }

            InitPartitioningKey(message, cloudEvent);
            return Validation.CheckCloudEventArgument(cloudEvent, nameof(message));
        }

        private static void InitPartitioningKey(Message<string?, byte[]> message, CloudEvent cloudEvent)
        {
            if (!string.IsNullOrEmpty(message.Key))
            {
                cloudEvent[Partitioning.PartitionKeyAttribute] = message.Key;
            }
        }

        /// <summary>
        /// Returns the last header value with the given name, decoded using UTF-8, or null if there is no such header.
        /// </summary>
        private static string? GetHeaderValue(MessageMetadata message, string headerName) =>
            Validation.CheckNotNull(message, nameof(message)).Headers is null
            ? null
            : message.Headers.TryGetLastBytes(headerName, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;

        /// <summary>
        /// Converts a CloudEvent to a Kafka message.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        public static Message<string?, byte[]> ToKafkaMessage(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
            Validation.CheckNotNull(formatter, nameof(formatter));

            var headers = MapHeaders(cloudEvent);
            string? key = (string?) cloudEvent[Partitioning.PartitionKeyAttribute];
            byte[] value;
            string? contentTypeHeaderValue;

            switch (contentMode)
            {
                case ContentMode.Structured:
                    value = BinaryDataUtilities.AsArray(formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType));
                    // TODO: What about the non-media type parts?
                    contentTypeHeaderValue = contentType.MediaType;
                    break;
                case ContentMode.Binary:
                    value = BinaryDataUtilities.AsArray(formatter.EncodeBinaryModeEventData(cloudEvent));
                    contentTypeHeaderValue = formatter.GetOrInferDataContentType(cloudEvent);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contentMode), $"Unsupported content mode: {contentMode}");
            }
            if (contentTypeHeaderValue is object)
            {
                headers.Add(KafkaContentTypeAttributeName, Encoding.UTF8.GetBytes(contentTypeHeaderValue));
            }
            return new Message<string?, byte[]>
            {
                Headers = headers,
                Value = value,
                Key = key
            };
        }

        private static Headers MapHeaders(CloudEvent cloudEvent)
        {
            var headers = new Headers
            {
                { SpecVersionKafkaHeader, Encoding.UTF8.GetBytes(cloudEvent.SpecVersion.VersionId) }
            };
            foreach (var pair in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = pair.Key;
                if (attribute == cloudEvent.SpecVersion.DataContentTypeAttribute ||
                    attribute.Name == Partitioning.PartitionKeyAttribute.Name)
                {
                    continue;
                }
                var value = attribute.Format(pair.Value);
                headers.Add(KafkaHeaderPrefix + attribute.Name, Encoding.UTF8.GetBytes(value));
            }
            return headers;
        }
    }
}