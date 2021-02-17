// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Linq;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests.Http
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
            var header = originalContentType.ToMediaTypeHeaderValue();
            AssertEqualParts(text, header.ToString());
            var convertedContentType = header.ToContentType();
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
            Assert.Null(default(ContentType).ToMediaTypeHeaderValue());
            Assert.Null(default(MediaTypeHeaderValue).ToContentType());
        }

        [Theory]
        [InlineData("iso-8859-1")]
        [InlineData("utf-8")]
        public void ContentTypeGetEncoding(string charSet)
        {
            var contentType = new ContentType($"text/plain; charset={charSet}");
            Encoding encoding = contentType.GetEncoding();
            Assert.Equal(charSet, encoding.WebName);
        }

        [Fact]
        public void ContentTypeGetEncoding_NoContentType()
        {
            ContentType contentType = null;
            Encoding encoding = contentType.GetEncoding();
            Assert.Equal(Encoding.UTF8, encoding);
        }

        [Fact]
        public void ContentTypeGetEncoding_NoCharSet()
        {
            ContentType contentType = new ContentType("text/plain");
            Encoding encoding = contentType.GetEncoding();
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
    }
}
