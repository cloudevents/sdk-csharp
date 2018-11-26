// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Amqp
{
    using System.IO;
    using global::Amqp;
    using global::Amqp.Framing;

    public class AmqpCloudEventMessage : Message
    {
        public AmqpCloudEventMessage(CloudEvent cloudEvent, ContentMode contentMode, ICloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                this.BodySection = new Data
                    { Binary = formatter.EncodeStructuredEvent(cloudEvent, out var contentType) };
                this.Properties.ContentType = contentType.MediaType;
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

            this.Properties.ContentType = cloudEvent.ContentType?.MediaType;
            MapHeaders(cloudEvent);
        }

        void MapHeaders(CloudEvent cloudEvent)
        {
            foreach (var attribute in cloudEvent.GetAttributes())
            {
                if (attribute.Key != CloudEventAttributes.ContentTypeAttributeName &&
                    attribute.Key != CloudEventAttributes.DataAttributeName)
                {
                    this.ApplicationProperties.Map.Add("cloudEvents:" + attribute.Key, attribute.Value);
                }
            }
        }
    }
}