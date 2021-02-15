// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Amqp;
using Amqp.Framing;
using Amqp.Types;
using System;
using System.IO;

// TODO: Avoid the use of inheritance here? We're not really adding anything.
namespace CloudNative.CloudEvents.Amqp
{
    public class AmqpCloudEventMessage : Message
    {
        public AmqpCloudEventMessage(CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter)
        {
            ApplicationProperties = new ApplicationProperties();
            MapHeaders(cloudEvent);

            if (contentMode == ContentMode.Structured)
            {
                BodySection = new Data
                {
                    Binary = formatter.EncodeStructuredEvent(cloudEvent, out var contentType)
                };
                Properties = new Properties { ContentType = contentType.MediaType };
                ApplicationProperties = new ApplicationProperties();
                MapHeaders(cloudEvent);
                return;
            }
            else
            {
                BodySection = SerializeData(cloudEvent.Data);
                Properties = new Properties { ContentType = cloudEvent.DataContentType };
            }
        }

        private void MapHeaders(CloudEvent cloudEvent)
        {
            var properties = ApplicationProperties.Map;
            properties.Add(AmqpClientExtensions.SpecVersionAmqpHeader, cloudEvent.SpecVersion.VersionId);
            
            foreach (var pair in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = pair.Key;

                // The content type is specified elsewhere.
                if (attribute == cloudEvent.SpecVersion.DataContentTypeAttribute)
                {
                    continue;
                }

                string propKey = AmqpClientExtensions.AmqpHeaderPrefix + attribute.Name;

                // TODO: Check that AMQP can handle byte[], bool and int values
                object propValue = pair.Value switch
                {
                    Uri uri => uri.ToString(),
                    // AMQPNetLite doesn't support DateTimeOffset values, so convert to UTC.
                    // That means we can't roundtrip events with non-UTC timestamps, but that's not awful.
                    DateTimeOffset dto => dto.UtcDateTime,
                    _ => pair.Value
                };
                properties.Add(propKey, propValue);
            }
        }

        /// <summary>
        /// Convert data into a suitable format for inclusion in an Avro record.
        /// TODO: Asynchronous version?
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static RestrictedDescribed SerializeData(object data)
        {
            switch (data)
            {
                case null:
                    return null;
                case byte[] bytes:
                    return new Data { Binary = bytes };
                case MemoryStream memoryStream:
                    // Note: this will return the whole stream, regardless of position...
                    return new Data { Binary = memoryStream.ToArray() };
                case Stream stream:
                    var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    return new Data { Binary = buffer.ToArray() };
                case string text:
                    return new AmqpValue { Value = text };
                default:
                    throw new ArgumentException($"Unsupported type for AMQP data: {data.GetType()}");
            }
        }
    }
}