// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using MQTTnet;

namespace CloudNative.CloudEvents.Mqtt
{
    // TODO: Update to a newer version of MQTTNet and support both binary and structured mode?
    public class MqttCloudEventMessage : MqttApplicationMessage
    {
        public MqttCloudEventMessage(CloudEvent cloudEvent, CloudEventFormatter formatter)
        {
            this.Payload = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
        }
    }
}