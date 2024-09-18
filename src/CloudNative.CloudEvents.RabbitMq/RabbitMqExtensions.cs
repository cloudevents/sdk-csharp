using RabbitMQ.Client;
using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;

namespace CloudNative.CloudEvents.RabbitMq
{
    /// <summary>
    /// Used to convert CloudEvents to and from RabbitMQ messages.
    /// </summary>
    public static class RabbitMqExtensions
    {
        // This is internal in CloudEventsSpecVersion.
        private const string SpecVersionAttributeName = "specversion";

        internal const string AmqpHeaderUnderscorePrefix = "cloudEvents_";
        internal const string AmqpHeaderColonPrefix = "cloudEvents:";

        internal const string SpecVersionAmqpHeaderWithUnderscore = AmqpHeaderUnderscorePrefix + SpecVersionAttributeName;
        internal const string SpecVersionAmqpHeaderWithColon = AmqpHeaderColonPrefix + SpecVersionAttributeName;

        /// <summary>
        /// Indicates whether this <see cref="Message"/> holds a single CloudEvent.
        /// </summary>
        /// <remarks>
        /// This method returns false for batch requests, as they need to be parsed differently.
        /// </remarks>
        /// <param name="message">The message to check for the presence of a CloudEvent. Must not be null.</param>
        /// <returns>true, if the request is a CloudEvent</returns>
        public static bool IsCloudEvent(this Message message) =>
            HasCloudEventsContentType(Validation.CheckNotNull(message, nameof(message)), out _) ||
            message.Properties.Headers.ContainsKey(SpecVersionAmqpHeaderWithUnderscore) || message.Properties.Headers.ContainsKey(SpecVersionAmqpHeaderWithColon);

        /// <summary>
        /// Converts this AMQP message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The AMQP message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(
            this Message message,
            CloudEventFormatter formatter)
        {
            return ToCloudEvent(message, formatter, new CloudEventAttribute[] { });
        }

