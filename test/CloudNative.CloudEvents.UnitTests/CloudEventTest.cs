// Copyright 2020 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Net.Mime;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.UnitTests
{
    public class CloudEventTest
    {
        private static readonly DateTimeOffset sampleTimestamp = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero);

        [Fact]
        public void CreateBaseEvent1()
        {
            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull/123"),
                Id = "A234-1234-1234",
                Time = sampleTimestamp,
                DataContentType = MediaTypeNames.Text.Xml,
                Data = "<much wow=\"xml\"/>",
                ["comexampleextension1"] = "value"
            };

            Assert.Equal(CloudEventsSpecVersion.Default, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            AssertTimestampsEqual("2018-04-05T17:31:00Z", cloudEvent.Time.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            Assert.Equal("value", (string)cloudEvent["comexampleextension1"]);
        }

        [Fact]
        public void CreateEventWithExtension()
        {
            var extension = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer);

            var cloudEvent = new CloudEvent(new[] { extension })
            {
                Type =  "com.github.pull.create",
                Id = "A234-1234-1234",
                Time = sampleTimestamp,
                [extension] = 10
            };

            Assert.Equal(10, cloudEvent[extension]);
            Assert.Equal(10, cloudEvent["ext"]);

            // TODO: Decide whether this should actually be FormatException, or whether it should always end
            // up as an ArgumentException.
            Assert.Throws<FormatException>(() => cloudEvent.SetAttributeFromString("ext", "not an integer"));
            cloudEvent.SetAttributeFromString("ext", "20");
            Assert.Equal(20, cloudEvent[extension]);
        }

        [Fact]
        public void Invalid_ContentType_Throws()
        {
            var cloudEvent = new CloudEvent();
            var exception = Assert.Throws<ArgumentException>(() => cloudEvent.DataContentType = "text/html; charset:");
            Assert.StartsWith(Strings.ErrorContentTypeIsNotRFC2046, exception.InnerException.Message);
        }

        [Fact]
        public void SetAttributePropertiesToNull()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "some data",
                DataContentType = "text/plain",
                DataSchema = new Uri("https://schema")
            };

            cloudEvent.Type = null;
            cloudEvent.Source = null;
            cloudEvent.Subject = null;
            cloudEvent.Id = null;
            cloudEvent.Time = null;
            cloudEvent.Data = null;
            cloudEvent.DataContentType = null;
            cloudEvent.DataSchema = null;

            Assert.Null(cloudEvent.Type);
            Assert.Null(cloudEvent.Source);
            Assert.Null(cloudEvent.Subject);
            Assert.Null(cloudEvent.Id);
            Assert.Null(cloudEvent.Time);
            Assert.Null(cloudEvent.Data);
            Assert.Null(cloudEvent.DataContentType);
            Assert.Null(cloudEvent.DataSchema);
        }

        [Fact]
        public void Validate_Invalid()
        {
            var cloudEvent = new CloudEvent
            {
                Type = "type",
                DataContentType = "text/plain",
                Data = "text"
            };
            Assert.False(cloudEvent.IsValid);
            var exception = Assert.Throws<InvalidOperationException>(() => cloudEvent.Validate());
            Assert.Contains(CloudEventsSpecVersion.Default.IdAttribute.Name, exception.Message);
            Assert.Contains(CloudEventsSpecVersion.Default.SourceAttribute.Name, exception.Message);
            Assert.DoesNotContain(CloudEventsSpecVersion.Default.TypeAttribute.Name, exception.Message);
        }

        [Fact]
        public void Validate_Valid()
        {
            var cloudEvent = new CloudEvent
            {
                Type = "type",
                Id = "id",
                Source = new Uri("https://somewhere")
            };
            Assert.True(cloudEvent.IsValid);
            Assert.Same(cloudEvent, cloudEvent.Validate());
        }
    }
}
