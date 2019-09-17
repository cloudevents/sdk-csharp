// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Kafka
{
    using CloudNative.CloudEvents.Extensions;
    using Confluent.Kafka;
    using System;
    using System.IO;
    using System.Text;

    public class KafkaCloudEventMessage : Message<string, byte[]>
    {
        public const string KafkaHeaderPerfix = "ce_";

        public const string KafkaContentTypeAttributeName = "content-type";

        public KafkaCloudEventMessage(CloudEvent cloudEvent, ContentMode contentMode, ICloudEventFormatter formatter)
        {
            if (cloudEvent.Data == null)
            {
                throw new ArgumentNullException(nameof(cloudEvent.Data));
            }

            Headers = new Headers();

            Key = ExtractPartitionKey(cloudEvent);            

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

                Headers.Add(KafkaContentTypeAttributeName, Encoding.UTF8.GetBytes(cloudEvent.DataContentType?.MediaType));                
            }

            MapHeaders(cloudEvent, formatter);
        }

        private void MapHeaders(CloudEvent cloudEvent, ICloudEventFormatter formatter)
        {
            foreach (var attr in cloudEvent.GetAttributes())
            {
                if (string.Equals(attr.Key, CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion))                    
                    || string.Equals(attr.Key, CloudEventAttributes.DataContentTypeAttributeName(cloudEvent.SpecVersion))
                    || string.Equals(attr.Key, PartitioningExtension.PartitioningKeyAttributeName))
                {
                    continue;
                }

                Headers.Add(KafkaHeaderPerfix + attr.Key, 
                    formatter.EncodeAttribute(cloudEvent.SpecVersion, attr.Key, attr.Value, cloudEvent.Extensions.Values));
            }
        }

        protected string ExtractPartitionKey(CloudEvent cloudEvent)
        {
            var extension = cloudEvent.Extension<PartitioningExtension>();

            return extension?.PartitioningKeyValue;
        }
    }
}  