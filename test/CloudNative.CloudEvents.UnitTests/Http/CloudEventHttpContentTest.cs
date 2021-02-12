// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.NewtonsoftJson;
using System;
using System.Net.Http.Headers;
using Xunit;

namespace CloudNative.CloudEvents.Http.UnitTests
{
    public class CloudEventHttpContentTest
    {
        [Fact]
        public void ContentType_FromCloudEvent_BinaryMode()
        {
            var cloudEvent = CreateEmptyCloudEvent();
            cloudEvent.DataContentType = "text/plain";
            var content = new CloudEventHttpContent(cloudEvent, ContentMode.Binary, new JsonEventFormatter());
            var expectedContentType = new MediaTypeHeaderValue("text/plain");
            Assert.Equal(expectedContentType, content.Headers.ContentType);
        }

        // We need to work out whether we want a modified version of this test.
        // It should be okay to not set a DataContentType if there's no data...
        // but what if there's a data value which is an empty string, empty byte array or empty stream?
        [Fact]
        public void NoContentType_NoContent()
        {
            var cloudEvent = CreateEmptyCloudEvent();
            var content = new CloudEventHttpContent(cloudEvent, ContentMode.Binary, new JsonEventFormatter());
            Assert.Null(content.Headers.ContentType);
        }

        [Fact]
        public void NoContentType_WithContent()
        {
            var cloudEvent = CreateEmptyCloudEvent();
            cloudEvent.Data = "Some text";
            var exception = Assert.Throws<ArgumentException>(() => new CloudEventHttpContent(cloudEvent, ContentMode.Binary, new JsonEventFormatter()));
            Assert.StartsWith(Strings.ErrorContentTypeUnspecified, exception.Message);
        }

        private static CloudEvent CreateEmptyCloudEvent() =>
            new CloudEvent
            { 
                Type = "type",
                Source = new Uri("https://source"),
                Id = "id"
            };
    }
}
