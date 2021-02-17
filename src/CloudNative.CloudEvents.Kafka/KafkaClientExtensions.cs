// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Extensions;
using Confluent.Kafka;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CloudNative.CloudEvents.Kafka
{
    public static class KafkaClientExtensions
    {
        // TODO: Avoid all the byte[] -> string conversions? If we didn't care about case-sensitivity, we could prepare byte arrays to perform comparisons with.

        public static bool IsCloudEvent(this Message<string, byte[]> message) =>
            GetHeaderValue(message, KafkaCloudEventMessage.SpecVersionKafkaHeader) is object ||
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
                cloudEvent = eventFormatter.DecodeStructuredEvent(message.Value, extensionAttributes);
            }
            else
            {
                // Binary mode
                if (!(GetHeaderValue(message, KafkaCloudEventMessage.SpecVersionKafkaHeader) is byte[] versionIdBytes))
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
                foreach (var header in message.Headers.Where(h => h.Key.StartsWith(KafkaCloudEventMessage.KafkaHeaderPrefix)))
                {
                    var attributeName = header.Key.Substring(KafkaCloudEventMessage.KafkaHeaderPrefix.Length).ToLowerInvariant();
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
            var headerValue = GetHeaderValue(message, KafkaCloudEventMessage.KafkaContentTypeAttributeName);
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
    }
}