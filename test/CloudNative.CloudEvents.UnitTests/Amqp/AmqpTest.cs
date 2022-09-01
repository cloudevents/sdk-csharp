// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Amqp;
using Amqp.Framing;
using CloudNative.CloudEvents.NewtonsoftJson;
using System;
using System.Net.Mime;
using System.Text;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.Amqp.UnitTests
{
    public class AmqpTest
    {
        [Fact]
        public void AmqpStructuredMessageTest()
        {
            // The AMQPNetLite library is factored such that we don't need to do a wire test here.
            var cloudEvent = CreateSampleCloudEvent();
            var message = cloudEvent.ToAmqpMessage(ContentMode.Structured, new JsonEventFormatter());
            Assert.True(message.IsCloudEvent());
            AssertDecodeThenEqual(cloudEvent, message);
        }

        [Fact]
        public void AmqpBinaryMessageTest()
        {
            // The AMQPNetLite library is factored such that we don't need to do a wire test here.
            var cloudEvent = CreateSampleCloudEvent();
            var message = cloudEvent.ToAmqpMessage(ContentMode.Binary, new JsonEventFormatter());            
            Assert.True(message.IsCloudEvent());
            var encodedAmqpMessage = message.Encode();

            var message1 = Message.Decode(encodedAmqpMessage);
            Assert.True(message1.IsCloudEvent());
            var receivedCloudEvent = message1.ToCloudEvent(new JsonEventFormatter());

            AssertCloudEventsEqual(cloudEvent, receivedCloudEvent);
        }

        [Fact]
        public void BinaryMode_ContentTypeCanBeInferredByFormatter()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "plain text"
            }.PopulateRequiredAttributes();

            var message = cloudEvent.ToAmqpMessage(ContentMode.Binary, new JsonEventFormatter());
            Assert.Equal("application/json", message.Properties.ContentType);
        }

        [Fact]
        public void AmqpNormalizesTimestampsToUtc()
        {
            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull/123"),
                Id = "A234-1234-1234",
                // 2018-04-05T18:31:00+01:00 => 2018-04-05T17:31:00Z
                Time = new DateTimeOffset(2018, 4, 5, 18, 31, 0, TimeSpan.FromHours(1))
            };

            var message = cloudEvent.ToAmqpMessage(ContentMode.Binary, new JsonEventFormatter());
            var encodedAmqpMessage = message.Encode();

            var message1 = Message.Decode(encodedAmqpMessage);
            var receivedCloudEvent = message1.ToCloudEvent(new JsonEventFormatter());

            AssertTimestampsEqual("2018-04-05T17:31:00Z", receivedCloudEvent.Time!.Value);
        }

        [Fact]
        public void EncodeTextDataInBinaryMode_PopulatesDataProperty()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.DataContentType = "text/plain";
            cloudEvent.Data = "some text";

            var message = cloudEvent.ToAmqpMessage(ContentMode.Binary, new JsonEventFormatter());
            var body = Assert.IsType<Data>(message.BodySection);
            var text = Encoding.UTF8.GetString(body.Binary);
            Assert.Equal("some text", text);
        }

        [Fact]
        public void DefaultPrefix()
        {
            var cloudEvent = CreateSampleCloudEvent();

            var message = cloudEvent.ToAmqpMessage(ContentMode.Binary, new JsonEventFormatter());
            Assert.Equal(cloudEvent.Id, message.ApplicationProperties["cloudEvents:id"]);
            AssertDecodeThenEqual(cloudEvent, message);
        }

        [Fact]
        public void UnderscorePrefix()
        {
            var cloudEvent = CreateSampleCloudEvent();
            var message = cloudEvent.ToAmqpMessageWithUnderscorePrefix(ContentMode.Binary, new JsonEventFormatter());
            Assert.Equal(cloudEvent.Id, message.ApplicationProperties["cloudEvents_id"]);
            AssertDecodeThenEqual(cloudEvent, message);
        }

        [Fact]
        public void ColonPrefix()
        {
            var cloudEvent = CreateSampleCloudEvent();
            var message = cloudEvent.ToAmqpMessageWithColonPrefix(ContentMode.Binary, new JsonEventFormatter());
            Assert.Equal(cloudEvent.Id, message.ApplicationProperties["cloudEvents:id"]);
            AssertDecodeThenEqual(cloudEvent, message);
        }

        private void AssertDecodeThenEqual(CloudEvent cloudEvent, Message message)
        {
            var encodedAmqpMessage = message.Encode();

            var message1 = Message.Decode(encodedAmqpMessage);
            var receivedCloudEvent = message1.ToCloudEvent(new JsonEventFormatter());
            AssertCloudEventsEqual(cloudEvent, receivedCloudEvent);
        }

        /// <summary>
        /// Returns a CloudEvent with XML data and an extension.
        /// </summary>
        private static CloudEvent CreateSampleCloudEvent() => new CloudEvent
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
    }
}