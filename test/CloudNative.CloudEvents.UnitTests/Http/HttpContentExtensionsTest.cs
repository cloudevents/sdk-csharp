// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.NewtonsoftJson;
using System;
using System.Net.Http.Headers;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.Http.UnitTests
{
    public class HttpContentExtensionsTest
    {
        [Fact]
        public void ContentType_FromCloudEvent_BinaryMode()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.DataContentType = "text/plain";
            var content = cloudEvent.ToHttpContent(ContentMode.Binary, new JsonEventFormatter());
            var expectedContentType = new MediaTypeHeaderValue("text/plain");
            Assert.Equal(expectedContentType, content.Headers.ContentType);
        }

        // We need to work out whether we want a modified version of this test.
        // It should be okay to not set a DataContentType if there's no data...
        // but what if there's a data value which is an empty string, empty byte array or empty stream?
        [Fact]
        public void NoContentType_NoContent()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var content = cloudEvent.ToHttpContent(ContentMode.Binary, new JsonEventFormatter());
            Assert.Null(content.Headers.ContentType);
        }

        [Fact]
        public void NoContentType_WithContent()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = "Some text";
            var exception = Assert.Throws<ArgumentException>(() => cloudEvent.ToHttpContent(ContentMode.Binary, new JsonEventFormatter()));
            Assert.StartsWith(Strings.ErrorContentTypeUnspecified, exception.Message);
        }
    }
}
