// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.


namespace CloudNative.CloudEvents.Kafka
{
    using CloudNative.CloudEvents.Extensions;
    using Confluent.Kafka;
    using System;
    using System.Linq;
    using System.Net.Mime;
    using System.Text;

    public static class KafkaClientExtensions
    {
        private static string StructuredContentTypePrefix = "application/cloudevents";
        private const string SpecVersionKafkaHeader1 = KafkaCloudEventMessage.KafkaHeaderPerfix + "cloudEventsVersion";

        private const string SpecVersionKafkaHeader2 = KafkaCloudEventMessage.KafkaHeaderPerfix + "specversion";

        private static JsonEventFormatter _jsonFormatter = new JsonEventFormatter();

        public static bool IsCloudEvent(this Message<string, byte[]> message)
        {
            return message.Headers.Any(x =>
            string.Equals(x.Key, SpecVersionKafkaHeader1, StringComparison.InvariantCultureIgnoreCase)
                || string.Equals(x.Key, SpecVersionKafkaHeader2, StringComparison.InvariantCultureIgnoreCase)
                || (string.Equals(x.Key, KafkaCloudEventMessage.KafkaContentTypeAttributeName, StringComparison.InvariantCultureIgnoreCase)
                    && Encoding.UTF8.GetString(x.GetValueBytes() ?? Array.Empty<byte>()).StartsWith(StructuredContentTypePrefix)));                
        }       

        public static CloudEvent ToCloudEvent(this Message<string, byte[]> message,
            ICloudEventFormatter eventFormatter = null, params ICloudEventExtension[] extensions)
        {
            if (!IsCloudEvent(message))
            {
                throw new InvalidOperationException();
            }

            var contentType = ExtractContentType(message);

            CloudEvent cloudEvent;

            if (!string.IsNullOrEmpty(contentType)
                && contentType.StartsWith(CloudEvent.MediaType, StringComparison.InvariantCultureIgnoreCase))
            {
                // structured mode
                if (eventFormatter == null)
                {
                    if (contentType.EndsWith(JsonEventFormatter.MediaTypeSuffix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        eventFormatter = _jsonFormatter;
                    }
                    else
                    {
                        throw new InvalidOperationException("Not supported CloudEvents media formatter.");
                    }
                }

                cloudEvent = _jsonFormatter.DecodeStructuredEvent(message.Value, extensions);
            }
            else
            {
                // binary mode
                var specVersion = ExtractVersion(message);

                cloudEvent = new CloudEvent(specVersion, extensions);
                var attributes = cloudEvent.GetAttributes();
                var cloudEventHeaders = message.Headers.Where(h => h.Key.StartsWith(KafkaCloudEventMessage.KafkaHeaderPerfix));

                foreach (var header in cloudEventHeaders)
                {
                    if (string.Equals(header.Key, SpecVersionKafkaHeader1, StringComparison.InvariantCultureIgnoreCase)
                        || string.Equals(header.Key, SpecVersionKafkaHeader2, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    var attributeName = header.Key.Substring(KafkaCloudEventMessage.KafkaHeaderPerfix.Length);
                    attributes.Add(attributeName,
                        eventFormatter.DecodeAttribute(specVersion, attributeName, header.GetValueBytes(), extensions));
                }

                cloudEvent.DataContentType = contentType != null ? new ContentType(contentType) : null;
                cloudEvent.Data = message.Value;
            }

            InitPartitioningKey(message, cloudEvent);

            return cloudEvent;
        }

        private static string ExtractContentType(Message<string, byte[]> message)
        {
            var contentTypeHeader = message.Headers.FirstOrDefault(x => string.Equals(x.Key, KafkaCloudEventMessage.KafkaContentTypeAttributeName,
                StringComparison.InvariantCultureIgnoreCase));
            string contentType = null;
            if (contentTypeHeader != null)
            {
                var bytes = contentTypeHeader.GetValueBytes();                
                contentType = Encoding.UTF8.GetString(bytes ?? Array.Empty<byte>());
            }

            return contentType;
        }

        private static void InitPartitioningKey(Message<string, byte[]> message, CloudEvent cloudEvent)
        {
            if (!string.IsNullOrEmpty(message.Key))
            {
                var extension = cloudEvent.Extension<PartitioningExtension>();
                extension.PartitioningKeyValue = message.Key;
            }
        }

        private static CloudEventsSpecVersion ExtractVersion(Message<string, byte[]> message)
        {
            var specVersionHeaders = message.Headers.Where(x => string.Equals(x.Key, SpecVersionKafkaHeader1, StringComparison.InvariantCultureIgnoreCase)
                                || string.Equals(x.Key, SpecVersionKafkaHeader2, StringComparison.InvariantCultureIgnoreCase))
                             .ToDictionary(x => x.Key, x => x, StringComparer.InvariantCultureIgnoreCase);

            var specVersion = CloudEventsSpecVersion.Default;
            if (specVersionHeaders.ContainsKey(SpecVersionKafkaHeader1))
            {
                specVersion = CloudEventsSpecVersion.V0_1;
            }
            else if (specVersionHeaders.ContainsKey(SpecVersionKafkaHeader2))
            {
                var specVersionValue = Encoding.UTF8.GetString(specVersionHeaders[SpecVersionKafkaHeader2].GetValueBytes() ?? Array.Empty<byte>());
                if (specVersionValue == "0.2")
                {
                    specVersion = CloudEventsSpecVersion.V0_2;
                }
                else if (specVersionValue == "0.3")
                {
                    specVersion = CloudEventsSpecVersion.V0_3;
                }
            }

            return specVersion;
        }

        private static (bool isBinaryMode, string contentType) IsBinaryMode(Message<string, object> message)
        {
            var contentTypeHeader = message.Headers.FirstOrDefault(x => string.Equals(x.Key, KafkaCloudEventMessage.KafkaContentTypeAttributeName));
            if (contentTypeHeader != null)
            {
                var value = Encoding.UTF8.GetString(contentTypeHeader.GetValueBytes());
                if (!string.IsNullOrEmpty( value) && value.StartsWith(StructuredContentTypePrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    return (true, value);
                }
            }

            return (false, null);
        }
    }
}