        /// <summary>
        /// Converts this AMQP message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The AMQP message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(
            this Message message,
            CloudEventFormatter formatter,
            params CloudEventAttribute[] extensionAttributes) =>
            ToCloudEvent(message, formatter, (IEnumerable<CloudEventAttribute>) extensionAttributes);

        /// <summary>
        /// Converts this AMQP message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The AMQP message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(
            this Message message,
            CloudEventFormatter formatter,
            IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            Validation.CheckNotNull(message, nameof(message));
            Validation.CheckNotNull(formatter, nameof(formatter));

            if (HasCloudEventsContentType(message, out var contentType))
            {
                return formatter.DecodeStructuredModeMessage(new MemoryStream(message.Body.ToArray()), new ContentType(contentType), extensionAttributes);
            }
            else
            {
                var propertyMap = message.Properties.Headers;
                if (!propertyMap.TryGetValue(SpecVersionAmqpHeaderWithUnderscore, out var versionId) &&
                    !propertyMap.TryGetValue(SpecVersionAmqpHeaderWithColon, out versionId))
                {
                    throw new ArgumentException("Request is not a CloudEvent");
                }

                var version = CloudEventsSpecVersion.FromVersionId(versionId as string)
                    ?? throw new ArgumentException($"Unknown CloudEvents spec version '{versionId}'", nameof(message));

                var cloudEvent = new CloudEvent(version, extensionAttributes)
                {
                    DataContentType = message.Properties.ContentType
                };

                foreach (var property in propertyMap)
                {
                    if (!(property.Key is string key &&
                        (key.StartsWith(AmqpHeaderColonPrefix) || key.StartsWith(AmqpHeaderUnderscorePrefix))))
                    {
                        continue;
                    }
                    // Note: both prefixes have the same length. If we ever need any prefixes with a different length, we'll need to know which
                    // prefix we're looking at.
                    string attributeName = key.Substring(AmqpHeaderUnderscorePrefix.Length).ToLowerInvariant();

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
                        cloudEvent[attributeName] = (DateTimeOffset) dt;
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
                // Populate the data after the rest of the CloudEvent
                formatter.DecodeBinaryModeEventData(message.Body, cloudEvent);

                return Validation.CheckCloudEventArgument(cloudEvent, nameof(message));
            }
        }

        private static bool HasCloudEventsContentType(Message message, out string contentType)
        {
            contentType = message.Properties.ContentType;
            return MimeUtilities.IsCloudEventsContentType(contentType);
        }

        /// <summary>
        /// Converts a CloudEvent to <see cref="Message"/> using the default property prefix. Versions released prior to March 2023
        /// use a default property prefix of "cloudEvents:". Versions released from March 2023 onwards use a property prefix of "cloudEvents_".
        /// Code wishing to express the prefix explicitly should use <see cref="ToRabbitMqMessageWithColonPrefix(CloudEvent, ContentMode, CloudEventFormatter,IBasicProperties)"/> or
        /// <see cref="ToRabbitMqMessageWithUnderscorePrefix(CloudEvent, ContentMode, CloudEventFormatter,IBasicProperties)"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <param name="basicProperties">The message properties used by RabbitMQ</param>
        public static Message ToRabbitMqMessage(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter, IBasicProperties basicProperties) =>
            ToRabbitMqMessage(cloudEvent, contentMode, formatter, AmqpHeaderColonPrefix,basicProperties);

        /// <summary>
        /// Converts a CloudEvent to <see cref="Message"/> using a property prefix of "cloudEvents_". This prefix was introduced as the preferred
        /// prefix for the AMQP binding in August 2022.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <param name="basicProperties">The message properties used by RabbitMQ</param>
        public static Message ToRabbitMqMessageWithUnderscorePrefix(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter, IBasicProperties basicProperties) =>
            ToRabbitMqMessage(cloudEvent, contentMode, formatter, AmqpHeaderUnderscorePrefix,basicProperties);

        /// <summary>
        /// Converts a CloudEvent to <see cref="Message"/> using a property prefix of "cloudEvents:". This prefix
        /// is a legacy retained only for compatibility purposes; it can't be used by JMS due to constraints in JMS property names.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <param name="basicProperties">The message properties used by RabbitMQ</param>
        public static Message ToRabbitMqMessageWithColonPrefix(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter, IBasicProperties basicProperties) =>
            ToRabbitMqMessage(cloudEvent, contentMode, formatter, AmqpHeaderColonPrefix,basicProperties);

        private static Message ToRabbitMqMessage(CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter, string prefix, IBasicProperties basicProperties)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
            Validation.CheckNotNull(formatter, nameof(formatter));

            MapHeaders(cloudEvent, prefix,basicProperties);

           byte[] data;
            switch (contentMode)
            {
                case ContentMode.Structured:
                    var structuredData = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
                    data = BinaryDataUtilities.AsArray(structuredData);
                    basicProperties.ContentType = contentType.MediaType;
                    break;
                case ContentMode.Binary:
                    var binaryData = formatter.EncodeBinaryModeEventData(cloudEvent);
                    data = BinaryDataUtilities.AsArray(binaryData);
                    basicProperties.ContentType = formatter.GetOrInferDataContentType(cloudEvent);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contentMode), $"Unsupported content mode: {contentMode}");
            }

            return new Message(basicProperties, data);
        }

        private static void MapHeaders(CloudEvent cloudEvent, string prefix, IBasicProperties basicProperties)
        {
            var properties = basicProperties.Headers;
            if (properties == null)
            {
                properties = new Dictionary<string, object>();
                basicProperties.Headers = properties;
            }
            var specVersion = cloudEvent.SpecVersion;
            var versionId = specVersion.VersionId;
            properties.Add(prefix + SpecVersionAttributeName, versionId);

            foreach (var pair in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = pair.Key;

                // The content type is specified elsewhere.
                if (attribute == cloudEvent.SpecVersion.DataContentTypeAttribute)
                {
                    continue;
                }

                var propKey = prefix + attribute.Name;
                object propValue;

                // TODO: Check that AMQP can handle byte[], bool and int values
                switch (pair.Value)
                {
                    case Uri uri:
                        propValue = uri.ToString();
                        break;
                    case DateTimeOffset dto:
                        // https://en.cppreference.com/w/c/chrono/time_t
                        // Although not defined by the C standard, this is almost always an integral value holding the number of seconds
                        propValue = new AmqpTimestamp(dto.ToUnixTimeSeconds());
                        break;
                    default:
                        propValue = pair.Value;
                        break;
                }

                properties.Add(propKey, propValue);
            }
        }
    }
}
