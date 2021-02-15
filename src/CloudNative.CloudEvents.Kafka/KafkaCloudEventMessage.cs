// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Extensions;
using Confluent.Kafka;
using System;
using System.IO;
using System.Text;

namespace CloudNative.CloudEvents.Kafka
{
    // TODO: avoid the inheritance here? Constructors are somewhat constricting...
    public class KafkaCloudEventMessage : Message<string, byte[]>
    {
        internal const string KafkaHeaderPrefix = "ce_";

        internal const string KafkaContentTypeAttributeName = "content-type";
        internal const string SpecVersionKafkaHeader = KafkaHeaderPrefix + "specversion";

        public KafkaCloudEventMessage(CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter)
        {
            // TODO: Is this appropriate? Why can't we transport a CloudEvent without data in Kafka?
            if (cloudEvent.Data == null)
            {
                throw new ArgumentNullException(nameof(cloudEvent.Data));
            }

            Headers = new Headers
            {
                {  SpecVersionKafkaHeader, Encoding.UTF8.GetBytes(cloudEvent.SpecVersion.VersionId) }
            };
            Key = (string) cloudEvent[Partitioning.PartitionKeyAttribute];

            if (contentMode == ContentMode.Structured)
            {
                Value = formatter.EncodeStructuredEvent(cloudEvent, out var contentType);
                Headers.Add(KafkaContentTypeAttributeName, Encoding.UTF8.GetBytes(contentType.MediaType));
            }
            else
            {
                if (cloudEvent.Data is byte[] byteData)
                {
                    Value = byteData;
                }
                else if (cloudEvent.Data is Stream dataStream)
                {
                    // TODO: Extract this common code somewhere
                    if (dataStream is MemoryStream dataMemoryStream)
                    {
                        Value = dataMemoryStream.ToArray();
                    }
                    else
                    {
                        var buffer = new MemoryStream();
                        dataStream.CopyTo(buffer);
                        Value = buffer.ToArray();
                    }
                }
                else
                {
                    throw new InvalidOperationException($"{cloudEvent.Data.GetType()} type is not supported for Cloud Event's Value.");
                }
                if (cloudEvent.DataContentType is string dataContentType)
                {
                    Headers.Add(KafkaContentTypeAttributeName, Encoding.UTF8.GetBytes(dataContentType));
                }
            }

            MapHeaders(cloudEvent, formatter);
        }

        private void MapHeaders(CloudEvent cloudEvent, CloudEventFormatter formatter)
        {

            foreach (var pair in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = pair.Key;
                if (attribute == cloudEvent.SpecVersion.DataContentTypeAttribute ||
                    attribute.Name == Partitioning.PartitionKeyAttribute.Name)
                {
                    continue;
                }
                var value = attribute.Format(pair.Value);
                Headers.Add(KafkaHeaderPrefix + attribute.Name, Encoding.UTF8.GetBytes(value));
            }
        }

    }
}  