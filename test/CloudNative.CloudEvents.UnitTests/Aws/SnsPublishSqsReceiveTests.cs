// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using CloudNative.CloudEvents.AwsSns;
using CloudNative.CloudEvents.AwsSqs;
using CloudNative.CloudEvents.NewtonsoftJson;
using Xunit;
using MessageAttributeValueSns = Amazon.SimpleNotificationService.Model.MessageAttributeValue;
using MessageAttributeValueSqs = Amazon.SQS.Model.MessageAttributeValue;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.UnitTests.Aws
{
    public class SnsPublishSqsReceiveTests
    {
        [Theory]
        [InlineData(ContentMode.Structured)]
        [InlineData(ContentMode.Binary)]
        public void ShouldProperlyEncodeAndDecode(ContentMode contentMode)
        {
            // Given 
            var jsonEventFormatter = new JsonEventFormatter();

            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull"),
                Subject = "123",
                Id = "A234-1234-1234",
                Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero),
                DataContentType = MediaTypeNames.Text.Xml,
                Data = "<much wow=\"xml\"/>",
                ["comexampleextension1"] = "value"
            };

            // When
            var publishRequest = cloudEvent.ToSnsPublishRequest(contentMode, jsonEventFormatter);

            // Then
            var response = SimulateMessageTransport(publishRequest);

            var receivedCloudEvent = response.ToCloudEvents(jsonEventFormatter).Single();

            AssertCloudEventsEqual(cloudEvent, receivedCloudEvent);
        }

        private static ReceiveMessageResponse SimulateMessageTransport(PublishRequest publishRequest)
        {
            // Using serialization to create fully independent copy thus simulating message transport
            // real transport will work in a similar way
            var publishRequestCopy = DeepCopy(publishRequest);

            return new ReceiveMessageResponse
            {
                Messages = new List<Message>
                {
                    new Message
                    {
                        Body = publishRequestCopy.Message,
                        MessageAttributes = publishRequestCopy.MessageAttributes.ToDictionary(
                            c => c.Key,
                            v => new MessageAttributeValueSqs
                            {
                                DataType = v.Value.DataType,
                                StringValue = v.Value.StringValue
                            })
                    }
                }
            };
        }
    }
}