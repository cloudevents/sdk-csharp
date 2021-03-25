// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Extensions;
using CloudNative.CloudEvents.Core;
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
    public static class KafkaClientExtensions
    {
        private const string KafkaHeaderPrefix = "ce_";

        private const string KafkaContentTypeAttributeName = "content-type";
        private const string SpecVersionKafkaHeader = KafkaHeaderPrefix + "specversion";

        // TODO: Avoid all the byte[] -> string conversions? If we didn't care about case-sensitivity, we could prepare byte arrays to perform comparisons with.

        public static bool IsCloudEvent(this Message<string, byte[]> message) =>
            GetHeaderValue(message, SpecVersionKafkaHeader) is object ||
            (ExtractContentType(message)?.StartsWith(CloudEvent.MediaType, StringComparison.InvariantCultureIgnoreCase) == true);


        /// <summary>
        /// Converts this Kafka message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The Kafka message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(this Message<string, byte[]> message,
            CloudEventFormatter formatter, params CloudEventAttribute[] extensionAttributes) =>
            ToCloudEvent(message, formatter, (IEnumerable<CloudEventAttribute>) extensionAttributes);

        /// <summary>
        /// Converts this Kafka message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The Kafka message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(this Message<string, byte[]> message,
            CloudEventFormatter formatter, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            Validation.CheckNotNull(message, nameof(message));
            Validation.CheckNotNull(formatter, nameof(formatter));

            if (!IsCloudEvent(message))
            {
                throw new InvalidOperationException();
            }

            var contentType = ExtractContentType(message);

            CloudEvent cloudEvent;

            // Structured mode
            if (contentType?.StartsWith(CloudEvent.MediaType, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                cloudEvent = formatter.DecodeStructuredModeMessage(message.Value, new ContentType(contentType), extensionAttributes);
            }
            else
            {
                // Binary mode
                if (!(GetHeaderValue(message, SpecVersionKafkaHeader) is byte[] versionIdBytes))
                {
                    throw new ArgumentException("Request is not a CloudEvent");
                }
                string versionId = Encoding.UTF8.GetString(versionIdBytes);
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

        private static string ExtractContentType(Message<string, byte[]> message)
        {
            var headerValue = GetHeaderValue(message, KafkaContentTypeAttributeName);
            return headerValue is null ? null : Encoding.UTF8.GetString(headerValue);
        }

        private static void InitPartitioningKey(Message<string, byte[]> message, CloudEvent cloudEvent)
        {
            if (!string.IsNullOrEmpty(message.Key))
            {
                cloudEvent[Partitioning.PartitionKeyAttribute] = message.Key;
            }
        }

        private static byte[] GetHeaderValue(MessageMetadata message, string headerName) =>
            message.Headers.FirstOrDefault(x => string.Equals(x.Key, headerName, StringComparison.InvariantCultureIgnoreCase))
                ?.GetValueBytes();

        /// <summary>
        /// Converts a CloudEvent to <see cref="Message"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        public static Message<string, byte[]> ToKafkaMessage(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
            Validation.CheckNotNull(formatter, nameof(formatter));

            // TODO: Is this appropriate? Why can't we transport a CloudEvent without data in Kafka?
            Validation.CheckArgument(cloudEvent.Data is object, nameof(cloudEvent), "Only CloudEvents with data can be converted to Kafka messages");
            var headers = MapHeaders(cloudEvent, formatter);
            string key = (string) cloudEvent[Partitioning.PartitionKeyAttribute];
            byte[] value;
            string contentTypeHeaderValue;

            switch (contentMode)
            {
                case ContentMode.Structured:
                    value = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
                    // TODO: What about the non-media type parts?
                    contentTypeHeaderValue = contentType.MediaType;
                    break;
                case ContentMode.Binary:
                    value = formatter.EncodeBinaryModeEventData(cloudEvent);
                    contentTypeHeaderValue = cloudEvent.DataContentType;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contentMode), $"Unsupported content mode: {contentMode}");
            }
            if (contentTypeHeaderValue is object)
            {
                headers.Add(KafkaContentTypeAttributeName, Encoding.UTF8.GetBytes(contentTypeHeaderValue));
            }
            return new Message<string, byte[]>
            {
                Headers = headers,
                Value = value,
                Key = key
            };
        }

        private static Headers MapHeaders(CloudEvent cloudEvent, CloudEventFormatter formatter)
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