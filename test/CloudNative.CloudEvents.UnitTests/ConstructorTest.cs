// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.UnitTests
{
    using System;
    using System.Net.Mime;
    using Xunit;

    public class ConstructorTest
    {
        [Fact]
        public void CreateBaseEvent1()
        {
            var cloudEvent = new CloudEvent("com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"))
            {
                Id = "A234-1234-1234",
                Time = new DateTime(2018, 4, 5, 17, 31, 0, DateTimeKind.Utc),
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["comexampleextension2"] = new { othervalue = 5 };

            Assert.Equal(CloudEventsSpecVersion.Default, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            var attr = cloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
            Assert.Equal(5, (int)((dynamic)attr["comexampleextension2"]).othervalue);
        }

        [Fact]
        public void CreateBaseEvent2()
        {
            var cloudEvent = new CloudEvent(
                "com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"),
                "A234-1234-1234",
                new DateTime(2018, 4, 5, 17, 31, 0, DateTimeKind.Utc))
            {
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["comexampleextension2"] = new { othervalue = 5 };

            Assert.Equal(CloudEventsSpecVersion.Default, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            var attr = cloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
            Assert.Equal(5, (int)((dynamic)attr["comexampleextension2"]).othervalue);
        }


        [Fact]
        public void CreateV0_1()
        {
            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V0_1, "com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"))
            {
                Id = "A234-1234-1234",
                Time = new DateTime(2018, 4, 5, 17, 31, 0, DateTimeKind.Utc),
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["comexampleextension2"] = new { othervalue = 5 };

            Assert.Equal(CloudEventsSpecVersion.V0_1, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            var attr = cloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
            Assert.Equal(5, (int)((dynamic)attr["comexampleextension2"]).othervalue);
        }

        [Fact]
        public void CreateV0_1ConvertToV0_2()
        {
            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V0_1, "com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"))
            {
                Id = "A234-1234-1234",
                Time = new DateTime(2018, 4, 5, 17, 31, 0, DateTimeKind.Utc),
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["comexampleextension2"] = new { othervalue = 5 };

            cloudEvent.SpecVersion = CloudEventsSpecVersion.V0_2;

            Assert.Equal(CloudEventsSpecVersion.V0_2, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            var attr = cloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
            Assert.Equal(5, (int)((dynamic)attr["comexampleextension2"]).othervalue);
        }


        [Fact]
        public void CreateV0_2ConvertToV1_0()
        {
            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V0_2, "com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"))
            {
                Id = "A234-1234-1234",
                Time = new DateTime(2018, 4, 5, 17, 31, 0, DateTimeKind.Utc),
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";

            cloudEvent.SpecVersion = CloudEventsSpecVersion.V1_0;

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
        public void CreateEventWithExtensions()
        {
            var cloudEvent = new CloudEvent(
                "com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"),
                "A234-1234-1234",
                new DateTime(2018, 4, 5, 17, 31, 0, DateTimeKind.Utc),
                new ComExampleExtension1Extension()
                {
                    ComExampleExtension1 = "value"
                })
            {
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            Assert.Equal(CloudEventsSpecVersion.Default, cloudEvent.SpecVersion);
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