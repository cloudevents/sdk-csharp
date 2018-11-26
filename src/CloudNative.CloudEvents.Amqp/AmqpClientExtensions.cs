// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Amqp
{
    using System;
    using System.IO;
    using System.Net.Mime;
    using System.Threading.Tasks;
    using global::Amqp;

    public static class AmqpClientExtensions
    {
        const string AmqpHeaderPrefix = "cloudEvents:";

        const string SpecVersionAmqpHeader = AmqpHeaderPrefix + "specversion";

        static JsonEventFormatter jsonFormatter = new JsonEventFormatter();

        public static bool IsCloudEvent(this Message message)
        {                                         
            return ((message.Properties.ContentType != null &&
                     message.Properties.ContentType.ToString().StartsWith(CloudEvent.MediaType)) ||
                    message.ApplicationProperties.Map.ContainsKey(SpecVersionAmqpHeader));
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
                var cloudEvent = new CloudEvent(extensions);
                var attributes = cloudEvent.GetAttributes();
                foreach (var prop in message.ApplicationProperties.Map)
                {
                    if (prop.Key is string &&
                        ((string)prop.Key).StartsWith(AmqpHeaderPrefix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        attributes[((string)prop.Key).Substring(AmqpHeaderPrefix.Length).ToLowerInvariant()] =
                            prop.Value;
                    }
                }

                cloudEvent.ContentType = message.Properties.ContentType != null
                    ? new ContentType(message.Properties.ContentType)
                    : null;
                cloudEvent.Data = message.Body;
                return cloudEvent;
            }
        }
    }
}