// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using MQTTnet;
using MQTTnet.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents.Mqtt
{
    /// <summary>
    /// Extension methods to convert between CloudEvents and MQTT messages.
    /// </summary>
    public static class MqttExtensions
    {
        /// <summary>
        /// Converts this MQTT message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The MQTT message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(this MqttApplicationMessage message,
            CloudEventFormatter formatter, params CloudEventAttribute[]? extensionAttributes) =>
            ToCloudEvent(message, formatter, (IEnumerable<CloudEventAttribute>?) extensionAttributes);

        /// <summary>
        /// Converts this MQTT message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The MQTT message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(this MqttApplicationMessage message,
            CloudEventFormatter formatter, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(formatter, nameof(formatter));
            Validation.CheckNotNull(message, nameof(message));

            // Check if the spec version is specified in user properties.
            // If it is, we'll assume it's binary mode. Otherwise, we'll assume it's structured mode.
            if (message.UserProperties?.FirstOrDefault(p => p.Name == CloudEventsSpecVersion.SpecVersionAttribute.Name)
                is not MqttUserProperty specVersionProperty)
            {
                // TODO: Determine if there's a sensible content type we should apply.
                return formatter.DecodeStructuredModeMessage(message.PayloadSegment, contentType: null, extensionAttributes);
            }

            var specVersion = CloudEventsSpecVersion.FromVersionId(specVersionProperty.Value)
                ?? throw new ArgumentException($"Unknown CloudEvents spec version '{specVersionProperty.Value}'", nameof(message));
            var cloudEvent = new CloudEvent(specVersion, extensionAttributes);

            foreach (var userProperty in message.UserProperties)
            {
                if (userProperty == specVersionProperty)
                {
                    continue;
                }
                cloudEvent.SetAttributeFromString(userProperty.Name, userProperty.Value);
            }

            if (message.PayloadSegment.Array is not null)
            {
                formatter.DecodeBinaryModeEventData(message.PayloadSegment, cloudEvent);
            }
            return cloudEvent;
        }

        /// <summary>
        /// Converts a CloudEvent to <see cref="MqttApplicationMessage"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Both structured mode and binary mode are supported.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <param name="topic">The MQTT topic for the message. May be null.</param>
        public static MqttApplicationMessage ToMqttApplicationMessage(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter, string? topic)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
            Validation.CheckNotNull(formatter, nameof(formatter));

            switch (contentMode)
            {
                case ContentMode.Structured:
                    var arraySegment = BinaryDataUtilities.GetArraySegment(formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType));
                    return new MqttApplicationMessage
                    {
                        Topic = topic,
                        ContentType = contentType.ToString(),
                        PayloadSegment = arraySegment
                    };
                default:
                    return new MqttApplicationMessage
                    {
                        ContentType = formatter.GetOrInferDataContentType(cloudEvent),
                        UserProperties = cloudEvent.GetPopulatedAttributes()
                            .Select(pair => new MqttUserProperty(pair.Key.Name, pair.Key.Format(pair.Value)))
                            .Append(new MqttUserProperty(CloudEventsSpecVersion.SpecVersionAttribute.Name, cloudEvent.SpecVersion.VersionId))
                            .ToList(),
                        Topic = topic,
                        PayloadSegment = BinaryDataUtilities.GetArraySegment(formatter.EncodeBinaryModeEventData(cloudEvent))
                    };
            }
        }
    }
}
