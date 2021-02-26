// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Extensions;
using Confluent.Kafka;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;

namespace CloudNative.CloudEvents.Kafka
{
    public static class KafkaClientExtensions
    {
        private const string KafkaHeaderPrefix = "ce_";

        private const string KafkaContentTypeAttributeName = "content-type";
        private const string SpecVersionKafkaHeader = KafkaHeaderPrefix + "specversion";

        // TODO: Avoid all the byte[] -> string conversions? If we didn't care about case-sensitivity, we could prepare byte arrays to perform comparisons with.

        public static bool IsCloudEvent(this Message<string, byte[]> message) =>
            GetHeaderValue(message, SpecVersionKafkaHeader) is object ||
            (ExtractContentType(message)?.StartsWith(CloudEvent.MediaType, StringComparison.InvariantCultureIgnoreCase) == true);

        public static CloudEvent ToCloudEvent(this Message<string, byte[]> message,
            CloudEventFormatter eventFormatter, params CloudEventAttribute[] extensionAttributes) =>
            ToCloudEvent(message, eventFormatter, (IEnumerable<CloudEventAttribute>) extensionAttributes);

        public static CloudEvent ToCloudEvent(this Message<string, byte[]> message,
            CloudEventFormatter eventFormatter, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            if (!IsCloudEvent(message))
            {
                throw new InvalidOperationException();
            }

            var contentType = ExtractContentType(message);

            CloudEvent cloudEvent;

            // Structured mode
            if (contentType?.StartsWith(CloudEvent.MediaType, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                cloudEvent = eventFormatter.DecodeStructuredModeMessage(message.Value, new ContentType(contentType), extensionAttributes);
            }
            else
            {
                // Binary mode
                if (!(GetHeaderValue(message, SpecVersionKafkaHeader) is byte[] versionIdBytes))
                {
                    throw new ArgumentException("Request is not a CloudEvent");
                }
                string versionId = Encoding.UTF8.GetString(versionIdBytes);
                CloudEventsSpecVersion version = CloudEventsSpecVersion.FromVersionId(versionId);
                if (version is null)
                {
                    throw new ArgumentException($"Unsupported CloudEvents spec version '{versionId}'");
                }

                cloudEvent = new CloudEvent(version, extensionAttributes)
                {
                    Data = message.Value,
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
            }

            InitPartitioningKey(message, cloudEvent);
            return cloudEvent;
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

        public static Message<string, byte[]> ToKafkaMessage(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter)
        {
            // TODO: Is this appropriate? Why can't we transport a CloudEvent without data in Kafka?
            if (cloudEvent.Data == null)
            {
                throw new ArgumentNullException(nameof(cloudEvent.Data));
            }
            var headers = MapHeaders(cloudEvent, formatter);
            string key = (string) cloudEvent[Partitioning.PartitionKeyAttribute];
            byte[] value;
            string contentTypeHeaderValue = null;

            if (contentMode == ContentMode.Structured)
            {
                value = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
                // TODO: What about the non-media type parts?
                contentTypeHeaderValue = contentType.MediaType;
            }
            else
            {
                if (cloudEvent.Data is byte[] byteData)
                {
                    value = byteData;
                }
                else if (cloudEvent.Data is Stream dataStream)
                {
                    // TODO: Extract this common code somewhere, or use shared source to access BinaryDataUtilities.
                    if (dataStream is MemoryStream dataMemoryStream)
                    {
                        value = dataMemoryStream.ToArray();
                    }
                    else
                    {
                        var buffer = new MemoryStream();
                        dataStream.CopyTo(buffer);
                        value = buffer.ToArray();
                    }
                }
                else
                {
                    throw new InvalidOperationException($"{cloudEvent.Data.GetType()} type is not supported for Cloud Event's Value.");
                }
                if (cloudEvent.DataContentType is string dataContentType)
                {
                    contentTypeHeaderValue = dataContentType;                    
                }
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