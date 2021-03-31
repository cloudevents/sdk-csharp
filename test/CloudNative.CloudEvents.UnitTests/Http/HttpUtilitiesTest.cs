// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace CloudNative.CloudEvents.Http.UnitTests
{
    public class HttpUtilitiesTest
    {
        [Theory]
        [InlineData("simple", "simple")]
        [InlineData("Euro \u20AC \U0001F600", "Euro%20%E2%82%AC%20%F0%9F%98%80")]
        [InlineData("space encoded", "space%20encoded")]
        [InlineData("percent%encoded", "percent%25encoded")]
        [InlineData("quote\"encoded", "quote%22encoded")]
        [InlineData("caf\u00e9", "caf%C3%A9")]
        // This wouldn't be a valid attribute value in CloudEvents 1.0, but we encode ASCII control characters
        // for good measure, so let's test it.
        [InlineData("line1\r\nline2", "line1%0D%0Aline2")]
        public void RoundTripHeaderValue(string original, string encoded)
        {
            var actualEncoded = HttpUtilities.EncodeHeaderValue(original);
            Assert.Equal(encoded, actualEncoded);

            var actualDecoded = HttpUtilities.DecodeHeaderValue(encoded);
            Assert.Equal(original, actualDecoded);
        }

        // This is for values which would be encoded a different way, but we need to
        // test the decode path
        [Theory]
        [InlineData("lenient decoding %30%31", "lenient decoding 01")]
        [InlineData(@"""  quoted  text  ""unquoted", "  quoted  text  unquoted")]
        [InlineData(@"""escaped quote\""end""", @"escaped quote""end")]
        [InlineData(@"""escaped backslash\\end""", @"escaped backslash\end")]
        [InlineData(@"non-escaping backslash\end", @"non-escaping backslash\end")]
        // Mixed case for percent encoding
        [InlineData("Euro%20%e2%82%ac%20%f0%9F%98%80", "Euro \u20AC \U0001F600")]
        public void DecodeHeaderValue_NonRoundTrip(string headerValue, string expectedResult)
        {
            var actualResult = HttpUtilities.DecodeHeaderValue(headerValue);
            Assert.Equal(expectedResult, actualResult);
        }

        [Theory]
        [InlineData("unterminated percent %")]
        [InlineData("unterminated percent %0")]
        [InlineData("non hex percent %g0")]
        [InlineData("non hex percent %0g")]
        [InlineData("non hex percent %0$")]
        [InlineData("low surrogate %ED%B0%80")]
        [InlineData("high surrogate %ED%A0%80")]
        [InlineData("surrogate pair via two UTF-16 %ED%A0%80%ED%B0%80")]
        [InlineData("overlong UTF-8 %C0%A0")]
        [InlineData("incomplete end UTF-8 %E2")]
        [InlineData("incomplete end UTF-8 %E2%")]
        [InlineData("incomplete non-percent UTF-8 %E2x")]
        [InlineData("invalid UTF-8 first byte %82")]
        [InlineData("invalid UTF-8 second byte %E2%E2")]
        [InlineData(@"""unterminated quote")]
        [InlineData(@"""unterminated escape \")]
        public void DecodeHeaderValue_Invalid(string headerValue) =>
            Assert.Throws<ArgumentException>(() => HttpUtilities.DecodeHeaderValue(headerValue));
    }
}
