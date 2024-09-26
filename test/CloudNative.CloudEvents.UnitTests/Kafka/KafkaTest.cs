// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Extensions;
using CloudNative.CloudEvents.NewtonsoftJson;
using Confluent.Kafka;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.Kafka.UnitTests
{
    public class KafkaTest
    {
        [Theory]
        [InlineData("content-type", "application/cloudevents", true)]
        [InlineData("content-type", "APPLICATION/CLOUDEVENTS", true)]
        [InlineData("CONTENT-TYPE", "application/cloudevents", false)]
        [InlineData("ce_specversion", "1.0", true)]
        [InlineData("CE_SPECVERSION", "1.0", false)]
        public void IsCloudEvent(string headerName, string headerValue, bool expectedResult)
        {
            var message = new Message<string?, byte[]>
            {
                Headers = new Headers { { headerName, Encoding.UTF8.GetBytes(headerValue) } }
            };
            Assert.Equal(expectedResult, message.IsCloudEvent());
        }

        [Fact]
        public void IsCloudEvent_NoHeaders() =>
            Assert.False(new Message<string?, byte[]>().IsCloudEvent());

        private static CloudEvent CreateTestCloudEvent()
        {
            return new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull"),
                Subject = "123",
                Id = "A234-1234-1234",
                Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero),
                DataContentType = MediaTypeNames.Text.Xml,
                Data = "<much wow=\"xml\"/>",
                ["comexampleextension1"] = "value",
            };
        }

        private static void VerifyTestCloudEvent(CloudEvent receivedCloudEvent)
        {
            Assert.Equal(CloudEventsSpecVersion.Default, receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull"), receivedCloudEvent.Source);
            Assert.Equal("123", receivedCloudEvent.Subject);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            AssertTimestampsEqual("2018-04-05T17:31:00Z", receivedCloudEvent.Time!.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, receivedCloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

            Assert.Equal("value", (string?) receivedCloudEvent["comexampleextension1"]);
        }

        private static Message<TKey, byte[]>? SimulateMessageTransport<TKey>(Message<TKey, byte[]> message)
        {
            // Using serialization to create fully independent copy thus simulating message transport.
            // The real transport will work in a similar way.
            var serialized = JsonConvert.SerializeObject(message, new HeaderConverter());
            var messageCopy = JsonConvert.DeserializeObject<Message<TKey, byte[]>>(serialized, new HeadersConverter(), new HeaderConverter())!;
            return messageCopy;
        }

        [Fact]
        public void KafkaStructuredMessageTest()
        {
            // Kafka doesn't provide any way to get to the message transport level to do the test properly
            // and it doesn't have an embedded version of a server for .Net so the lowest we can get is 
            // the `Message<T, K>`

            var jsonEventFormatter = new JsonEventFormatter();
            var key = "Test";
            var cloudEvent = CreateTestCloudEvent();
            cloudEvent[Partitioning.PartitionKeyAttribute] = key;

            var message = cloudEvent.ToKafkaMessage(ContentMode.Structured, jsonEventFormatter);

            Assert.True(message.IsCloudEvent());

            var messageCopy = SimulateMessageTransport(message);

            Assert.NotNull(messageCopy);
            Assert.Equal(key, messageCopy.Key);
            Assert.True(messageCopy.IsCloudEvent());
            var receivedCloudEvent = messageCopy.ToCloudEvent(jsonEventFormatter, null);

            VerifyTestCloudEvent(receivedCloudEvent);
        }

        [Fact]
        public void KafkaBinaryGuidKeyedStructuredMessageTest()
        {
            // In order to test the most extreme case of key management we will simulate 
            // using Guid Keys serialized in their binary form in kafka that are converted
            // back to their string representation in the cloudEvent.
            var partitionKeyAdapter = new PartitionKeyAdapters.BinaryGuidPartitionKeyAdapter();
            var jsonEventFormatter = new JsonEventFormatter();
            var key = Guid.NewGuid();
            var cloudEvent = CreateTestCloudEvent();
            cloudEvent[Partitioning.PartitionKeyAttribute] = key.ToString();

            var message = cloudEvent.ToKafkaMessage<byte[]?>(
                ContentMode.Structured,
                jsonEventFormatter,
                partitionKeyAdapter);

            Assert.True(message.IsCloudEvent());

            var messageCopy = SimulateMessageTransport(message);

            Assert.NotNull(messageCopy);
            Assert.True(messageCopy.IsCloudEvent());

            var receivedCloudEvent = messageCopy.ToCloudEvent<byte[]?>(
                jsonEventFormatter,
                null,
                partitionKeyAdapter);

            Assert.NotNull(message.Key);
            // The key should be the original Guid in the binary representation.
            Assert.Equal(key, new Guid(messageCopy.Key!));
            VerifyTestCloudEvent(receivedCloudEvent);
        }

        [Fact]
        public void KafkaNullKeyedStructuredMessageTest()
        {
            // It will test the serialization using Confluent's Confluent.Kafka.Null type for the key.
            var partitionKeyAdapter = new PartitionKeyAdapters.NullPartitionKeyAdapter<Confluent.Kafka.Null>();
            var jsonEventFormatter = new JsonEventFormatter();
            var cloudEvent = CreateTestCloudEvent();
            // Even if the key is established in the cloud event it won't flow.
            cloudEvent[Partitioning.PartitionKeyAttribute] = "Test";

            var message = cloudEvent.ToKafkaMessage<Confluent.Kafka.Null>(
                ContentMode.Structured,
                jsonEventFormatter,
                partitionKeyAdapter);

            Assert.True(message.IsCloudEvent());

            var messageCopy = SimulateMessageTransport(message);

            Assert.NotNull(messageCopy);
            Assert.True(messageCopy.IsCloudEvent());

            var receivedCloudEvent = messageCopy.ToCloudEvent<Confluent.Kafka.Null>(
                jsonEventFormatter,
                null,
                partitionKeyAdapter);

            //The Message  key will continue to be null.
            Assert.Null(message.Key);
            VerifyTestCloudEvent(receivedCloudEvent);
        }

        [Fact]
        public void KafkaBinaryMessageTest()
        {
            // Kafka doesn't provide any way to get to the message transport level to do the test properly
            // and it doesn't have an embedded version of a server for .Net so the lowest we can get is 
            // the `Message<T, K>`

            var jsonEventFormatter = new JsonEventFormatter();
            var cloudEvent = new CloudEvent(Partitioning.AllAttributes)
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull/123"),
                Id = "A234-1234-1234",
                Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero),
                DataContentType = MediaTypeNames.Text.Xml,
                Data = Encoding.UTF8.GetBytes("<much wow=\"xml\"/>"),
                ["comexampleextension1"] = "value",
                [Partitioning.PartitionKeyAttribute] = "hello much wow"
            };

            var message = cloudEvent.ToKafkaMessage(ContentMode.Binary, new JsonEventFormatter());
            Assert.True(message.IsCloudEvent());

            // Using serialization to create fully independent copy thus simulating message transport.
            // The real transport will work in a similar way.
            var serialized = JsonConvert.SerializeObject(message, new HeaderConverter());
            var settings = new JsonSerializerSettings
            {
                Converters = { new HeadersConverter(), new HeaderConverter() }
            };
            var messageCopy = JsonConvert.DeserializeObject<Message<string?, byte[]>>(serialized, settings)!;

            Assert.True(messageCopy.IsCloudEvent());
            var receivedCloudEvent = messageCopy.ToCloudEvent(jsonEventFormatter, Partitioning.AllAttributes);

            Assert.Equal(CloudEventsSpecVersion.Default, receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            AssertTimestampsEqual("2018-04-05T17:31:00Z", receivedCloudEvent.Time!.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, receivedCloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);
            Assert.Equal("hello much wow", (string?) receivedCloudEvent[Partitioning.PartitionKeyAttribute]);

            Assert.Equal("value", (string?) receivedCloudEvent["comexampleextension1"]);
        }

        [Theory]
        [InlineData(MediaTypeNames.Application.Octet, new byte[0])]
        [InlineData(MediaTypeNames.Application.Json, null)]
        [InlineData(MediaTypeNames.Application.Xml, new byte[0])]
        [InlineData(MediaTypeNames.Text.Plain, "")]
        [InlineData(null, null)]
        public void KafkaBinaryMessageTombstoneTest(string? contentType, object? expectedDecodedResult)
        {
            var jsonEventFormatter = new JsonEventFormatter();
            var cloudEvent = new CloudEvent(Partitioning.AllAttributes)
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull/123"),
                Id = "A234-1234-1234",
                Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero),
                DataContentType = contentType,
                Data = null,
                ["comexampleextension1"] = "value",
                [Partitioning.PartitionKeyAttribute] = "hello much wow"
            };

            var message = cloudEvent.ToKafkaMessage(ContentMode.Binary, new JsonEventFormatter());
            Assert.True(message.IsCloudEvent());

            // Sending an empty message is equivalent to a delete (tombstone) for that partition key, when using compacted topics in Kafka.
            // This is the main use case for empty data messages with Kafka.
            Assert.Empty(message.Value);

            // Using serialization to create fully independent copy thus simulating message transport.
            // The real transport will work in a similar way.
            var serialized = JsonConvert.SerializeObject(message, new HeaderConverter());
            var settings = new JsonSerializerSettings
            {
                Converters = { new HeadersConverter(), new HeaderConverter() }
            };
            var messageCopy = JsonConvert.DeserializeObject<Message<string?, byte[]>>(serialized, settings)!;

            Assert.True(messageCopy.IsCloudEvent());
            var receivedCloudEvent = messageCopy.ToCloudEvent(jsonEventFormatter, Partitioning.AllAttributes);

            Assert.Equal(CloudEventsSpecVersion.Default, receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            AssertTimestampsEqual("2018-04-05T17:31:00Z", receivedCloudEvent.Time!.Value);
            Assert.Equal(contentType, receivedCloudEvent.DataContentType);
            Assert.Equal(expectedDecodedResult, receivedCloudEvent.Data);
            Assert.Equal("hello much wow", (string?) receivedCloudEvent[Partitioning.PartitionKeyAttribute]);
            Assert.Equal("value", (string?) receivedCloudEvent["comexampleextension1"]);
        }

        [Fact]
        public void ContentTypeCanBeInferredByFormatter()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "plain text"
            }.PopulateRequiredAttributes();

            var message = cloudEvent.ToKafkaMessage(ContentMode.Binary, new JsonEventFormatter());
            var contentTypeHeader = message.Headers.Single(h => h.Key == KafkaExtensions.KafkaContentTypeAttributeName);
            var contentTypeValue = Encoding.UTF8.GetString(contentTypeHeader.GetValueBytes());
            Assert.Equal("application/json", contentTypeValue);
        }

        private class HeadersConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Headers);
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }
                else
                {
                    var surrogate = serializer.Deserialize<List<Header>>(reader)!;
                    var headers = new Headers();

                    foreach (var header in surrogate)
                    {
                        headers.Add(header.Key, header.GetValueBytes());
                    }
                    return headers;
                }
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        private class HeaderConverter : JsonConverter
        {
            private class HeaderContainer
            {
                public string? Key { get; set; }
                public byte[]? Value { get; set; }
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Header) || objectType == typeof(IHeader);
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                var headerContainer = serializer.Deserialize<HeaderContainer>(reader)!;
                return new Header(headerContainer.Key, headerContainer.Value);
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                var header = (IHeader) value!;
                var container = new HeaderContainer { Key = header.Key, Value = header.GetValueBytes() };
                serializer.Serialize(writer, container);
            }
        }
    }
}
