// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using MQTTnet;
using System;
using System.Collections.Generic;

namespace CloudNative.CloudEvents.Mqtt
{
    /// <summary>
    /// Extension methods to convert between CloudEvents and MQTT messages.
    /// </summary>
    public static class MqttClientExtensions
    {
        /// <summary>
        /// Converts this MQTT message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The MQTT message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(this MqttApplicationMessage message,
            CloudEventFormatter formatter, params CloudEventAttribute[] extensionAttributes) =>
            ToCloudEvent(message, formatter, (IEnumerable<CloudEventAttribute>) extensionAttributes);

        /// <summary>
        /// Converts this MQTT message into a CloudEvent object.
        /// </summary>
        /// <param name="message">The MQTT message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static CloudEvent ToCloudEvent(this MqttApplicationMessage message,
            CloudEventFormatter formatter, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            message = message ?? throw new ArgumentNullException(nameof(message));
            formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));

            // TODO: Determine if there's a sensible content type we should apply.
            return formatter.DecodeStructuredModeMessage(message.Payload, contentType: null, extensionAttributes);
        }

        // TODO: Update to a newer version of MQTTNet and support both binary and structured mode?
        /// <summary>
        /// Converts a CloudEvent to <see cref="MqttApplicationMessage"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Currently only structured mode is supported.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <param name="topic">The MQTT topic for the message. May be null.</param>
        public static MqttApplicationMessage ToMqttApplicationMessage(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter, string topic)
        {
            cloudEvent = cloudEvent ?? throw new ArgumentNullException(nameof(cloudEvent));
            cloudEvent.ValidateForConversion(nameof(cloudEvent));
            formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));

            switch (contentMode)
            {
                case ContentMode.Structured:
                    return new MqttApplicationMessage
                    {
                        Topic = topic,
                        Payload = formatter.EncodeStructuredModeMessage(cloudEvent, out _)
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(contentMode), $"Unsupported content mode: {contentMode}");
            }
        }
    }
}