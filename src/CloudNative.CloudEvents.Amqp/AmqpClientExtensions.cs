// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Amqp;
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
    }
}