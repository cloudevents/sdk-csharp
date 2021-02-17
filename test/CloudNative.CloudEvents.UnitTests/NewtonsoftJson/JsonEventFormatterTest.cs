// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Net.Mime;
using System.Text;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.CloudEventFormatterExtensions;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.NewtonsoftJson.UnitTests
{
    public class JsonEventFormatterTest
    {
        private static readonly string jsonv10 = @"
            {
                'specversion' : '1.0',
                'type' : 'com.github.pull.create',
                'source' : 'https://github.com/cloudevents/spec/pull/123',
                'id' : 'A234-1234-1234',
                'time' : '2018-04-05T17:31:00Z',
                'comexampleextension1' : 'value',
                'comexampleextension2' : 10,
                'datacontenttype' : 'text/xml',
                'data' : '<much wow=\'xml\'/>'
            }".Replace('\'', '"');


        [Fact]
        public void ReserializeTest10()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent = jsonFormatter.DecodeStructuredModeText(jsonv10);
            var jsonData = jsonFormatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
            Assert.Equal("application/cloudevents+json", contentType.MediaType);

            var cloudEvent2 = jsonFormatter.DecodeStructuredModeMessage(jsonData, contentType: null, Array.Empty<CloudEventAttribute>());

            Assert.Equal(cloudEvent2.SpecVersion, cloudEvent.SpecVersion);
            Assert.Equal(cloudEvent2.Type, cloudEvent.Type);
            Assert.Equal(cloudEvent2.Source, cloudEvent.Source);
            Assert.Equal(cloudEvent2.Id, cloudEvent.Id);
            AssertTimestampsEqual(cloudEvent2.Time, cloudEvent.Time);
            Assert.Equal(cloudEvent2.DataContentType, cloudEvent.DataContentType);
            Assert.Equal(cloudEvent2.Data, cloudEvent.Data);
        }

        [Fact]
        public void StructuredParseSuccess10()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent = jsonFormatter.DecodeStructuredModeMessage(Encoding.UTF8.GetBytes(jsonv10), contentType: null, extensionAttributes: null);
            Assert.Equal(CloudEventsSpecVersion.V1_0, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            AssertTimestampsEqual("2018-04-05T17:31:00Z", cloudEvent.Time.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);
            Assert.Equal("value", cloudEvent["comexampleextension1"]);
            Assert.Equal("10", cloudEvent["comexampleextension2"]);
        }

        [Fact]
        public void StructuredParseWithExtensionsSuccess10()
        {
            // Register comexampleextension2 as an integer extension before parsing.
            var extension = CloudEventAttribute.CreateExtension("comexampleextension2", CloudEventAttributeType.Integer);

            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent = jsonFormatter.DecodeStructuredModeMessage(Encoding.UTF8.GetBytes(jsonv10), contentType: null, new[] { extension });
            // Instead of getting it as a string (as before), we now have it as an integer.
            Assert.Equal(10, cloudEvent["comexampleextension2"]);
        }
    }
}