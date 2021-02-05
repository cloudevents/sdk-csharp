// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Amqp;
using Amqp.Types;
using System;
using System.IO;

namespace CloudNative.CloudEvents.Amqp
{
    public static class AmqpClientExtensions
    {
        internal const string AmqpHeaderPrefix = "cloudEvents:";

        internal const string SpecVersionAmqpHeader = AmqpHeaderPrefix + "specversion";

        public static bool IsCloudEvent(this Message message) =>
            (message.Properties.ContentType is Symbol contentType && contentType.ToString().StartsWith(CloudEvent.MediaType)) ||
            message.ApplicationProperties.Map.ContainsKey(SpecVersionAmqpHeader);        

        public static CloudEvent ToCloudEvent(this Message message,
            ICloudEventFormatter formatter,
            params CloudEventAttribute[] extensionAttributes)
        {
            if (HasCloudEventsContentType(message))
            {
                return formatter.DecodeStructuredEvent(new MemoryStream((byte[])message.Body), extensionAttributes);
            }
            else
            {
                var propertyMap = message.ApplicationProperties.Map;
                CloudEventsSpecVersion version = CloudEventsSpecVersion.Default;
                if (propertyMap.TryGetValue(SpecVersionAmqpHeader, out var versionId) && versionId is string versionIdText)
                {
                    version = CloudEventsSpecVersion.FromVersionId(versionIdText);
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
        private static bool HasCloudEventsContentType(Message message) =>
            message.Properties.ContentType is Symbol contentType && contentType.ToString().StartsWith(CloudEvent.MediaType);
    }
}