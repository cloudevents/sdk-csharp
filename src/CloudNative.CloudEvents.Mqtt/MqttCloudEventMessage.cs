// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Mqtt
{
    using MQTTnet;

    public class MqttCloudEventMessage : MqttApplicationMessage
    {
        public MqttCloudEventMessage(CloudEvent cloudEvent, ICloudEventFormatter formatter)
        {
            this.Payload = formatter.EncodeStructuredEvent(cloudEvent, out var contentType);
        }
    }
}