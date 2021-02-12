// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.NewtonsoftJson;
using System;
using System.Net.Mime;
using System.Text;
using Xunit;
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
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonv10));
            var avroData = avroFormatter.EncodeStructuredEvent(cloudEvent, out var contentType);
            var cloudEvent2 = avroFormatter.DecodeStructuredEvent(avroData);

            Assert.Equal(cloudEvent2.SpecVersion, cloudEvent.SpecVersion);
            Assert.Equal(cloudEvent2.Type, cloudEvent.Type);
            Assert.Equal(cloudEvent2.Source, cloudEvent.Source);
            Assert.Equal(cloudEvent2.Id, cloudEvent.Id);
            AssertTimestampsEqual(cloudEvent2.Time.Value, cloudEvent.Time.Value);
            Assert.Equal(cloudEvent2.DataContentType, cloudEvent.DataContentType);
            Assert.Equal(cloudEvent2.Data, cloudEvent.Data);
        }

        [Fact]
        public void StructuredParseSuccess()
        {
            var jsonFormatter = new JsonEventFormatter();
            var avroFormatter = new AvroEventFormatter();
            var cloudEventJ = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonv10));
            var avroData = avroFormatter.EncodeStructuredEvent(cloudEventJ, out var contentType);
            var cloudEvent = avroFormatter.DecodeStructuredEvent(avroData);

            Assert.Equal(CloudEventsSpecVersion.V1_0, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            AssertTimestampsEqual("2018-04-05T17:31:00Z", cloudEvent.Time.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            Assert.Equal("value", (string)cloudEvent["comexampleextension1"]);
        }
        
        [Fact]
        public void StructuredParseWithExtensionsSuccess()
        {
            var jsonFormatter = new JsonEventFormatter();
            var avroFormatter = new AvroEventFormatter();
            var extensionAttribute = CloudEventAttribute.CreateExtension("comexampleextension1", CloudEventAttributeType.String);
            var cloudEventJ = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonv10), extensionAttribute);
            var avroData = avroFormatter.EncodeStructuredEvent(cloudEventJ, out var contentType);
            var cloudEvent = avroFormatter.DecodeStructuredEvent(avroData, extensionAttribute);

            Assert.Equal(CloudEventsSpecVersion.V1_0, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            AssertTimestampsEqual("2018-04-05T17:31:00Z", cloudEvent.Time.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            Assert.Equal("value", cloudEvent[extensionAttribute]);
        }
    }
}
