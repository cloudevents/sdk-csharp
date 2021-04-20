// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Linq;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Xunit;

namespace CloudNative.CloudEvents.Core.UnitTests
{
    public class MimeUtilitiesTest
    {
        [Theory]
        [InlineData("application/json")]
        [InlineData("application/json; charset=iso-8859-1")]
        [InlineData("application/json; charset=iso-8859-1; name=some-name")]
        [InlineData("application/json; charset=iso-8859-1; name=some-name; x=y; a=b")]
        [InlineData("application/json; charset=iso-8859-1; name=some-name; boundary=xyzzy; x=y")]
        public void ContentTypeConversions(string text)
        {
            var originalContentType = new ContentType(text);
            var header = MimeUtilities.ToMediaTypeHeaderValue(originalContentType);
            AssertEqualParts(text, header.ToString());
            var convertedContentType = MimeUtilities.ToContentType(header);
            AssertEqualParts(originalContentType.ToString(), convertedContentType.ToString());

            // Conversions can end up reordering the parameters. In reality we're only
            // likely to end up with a media type and charset, but our tests use more parameters.
            // This just makes them deterministic.
            void AssertEqualParts(string expected, string actual)
            {
                expected = string.Join(";", expected.Split(";").OrderBy(x => x));
                actual = string.Join(";", actual.Split(";").OrderBy(x => x));
                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public void ContentTypeConversions_Null()
        {
            Assert.Null(MimeUtilities.ToMediaTypeHeaderValue(default(ContentType)));
            Assert.Null(MimeUtilities.ToContentType(default(MediaTypeHeaderValue)));
        }

        [Theory]
        [InlineData("iso-8859-1")]
        [InlineData("utf-8")]
        public void ContentTypeGetEncoding(string charSet)
        {
            var contentType = new ContentType($"text/plain; charset={charSet}");
            Encoding encoding = MimeUtilities.GetEncoding(contentType);
            Assert.Equal(charSet, encoding.WebName);
        }

        [Fact]
        public void ContentTypeGetEncoding_NoContentType()
        {
            ContentType contentType = null;
            Encoding encoding = MimeUtilities.GetEncoding(contentType);
            Assert.Equal(Encoding.UTF8, encoding);
        }

        [Fact]
        public void ContentTypeGetEncoding_NoCharSet()
        {
            ContentType contentType = new ContentType("text/plain");
            Encoding encoding = MimeUtilities.GetEncoding(contentType);
            Assert.Equal(Encoding.UTF8, encoding);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("text/plain")]
        public void CreateContentTypeOrNull_WithContentType(string text)
        {
            ContentType ct = MimeUtilities.CreateContentTypeOrNull(text);
            Assert.Equal(text, ct?.ToString());
        }

        [Theory]
        [InlineData("text/plain", false)]
        [InlineData(null, false)]
        [InlineData("application/cloudevents", true)]
        [InlineData("application/cloudevents+json", true)]
        // It's not entirely clear that this *should* be true...
        [InlineData("application/cloudeventstrailing", true)]
        [InlineData("application/cloudevents-batch", false)]
        [InlineData("application/cloudevents-batch+json", false)]
        public void IsCloudEventsContentType(string contentType, bool expectedResult) =>
            Assert.Equal(expectedResult, MimeUtilities.IsCloudEventsContentType(contentType));

        [Theory]
        [InlineData("text/plain", false)]
        [InlineData(null, false)]
        [InlineData("application/cloudevents", false)]
        [InlineData("application/cloudevents+json", false)]
        [InlineData("application/cloudeventstrailing", false)]
        [InlineData("application/cloudevents-batch", true)]
        // It's not entirely clear that this *should* be true...
        [InlineData("application/cloudevents-batchtrailing", true)]
        [InlineData("application/cloudevents-batch+json", true)]
        public void IsCloudEventsBatchContentType(string contentType, bool expectedResult) =>
            Assert.Equal(expectedResult, MimeUtilities.IsCloudEventsBatchContentType(contentType));
    }
}
