using System;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    using System.Net.Mime;

    public class JsonTest
    {
        const string json =
                "{\n" +
                "    \"specversion\" : \"0.1\",\n" +
                "    \"type\" : \"com.github.pull.create\",\n" +
                "    \"source\" : \"https://github.com/cloudevents/spec/pull/123\",\n" +
                "    \"id\" : \"A234-1234-1234\",\n" +
                "    \"time\" : \"2018-04-05T17:31:00Z\",\n" +
                "    \"comexampleextension1\" : \"value\",\n" +
                "    \"comexampleextension2\" : {\n" +
                "        \"othervalue\": 5\n" +
                "    },\n" +
                "    \"contenttype\" : \"text/xml\",\n" +
                "    \"data\" : \"<much wow=\\\"xml\\\"/>\"\n" +
                "}";

        [Fact]
        public void StructuredParseSuccess()
        {
            var cloudEvent = JsonEventFormatter.FromJson(json);
            Assert.Equal("0.1", cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), cloudEvent.ContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            var attr = cloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
            Assert.Equal(5, (int)((dynamic)attr["comexampleextension2"]).othervalue);
        }

        [Fact]
        public void StructuredParseWithExtensionsSuccess()
        {
            var cloudEvent = JsonEventFormatter.FromJson(json, new ComExampleExtension1Extension(), new ComExampleExtension2Extension());
            Assert.Equal("0.1", cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), cloudEvent.ContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            Assert.Equal("value", cloudEvent.Extension<ComExampleExtension1Extension>().ComExampleExtension1);
            Assert.Equal(5, cloudEvent.Extension<ComExampleExtension2Extension>().ComExampleExtension2.OtherValue);
        }

        [Fact]
        public void ReserializeTest()
        {
            var cloudEvent = JsonEventFormatter.FromJson(json);
            var jsonString = cloudEvent.ToJsonString();
            var cloudEvent2 = JsonEventFormatter.FromJson(jsonString);

            Assert.Equal(cloudEvent2.SpecVersion, cloudEvent.SpecVersion);
            Assert.Equal(cloudEvent2.Type, cloudEvent.Type);
            Assert.Equal(cloudEvent2.Source, cloudEvent.Source);
            Assert.Equal(cloudEvent2.Id, cloudEvent.Id);
            Assert.Equal(cloudEvent2.Time.Value.ToUniversalTime(), cloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(cloudEvent2.ContentType, cloudEvent.ContentType);
            Assert.Equal(cloudEvent2.Data, cloudEvent.Data);

        }
    }
}