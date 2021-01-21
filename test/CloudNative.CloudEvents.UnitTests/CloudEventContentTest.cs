// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Net.Http.Headers;
using System.Net.Mime;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    public class CloudEventContentTest
    {
        [Fact]
        void ContentType_FromCloudEvent_BinaryMode()
        {
            var cloudEvent = CreateEmptyCloudEvent();
            cloudEvent.DataContentType = new ContentType("text/plain");
            var content = new CloudEventContent(cloudEvent, ContentMode.Binary, new JsonEventFormatter());
            var expectedContentType = new MediaTypeHeaderValue("text/plain");
            Assert.Equal(expectedContentType, content.Headers.ContentType);
        }

        [Fact]
        void ContentType_MissingFromCloudEvent_BinaryMode()
        {
            var cloudEvent = CreateEmptyCloudEvent();
            var exception = Assert.Throws<ArgumentException>(() => new CloudEventContent(cloudEvent, ContentMode.Binary, new JsonEventFormatter()));
            Assert.StartsWith(Strings.ErrorContentTypeUnspecified, exception.Message);
        }

        [Fact]
        void Invalid_ContentType_Throws()
        {
            var cloudEvent = CreateEmptyCloudEvent();
            var exception = Assert.Throws<InvalidOperationException>(() => cloudEvent.GetAttributes().Add("datacontenttype", "text/html; charset: windows-1255"));
            Assert.StartsWith(Strings.ErrorContentTypeIsNotRFC2046, exception.Message);
        }

        static CloudEvent CreateEmptyCloudEvent() =>
            new CloudEvent(CloudEventsSpecVersion.V1_0, "type",
                new Uri("https://source"), "subject", "id", DateTime.UtcNow);
    }
}
