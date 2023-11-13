﻿// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Amqp;
using Amqp.Framing;
using Amqp.Types;
using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Mime;

namespace CloudNative.CloudEvents.Amqp
{
    /// <summary>
    /// Extension methods to convert between CloudEvents and AMQP messages.
    /// </summary>
    public static class AmqpExtensions
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
            message.ApplicationProperties.Map.ContainsKey(SpecVersionAmqpHeaderWithUnderscore) ||
            message.ApplicationProperties.Map.ContainsKey(SpecVersionAmqpHeaderWithColon);

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
            params CloudEventAttribute[]? extensionAttributes) =>
            ToCloudEvent(message, formatter, (IEnumerable<CloudEventAttribute>?) extensionAttributes);

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
            IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(message, nameof(message));
            Validation.CheckNotNull(formatter, nameof(formatter));

            if (HasCloudEventsContentType(message, out var contentType))
            {
                return formatter.DecodeStructuredModeMessage(new MemoryStream((byte[]) message.Body), new ContentType(contentType), extensionAttributes);
            }
            else
            {
                var propertyMap = message.ApplicationProperties.Map;
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
                if (message.BodySection is Data data)
                {
                    // Note: Fetching the Binary property will always retrieve the data. It will
                    // be copied from the Buffer property if necessary.
                    formatter.DecodeBinaryModeEventData(data.Binary, cloudEvent);
                }
                else if (message.BodySection is object)
                {
                    throw new ArgumentException("Binary mode data in AMQP message must be in the application data section");
                }

                return Validation.CheckCloudEventArgument(cloudEvent, nameof(message));
            }
        }

        private static bool HasCloudEventsContentType(Message message, [NotNullWhen(true)] out string? contentType)
        {
            contentType = message.Properties.ContentType?.ToString();
            return MimeUtilities.IsCloudEventsContentType(contentType);
        }

        /// <summary>
        /// Converts a CloudEvent to <see cref="Message"/> using the default property prefix. Versions released prior to March 2023
        /// use a default property prefix of "cloudEvents:". Versions released from March 2023 onwards use a property prefix of "cloudEvents_".
        /// Code wishing to express the prefix explicitly should use <see cref="ToAmqpMessageWithColonPrefix(CloudEvent, ContentMode, CloudEventFormatter)"/> or
        /// <see cref="ToAmqpMessageWithUnderscorePrefix(CloudEvent, ContentMode, CloudEventFormatter)"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        public static Message ToAmqpMessage(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter) =>
            ToAmqpMessage(cloudEvent, contentMode, formatter, AmqpHeaderColonPrefix);

        /// <summary>
        /// Converts a CloudEvent to <see cref="Message"/> using a property prefix of "cloudEvents_". This prefix was introduced as the preferred
        /// prefix for the AMQP binding in August 2022.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        public static Message ToAmqpMessageWithUnderscorePrefix(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter) =>
            ToAmqpMessage(cloudEvent, contentMode, formatter, AmqpHeaderUnderscorePrefix);

        /// <summary>
        /// Converts a CloudEvent to <see cref="Message"/> using a property prefix of "cloudEvents:". This prefix
        /// is a legacy retained only for compatibility purposes; it can't be used by JMS due to constraints in JMS property names.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        public static Message ToAmqpMessageWithColonPrefix(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter) =>
            ToAmqpMessage(cloudEvent, contentMode, formatter, AmqpHeaderColonPrefix);

        private static Message ToAmqpMessage(CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter, string prefix)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
            Validation.CheckNotNull(formatter, nameof(formatter));

            var applicationProperties = MapHeaders(cloudEvent, prefix);
            RestrictedDescribed bodySection;
            Properties properties;

            switch (contentMode)
            {
                case ContentMode.Structured:
                    bodySection = new Data
                    {
                        Binary = BinaryDataUtilities.AsArray(formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType))
                    };
                    // TODO: What about the other parts of the content type?
                    properties = new Properties { ContentType = contentType.MediaType };
                    break;
                case ContentMode.Binary:
                    bodySection = new Data { Binary = BinaryDataUtilities.AsArray(formatter.EncodeBinaryModeEventData(cloudEvent)) };
                    properties = new Properties { ContentType = formatter.GetOrInferDataContentType(cloudEvent) };
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

        private static ApplicationProperties MapHeaders(CloudEvent cloudEvent, string prefix)
        {
            var applicationProperties = new ApplicationProperties();
            var properties = applicationProperties.Map;
            properties.Add(prefix + SpecVersionAttributeName, cloudEvent.SpecVersion.VersionId);

            foreach (var pair in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = pair.Key;

                // The content type is specified elsewhere.
                if (attribute == cloudEvent.SpecVersion.DataContentTypeAttribute)
                {
                    continue;
                }

                string propKey = prefix + attribute.Name;

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
    }
}

#if NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>Specifies that when a method returns <see cref="ReturnValue"/>, the parameter will not be null even if the corresponding type allows it.</summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        /// <summary>Initializes the attribute with the specified return value condition.</summary>
        /// <param name="returnValue">
        /// The return value condition. If the method returns this value, the associated parameter will not be null.
        /// </param>
        public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        /// <summary>Gets the return value condition.</summary>
        public bool ReturnValue { get; }
    }
}
#endif