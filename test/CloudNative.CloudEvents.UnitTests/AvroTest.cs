// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.UnitTests
{
    using System;
    using System.Net.Mime;
    using System.Text;
    using Xunit;

    public class AvroTest
    {
        const string jsonv10 =
            "{\n" +
            "    \"specversion\" : \"1.0\",\n" +
            "    \"type\" : \"com.github.pull.create\",\n" +
            "    \"source\" : \"https://github.com/cloudevents/spec/pull/123\",\n" +
            "    \"id\" : \"A234-1234-1234\",\n" +
            "    \"time\" : \"2018-04-05T17:31:00Z\",\n" +
            "    \"comexampleextension1\" : \"value\",\n" +
            "    \"datacontenttype\" : \"text/xml\",\n" +
            "    \"data\" : \"<much wow=\\\"xml\\\"/>\"\n" +
            "}";

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
            Assert.Equal(cloudEvent2.Time.Value.ToUniversalTime(), cloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(cloudEvent2.DataContentType, cloudEvent.DataContentType);
            Assert.Equal(cloudEvent2.Data, cloudEvent.Data);
        }


        [Fact]
        public void StructuredParseSuccess10()
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
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            var attr = cloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
        }

        
        [Fact]
        public void StructuredParseWithExtensionsSuccess10()
        {
            var jsonFormatter = new JsonEventFormatter();
            var avroFormatter = new AvroEventFormatter();
            var cloudEventJ = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonv10), new ComExampleExtension1Extension());
            var avroData = avroFormatter.EncodeStructuredEvent(cloudEventJ, out var contentType);
            var cloudEvent = avroFormatter.DecodeStructuredEvent(avroData, new ComExampleExtension1Extension());

            Assert.Equal(CloudEventsSpecVersion.V1_0, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            Assert.Equal("value", cloudEvent.Extension<ComExampleExtension1Extension>().ComExampleExtension1);
        }
    }
}