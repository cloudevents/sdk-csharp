// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.UnitTests
{
    using System;
    using System.Net.Mime;
    using CloudNative.CloudEvents.Amqp;
    using global::Amqp;
    using Xunit;

    public class AmqpTest
    {
        [Fact]
        public void AmqpStructuredMessageTest()
        {
            // the AMQPNetLite library is factored such
            // that we don't need to do a wire test here
            

            var jsonEventFormatter = new JsonEventFormatter();
            var cloudEvent = new CloudEvent("com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"))
            {
                Id = "A234-1234-1234",
                Time = new DateTime(2018, 4, 5, 17, 31, 0, DateTimeKind.Utc),
                ContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["comexampleextension2"] = new { othervalue = 5 };

            var message = new AmqpCloudEventMessage(cloudEvent, ContentMode.Structured, new JsonEventFormatter());
            Assert.True(message.IsCloudEvent());
            var encodedAmqpMessage = message.Encode();

            var message1 = Message.Decode(encodedAmqpMessage);
            Assert.True(message1.IsCloudEvent());
            var receivedCloudEvent = message1.ToCloudEvent();

            Assert.Equal("0.2", receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                receivedCloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), receivedCloudEvent.ContentType);
            Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

            var attr = receivedCloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
            Assert.Equal(5, (int)((dynamic)attr["comexampleextension2"]).othervalue);
        }

        [Fact]
        public void AmqpBinaryMessageTest()
        {
            // the AMQPNetLite library is factored such
            // that we don't need to do a wire test here


            var jsonEventFormatter = new JsonEventFormatter();
            var cloudEvent = new CloudEvent("com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"))
            {
                Id = "A234-1234-1234",
                Time = new DateTime(2018, 4, 5, 17, 31, 0, DateTimeKind.Utc),
                ContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["comexampleextension2"] = new { othervalue = 5 };

            var message = new AmqpCloudEventMessage(cloudEvent, ContentMode.Binary, new JsonEventFormatter());
            Assert.True(message.IsCloudEvent());
            var encodedAmqpMessage = message.Encode();

            var message1 = Message.Decode(encodedAmqpMessage);
            Assert.True(message1.IsCloudEvent());
            var receivedCloudEvent = message1.ToCloudEvent();

            Assert.Equal("0.2", receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                receivedCloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), receivedCloudEvent.ContentType);
            Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

            var attr = receivedCloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
            Assert.Equal(5, (int)((dynamic)attr["comexampleextension2"]).othervalue);
        }
    }
}