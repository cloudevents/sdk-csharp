// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Net.Mime;
    using global::Amqp;
    using global::Amqp.Types;

    public static class AmqpClientExtensions
    {
        const string AmqpHeaderPrefix = "cloudEvents:";

        const string SpecVersionAmqpHeader1 = AmqpHeaderPrefix + "cloudEventsVersion";

        const string SpecVersionAmqpHeader2 = AmqpHeaderPrefix + "specversion";

        static JsonEventFormatter jsonFormatter = new JsonEventFormatter();

        public static bool IsCloudEvent(this Message message)
        {
            return ((message.Properties.ContentType != null &&
                     message.Properties.ContentType.ToString().StartsWith(CloudEvent.MediaType)) ||
                    message.ApplicationProperties.Map.ContainsKey(SpecVersionAmqpHeader1) ||
                    message.ApplicationProperties.Map.ContainsKey(SpecVersionAmqpHeader2));
        }

        public static CloudEvent ToCloudEvent(this Message message,
            params ICloudEventExtension[] extensions)
        {
            return ToCloudEvent(message, null, extensions);
        }

        public static CloudEvent ToCloudEvent(this Message message,
            ICloudEventFormatter formatter = null,
            params ICloudEventExtension[] extensions)
        {
            string contentType = message.Properties.ContentType?.ToString();
            if (contentType != null &&
                contentType.StartsWith(CloudEvent.MediaType, StringComparison.InvariantCultureIgnoreCase))
            {
                // handle structured mode
                if (formatter == null)
                {
                    // if we didn't get a formatter, pick one
                    if (contentType.EndsWith(JsonEventFormatter.MediaTypeSuffix,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        formatter = jsonFormatter;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unsupported CloudEvents encoding");
                    }
                }

                return formatter.DecodeStructuredEvent(new MemoryStream((byte[])message.Body), extensions);
            }
            else
            {
                var specVersion = message.ApplicationProperties.Map.ContainsKey(SpecVersionAmqpHeader1)
                        ? CloudEventsSpecVersion.V0_1
                        : message.ApplicationProperties.Map.ContainsKey(SpecVersionAmqpHeader2)
                            ? (message.ApplicationProperties.Map[SpecVersionAmqpHeader2] as string == "0.2"
                                ? CloudEventsSpecVersion.V0_2 :
                                (message.ApplicationProperties.Map[SpecVersionAmqpHeader2] as string == "0.3"
                                    ? CloudEventsSpecVersion.V0_3
                                : CloudEventsSpecVersion.Default))
                            : CloudEventsSpecVersion.Default;

                var cloudEvent = new CloudEvent(specVersion, extensions);
                var attributes = cloudEvent.GetAttributes();
                foreach (var prop in message.ApplicationProperties.Map)
                {
                    if (prop.Key is string key &&
                        key.StartsWith(AmqpHeaderPrefix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        string attrName = key.Substring(AmqpHeaderPrefix.Length).ToLowerInvariant();
                        if (cloudEvent.SpecVersion != CloudEventsSpecVersion.V1_0 && prop.Value is Map)
                        {
                            IDictionary<string, object> exp = new ExpandoObject();
                            foreach (var props in (Map)prop.Value)
                            {
                                exp[props.Key.ToString()] = props.Value;
                            }

                            attributes[attrName] = exp;
                        }
                        else if (prop.Value is DateTime dt)
                        {
                            if (dt.Kind != DateTimeKind.Utc)
                            {
                                // This should only happen for MinValue and MaxValue...
                                // just respecify as UTC. (We could add validation that it really
                                // *is* MinValue or MaxValue if we wanted to.)
                                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                            }
                            attributes[attrName] = (DateTimeOffset) dt;
                        }
                        else
                        {
                            attributes[attrName] = prop.Value;
                        }
                    }
                }

                cloudEvent.DataContentType = message.Properties.ContentType != null
                    ? new ContentType(message.Properties.ContentType)
                    : null;
                cloudEvent.Data = message.Body;
                return cloudEvent;
            }
        }
    }
}