// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.UnitTests
{
    using System;
    using System.Net.Mime;
    using CloudNative.CloudEvents.Amqp;
    using CloudNative.CloudEvents.Kafka;
    using Newtonsoft.Json;
    using Xunit;
    using Confluent.Kafka;
    using Newtonsoft.Json.Linq;
    using System.Collections.Generic;
    using System.Text;
    using CloudNative.CloudEvents.Extensions;

    public class KafkaTest
    {
        [Fact]
        public void KafkaStructuredMessageTest()
        {
            // Kafka doesn't provide any way to get to the message transport level to do the test properly
            // and it doesn't have an embedded version of a server for .Net so the lowest we can get is 
            // the `Message<T, K>`

            var jsonEventFormatter = new JsonEventFormatter();

            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0,
                "com.github.pull.create",
                source: new Uri("https://github.com/cloudevents/spec/pull"),
                subject: "123")
            {
                Id = "A234-1234-1234",
                Time = new DateTime(2018, 4, 5, 17, 31, 0, DateTimeKind.Utc),
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["comexampleextension2"] = new { othervalue = 5 };

            var message = new KafkaCloudEventMessage(cloudEvent, ContentMode.Structured, new JsonEventFormatter());

            Assert.True(message.IsCloudEvent());

            // using serialization to create fully independent copy thus simulating message transport
            // real transport will work in a similar way
            var serialized = JsonConvert.SerializeObject(message, new HeaderConverter());
            var messageCopy = JsonConvert.DeserializeObject<Message<string, byte[]>>(serialized, new HeadersConverter(), new HeaderConverter());

            Assert.True(messageCopy.IsCloudEvent());
            var receivedCloudEvent = messageCopy.ToCloudEvent(jsonEventFormatter);

            Assert.Equal(CloudEventsSpecVersion.Default, receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull"), receivedCloudEvent.Source);
            Assert.Equal("123", receivedCloudEvent.Subject);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                receivedCloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), receivedCloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

            var attr = receivedCloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
            Assert.Equal(5, (int)((dynamic)attr["comexampleextension2"]).othervalue);
        }

        [Fact]
        public void KafkaBinaryMessageTest()
        {
            // Kafka doesn't provide any way to get to the message transport level to do the test properly
            // and it doesn't have an embedded version of a server for .Net so the lowest we can get is 
            // the `Message<T, K>`

            var jsonEventFormatter = new JsonEventFormatter();
            var cloudEvent = new CloudEvent("com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"),
                extensions: new PartitioningExtension())
            {
                Id = "A234-1234-1234",
                Time = new DateTime(2018, 4, 5, 17, 31, 0, DateTimeKind.Utc),
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = Encoding.UTF8.GetBytes("<much wow=\"xml\"/>")
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["comexampleextension2"] = new { othervalue = 5 };
            cloudEvent.Extension<PartitioningExtension>().PartitioningKeyValue = "hello much wow";

            var message = new KafkaCloudEventMessage(cloudEvent, ContentMode.Binary, new JsonEventFormatter());
            Assert.True(message.IsCloudEvent());

            // using serialization to create fully independent copy thus simulating message transport
            // real transport will work in a similar way
            var serialized = JsonConvert.SerializeObject(message, new HeaderConverter());
            var messageCopy = JsonConvert.DeserializeObject<Message<string, byte[]>>(serialized, new HeadersConverter(), new HeaderConverter());

            Assert.True(messageCopy.IsCloudEvent());
            var receivedCloudEvent = messageCopy.ToCloudEvent(jsonEventFormatter, new PartitioningExtension());

            Assert.Equal(CloudEventsSpecVersion.Default, receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                receivedCloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), receivedCloudEvent.DataContentType);
            Assert.Equal(Encoding.UTF8.GetBytes("<much wow=\"xml\"/>"), receivedCloudEvent.Data);
            Assert.Equal("hello much wow", receivedCloudEvent.Extension<PartitioningExtension>().PartitioningKeyValue);

            var attr = receivedCloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
            Assert.Equal(5, (int)((dynamic)attr["comexampleextension2"]).othervalue);
        }

        private class HeadersConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Headers);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }
                else 
                {
                    var surrogate = serializer.Deserialize<List<Header>>(reader);
                    var headers = new Headers();

                    foreach(var header in surrogate)
                    {
                        headers.Add(header.Key, header.GetValueBytes());
                    }
                    return headers;
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        private class HeaderConverter : JsonConverter
        {
            private class HeaderContainer
            {
                public string Key { get; set; }
                public byte[] Value { get; set; }
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Header) || objectType == typeof(IHeader);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var headerContainer = serializer.Deserialize<HeaderContainer>(reader);
                return new Header(headerContainer.Key, headerContainer.Value);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var header = (IHeader)value;
                var container = new HeaderContainer { Key = header.Key, Value = header.GetValueBytes() };
                serializer.Serialize(writer, container);
            }
        }
    }
}