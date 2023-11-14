// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.NewtonsoftJson;
using CloudNative.CloudEvents.UnitTests.Avro.Helpers;
using System;
using System.Net.Mime;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.CloudEventFormatterExtensions;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.Avro.UnitTests
{
    public class AvroEventFormatterTest
    {
        private static readonly string jsonv10 = @"
            {
                'specversion' : '1.0',
                'type' : 'com.github.pull.create',
                'source' : 'https://github.com/cloudevents/spec/pull/123',
                'id' : 'A234-1234-1234',
                'time' : '2018-04-05T17:31:00Z',
                'comexampleextension1' : 'value',
                'datacontenttype' : 'text/xml',
                'data' : '<much wow=\'xml\'/>'
            }".Replace('\'', '"');

        [Fact]
        public void ReserializeTest()
        {
            var jsonFormatter = new JsonEventFormatter();
            var avroFormatter = new AvroEventFormatter();
            var cloudEvent = jsonFormatter.DecodeStructuredModeText(jsonv10);
            var avroData = avroFormatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
            var cloudEvent2 = avroFormatter.DecodeStructuredModeMessage(avroData, contentType, extensionAttributes: null);

            Assert.Equal(cloudEvent2.SpecVersion, cloudEvent.SpecVersion);
            Assert.Equal(cloudEvent2.Type, cloudEvent.Type);
            Assert.Equal(cloudEvent2.Source, cloudEvent.Source);
            Assert.Equal(cloudEvent2.Id, cloudEvent.Id);
            AssertTimestampsEqual(cloudEvent2.Time!.Value, cloudEvent.Time!.Value);
            Assert.Equal(cloudEvent2.DataContentType, cloudEvent.DataContentType);
            Assert.Equal(cloudEvent2.Data, cloudEvent.Data);
        }

        [Fact]
        public void StructuredParseSuccess()
        {
            var jsonFormatter = new JsonEventFormatter();
            var avroFormatter = new AvroEventFormatter();
            var cloudEventJ = jsonFormatter.DecodeStructuredModeText(jsonv10);
            var avroData = avroFormatter.EncodeStructuredModeMessage(cloudEventJ, out var contentType);
            var cloudEvent = avroFormatter.DecodeStructuredModeMessage(avroData, contentType, extensionAttributes: null);

            Assert.Equal(CloudEventsSpecVersion.V1_0, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            AssertTimestampsEqual("2018-04-05T17:31:00Z", cloudEvent.Time!.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            Assert.Equal("value", (string?) cloudEvent["comexampleextension1"]);
        }

        [Fact]
        public void StructuredParseWithExtensionsSuccess()
        {
            var jsonFormatter = new JsonEventFormatter();
            var avroFormatter = new AvroEventFormatter();
            var extensionAttribute = CloudEventAttribute.CreateExtension("comexampleextension1", CloudEventAttributeType.String);
            var cloudEventJ = jsonFormatter.DecodeStructuredModeText(jsonv10, new[] { extensionAttribute });
            var avroData = avroFormatter.EncodeStructuredModeMessage(cloudEventJ, out var contentType);
            var cloudEvent = avroFormatter.DecodeStructuredModeMessage(avroData, contentType, new[] { extensionAttribute });

            Assert.Equal(CloudEventsSpecVersion.V1_0, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            AssertTimestampsEqual("2018-04-05T17:31:00Z", cloudEvent.Time!.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            Assert.Equal("value", cloudEvent[extensionAttribute]);
        }

        [Fact]
        public void StructuredParseSerializationWithCustomSerializer()
        {
            var serializer = new FakeGenericRecordSerializer();
            var jsonFormatter = new JsonEventFormatter();
            var avroFormatter = new AvroEventFormatter(serializer);

            var expectedSerializedData = new byte[] { 0x1, 0x2, 0x3, };
            serializer.SetSerializeResponse(expectedSerializedData);

            var inputCloudEvent = jsonFormatter.DecodeStructuredModeText(jsonv10);
            var avroData = avroFormatter
                .EncodeStructuredModeMessage(inputCloudEvent, out var _)
                .ToArray();

            Assert.Equal(1, serializer.SerializeCalls);
            Assert.Equal(expectedSerializedData, avroData);
        }

        [Fact]
        public void StructuredParseDeserializationWithCustomSerializer()
        {
            var serializer = new FakeGenericRecordSerializer();
            var avroFormatter = new AvroEventFormatter(serializer);
            var expectedCloudEventId = "4321";
            var expectedCloudEventType = "MyBrilliantEvent";
            var expectedCloudEventSource = "https://cloudevents.io.test/test-event";
            serializer.SetDeserializeResponseAttributes(
                expectedCloudEventId, expectedCloudEventType, expectedCloudEventSource);

            var actualCloudEvent = avroFormatter.DecodeStructuredModeMessage(Array.Empty<byte>(), null, null);

            Assert.Equal(1, serializer.DeserializeCalls);
            Assert.Equal(expectedCloudEventId, actualCloudEvent.Id);
            Assert.Equal(expectedCloudEventType, actualCloudEvent.Type);
            Assert.Equal(expectedCloudEventSource, actualCloudEvent.Source!.ToString());
        }
    }
}
