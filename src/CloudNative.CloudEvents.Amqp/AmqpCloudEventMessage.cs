// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Amqp
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using global::Amqp;
    using global::Amqp.Framing;
    using global::Amqp.Types;

    public class AmqpCloudEventMessage : Message
    {
        public AmqpCloudEventMessage(CloudEvent cloudEvent, ContentMode contentMode, ICloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                this.BodySection = new Data
                    { Binary = formatter.EncodeStructuredEvent(cloudEvent, out var contentType) };
                this.Properties = new Properties() { ContentType = contentType.MediaType };
                this.ApplicationProperties = new ApplicationProperties();
                MapHeaders(cloudEvent);
                return;
            }

            if (cloudEvent.Data is byte[])
            {
                this.BodySection = new Data { Binary = (byte[])cloudEvent.Data };
            }
            else if (cloudEvent.Data is Stream)
            {
                if (cloudEvent.Data is MemoryStream)
                {
                    this.BodySection = new Data { Binary = ((MemoryStream)cloudEvent.Data).ToArray() };
                }
                else
                {
                    var buffer = new MemoryStream();
                    ((Stream)cloudEvent.Data).CopyTo(buffer);
                    this.BodySection = new Data { Binary = buffer.ToArray() };
                }
            }
            else if (cloudEvent.Data is string)
            {
                this.BodySection = new AmqpValue() { Value = cloudEvent.Data };
            }

            this.Properties = new Properties() { ContentType = cloudEvent.DataContentType?.MediaType };
            this.ApplicationProperties = new ApplicationProperties();
            MapHeaders(cloudEvent);
        }

        void MapHeaders(CloudEvent cloudEvent)
        {
            foreach (var attribute in cloudEvent.GetAttributes())
            {
                if (!attribute.Key.Equals(CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion)) &&
                    !attribute.Key.Equals(CloudEventAttributes.DataContentTypeAttributeName(cloudEvent.SpecVersion)))
                {
                    string key = "cloudEvents:" + attribute.Key;
                    if (attribute.Value is Uri)
                    {
                        this.ApplicationProperties.Map.Add(key, attribute.Value.ToString());
                    }
                    else if (attribute.Value is DateTimeOffset dto)
                    {
                        // AMQPNetLite doesn't support DateTimeOffset values, so convert to UTC.
                        // That means we can't roundtrip events with non-UTC timestamps, but that's not awful.
                        this.ApplicationProperties.Map.Add(key, dto.UtcDateTime);
                    }
                    else if (attribute.Value is string)
                    {
                        this.ApplicationProperties.Map.Add(key, attribute.Value);
                    }
                    else
                    {
                        Map dict = new Map();
                        foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(attribute.Value))
                        {
                            dict.Add(descriptor.Name, descriptor.GetValue(attribute.Value));
                        }

                        this.ApplicationProperties.Map.Add("cloudEvents:" + attribute.Key, dict);
                    }
                }
            }
        }
    }
}