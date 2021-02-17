// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using MQTTnet;

namespace CloudNative.CloudEvents.Mqtt
{
    public static class MqttClientExtensions
    {
        public static CloudEvent ToCloudEvent(this MqttApplicationMessage message,
            CloudEventFormatter eventFormatter, params CloudEventAttribute[] extensionAttributes)
        {
            // TODO: Determine if there's a sensible content type we should apply.
            return eventFormatter.DecodeStructuredModeMessage(message.Payload, contentType: null, extensionAttributes);
        }
    }
}