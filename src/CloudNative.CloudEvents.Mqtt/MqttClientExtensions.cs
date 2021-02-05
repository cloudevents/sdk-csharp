// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using MQTTnet;

namespace CloudNative.CloudEvents.Mqtt
{
    public static class MqttClientExtensions
    {
        public static CloudEvent ToCloudEvent(this MqttApplicationMessage message,
            ICloudEventFormatter eventFormatter, params CloudEventAttribute[] extensionAttributes)
        {
            return eventFormatter.DecodeStructuredEvent(message.Payload, extensionAttributes);
        }
    }
}