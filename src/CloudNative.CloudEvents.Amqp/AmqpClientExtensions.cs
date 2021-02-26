// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Amqp;
using Amqp.Framing;
using Amqp.Types;
using System;
using System.IO;
using System.Net.Mime;

namespace CloudNative.CloudEvents.Amqp
{
    public static class AmqpClientExtensions
    {
        internal const string AmqpHeaderPrefix = "cloudEvents:";

        internal const string SpecVersionAmqpHeader = AmqpHeaderPrefix + "specversion";

        public static bool IsCloudEvent(this Message message) =>
            HasCloudEventsContentType(message, out _) ||
            message.ApplicationProperties.Map.ContainsKey(SpecVersionAmqpHeader);        

        public static CloudEvent ToCloudEvent(this Message message,
            CloudEventFormatter formatter,
            params CloudEventAttribute[] extensionAttributes)
        {
            if (HasCloudEventsContentType(message, out var contentType))
            {
                return formatter.DecodeStructuredModeMessage(new MemoryStream((byte[])message.Body), new ContentType(contentType), extensionAttributes);
            }
            else
            {
                var propertyMap = message.ApplicationProperties.Map;
                if (!propertyMap.TryGetValue(SpecVersionAmqpHeader, out var versionId) || !(versionId is string versionIdText))
                {
                    throw new ArgumentException("Request is not a CloudEvent");
                }
                var version = CloudEventsSpecVersion.FromVersionId(versionIdText);
                if (version is null)
                {
                    throw new ArgumentException($"Unsupported CloudEvents spec version '{versionIdText}'");
                }

                var cloudEvent = new CloudEvent(version, extensionAttributes)
                {
                    Data = message.Body,
                    DataContentType = message.Properties.ContentType
                };
                
                foreach (var property in propertyMap)
                {
                    if (!(property.Key is string key && key.StartsWith(AmqpHeaderPrefix)))
                    {
                        continue;
                    }
                    string attributeName = key.Substring(AmqpHeaderPrefix.Length).ToLowerInvariant();
                    
                    // We've already dealt with the spec version.
                    if (attributeName == CloudEventsSpecVersion.SpecVersionAttribute.Name)
                    {
                        continue;
                    }

                    // Timestamps are serialized via DateTime instead of DateTimeOffset.
                    if (property.Value is DateTime dt)
                    {
                        if (dt.Kind != DateTimeKind.Utc)
                        {
                            // This should only happen for MinValue and MaxValue...
                            // just respecify as UTC. (We could add validation that it really
                            // *is* MinValue or MaxValue if we wanted to.)
                            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                        }
                        cloudEvent[attributeName] = (DateTimeOffset)dt;
                    }
                    // URIs are serialized as strings, but we need to convert them back to URIs.
                    // It's simplest to let CloudEvent do this for us.
                    else if (property.Value is string text)
                    {
                        cloudEvent.SetAttributeFromString(attributeName, text);
                    }
                    else
                    {
                        cloudEvent[attributeName] = property.Value;
                    }
                }
                return cloudEvent;
            }
        }

        // TODO: Check that it really is meant to be case-sensitive. (Original code was inconsistent.)
        private static bool HasCloudEventsContentType(Message message, out string contentType)
        {
            contentType = (message.Properties.ContentType as Symbol)?.ToString();
            return contentType?.StartsWith(CloudEvent.MediaType) == true;
        }

        public static Message ToAmqpMessage(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter)
        {
            var applicationProperties = MapHeaders(cloudEvent);
            RestrictedDescribed bodySection;
            Properties properties;

            switch (contentMode)
            {
                case ContentMode.Structured:
                    bodySection = new Data
                    {
                        Binary = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType)
                    };
                    // TODO: What about the other parts of the content type?
                    properties = new Properties { ContentType = contentType.MediaType };
                    break;
                case ContentMode.Binary:
                    bodySection = SerializeData(cloudEvent.Data);
                    properties = new Properties { ContentType = cloudEvent.DataContentType };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contentMode), $"Unsupported content mode: {contentMode}");
            }
            return new Message
            {
                ApplicationProperties = applicationProperties,
                BodySection = bodySection,
                Properties = properties
            };
        }

        private static ApplicationProperties MapHeaders(CloudEvent cloudEvent)
        {
            var applicationProperties = new ApplicationProperties();
            var properties = applicationProperties.Map;
            properties.Add(SpecVersionAmqpHeader, cloudEvent.SpecVersion.VersionId);

            foreach (var pair in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = pair.Key;

                // The content type is specified elsewhere.
                if (attribute == cloudEvent.SpecVersion.DataContentTypeAttribute)
                {
                    continue;
                }

                string propKey = AmqpHeaderPrefix + attribute.Name;

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
            return applicationProperties;
        }

        /// <summary>
        /// Convert data into a suitable format for inclusion in an AMQP record.
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