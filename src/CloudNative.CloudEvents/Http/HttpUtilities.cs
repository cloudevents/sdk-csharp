// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.Http
{
    /// <summary>
    /// Common functionality used by all HTTP code. This is public to enable reuse by other packages,
    /// e.g. ASP.NET Core code.
    /// </summary>
    public static class HttpUtilities
    {
        /// <summary>
        /// The prefix used by all CloudEvents HTTP headers.
        /// </summary>
        public static string HttpHeaderPrefix { get; } = "ce-";

        /// <summary>
        /// The name of the HTTP header used to specify the CloudEvents specification version in an HTTP message.
        /// </summary>
        public static string SpecVersionHttpHeader { get; } = HttpHeaderPrefix + "specversion";

        /// <summary>
        /// Checks whether the given HTTP header name starts with "ce-", and if so, converts it into
        /// a lower-case attribute name.
        /// </summary>
        /// <param name="headerName">The name of the header to check. Must not be null.</param>
        /// <returns>The corresponding attribute name if the header name matches the CloudEvents header prefix;
        /// null otherwise.</returns>
        public static string GetAttributeNameFromHeaderName(string headerName) =>
            Validation.CheckNotNull(headerName, nameof(headerName)).StartsWith(HttpHeaderPrefix, StringComparison.InvariantCultureIgnoreCase)
            ? headerName.Substring(HttpHeaderPrefix.Length).ToLowerInvariant()
            : null;

        internal static async Task<(HttpStatusCode statusCode, string allowedOriginHeader, string allowedRateHeader)> HandleWebHookValidationAsync<TRequest>(
            TRequest request, Func<TRequest, string, string> headerSelector,
            Func<string, bool> validateOrigin, Func<string, string> validateRate)
        {
            var origin = headerSelector(request, "WebHook-Request-Origin");
            var rate = headerSelector(request, "WebHook-Request-Rate");

            if (origin is null || validateOrigin?.Invoke(origin) == false)
            {
                return (HttpStatusCode.MethodNotAllowed, null, null);
            }

            if (rate is object)
            {
                rate = validateRate?.Invoke(rate) ?? "*";
            }

            string callbackUri = headerSelector(request, "WebHook-Request-Callback");

            if (callbackUri is object)
            {
                try
                {
                    HttpClient client = new HttpClient();
                    var response = await client.GetAsync(new Uri(callbackUri));
                    return (response.StatusCode, null, null);
                }
                catch (Exception)
                {
                    return (HttpStatusCode.InternalServerError, null, null);
                }
            }
            else
            {
                return (HttpStatusCode.OK, origin, rate);
            }
        }

        /// <summary>
        /// Encodes the given value so that it is suitable to use as a header encoding,
        /// using percent-encoding for all non-ASCII values, as well as space, percent and double-quote.
        /// </summary>
        /// <param name="value">The header value to encode. Must not be null.</param>
        /// <returns>The encoded header value.</returns>
        public static string EncodeHeaderValue(string value)
        {
            Validation.CheckNotNull(value, nameof(value));

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                int codePoint = char.ConvertToUtf32(value, i);

                // Encode: non-printable-ASCII, non-ASCII, space, percent, double-quote.
                // Start by handling the ASCII special cases which are all hard-coded (and most common)
                switch (codePoint)
                {
                    case ' ':
                        builder.Append("%20");
                        continue;
                    case '"':
                        builder.Append("%22");
                        continue;
                    case '%':
                        builder.Append("%25");
                        continue;
                }
                // Now check for simple ASCII that doesn't need to be encoded
                if (codePoint > 0x20 && codePoint < 0x7f)
                {
                    builder.Append((char) codePoint);
                    continue;
                }

                // Full-on UTF-8 encoding now... it's simple enough to inline this
                // code, avoiding any allocation for Encoding.UTF8.GetBytes.
                // The Wikipedia page on UTF-8 is helpful for understanding this: https://en.wikipedia.org/wiki/UTF-8
                if (codePoint < 0x80)
                {
                    AppendPercentEncodedByte(codePoint);
                }
                else if (codePoint < 0x800)
                {
                    AppendPercentEncodedByte(0b11000000 | (codePoint >> 6));
                    AppendPercentEncodedByte(0b10000000 | (codePoint & 0b111111));
                }
                else if (codePoint < 0x10000)
                {
                    AppendPercentEncodedByte(0b11100000 | (codePoint >> 12));
                    AppendPercentEncodedByte(0b10000000 | ((codePoint >> 6) & 0b111111));
                    AppendPercentEncodedByte(0b10000000 | (codePoint & 0b111111));
                }
                else
                {
                    AppendPercentEncodedByte(0b11110000 | (codePoint >> 18));
                    AppendPercentEncodedByte(0b10000000 | ((codePoint >> 12) & 0b111111));
                    AppendPercentEncodedByte(0b10000000 | ((codePoint >> 6) & 0b111111));
                    AppendPercentEncodedByte(0b10000000 | (codePoint & 0b111111));
                    // Non-BMP character: this will have been represented as a surrogate
                    // pair in the input string, so skip over the second UTF-16 code unit.
                    i++;
                }
            }
            return builder.ToString();

            // Note: parameter is int rather than byte to avoid lots of casts in the call site.
            void AppendPercentEncodedByte(int b)
            {
                const string HexDigits = "0123456789ABCDEF";
                builder.Append('%');
                builder.Append(HexDigits[b >> 4]);
                builder.Append(HexDigits[b & 0x0f]);
            }
        }

        /// <summary>
        /// Decodes the given HTTP header value, first decoding any double-quoted strings,
        /// then performing percent-decoding.
        /// </summary>
        /// <param name="value">The header value to decode. Must not be null.</param>
        /// <returns>The decoded header value.</returns>
        public static string DecodeHeaderValue(string value)
        {
            Validation.CheckNotNull(value, nameof(value));
            value = DecodeDoubleQuoted(value);

            // Common case: no percent-encoding to decode.
            if (value.IndexOf('%') == -1)
            {
                return value;
            }

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c != '%')
                {
                    builder.Append(c);
                    continue;
                }

                // Percent encoding is remarkably complex to handle correctly and efficiently.
                if (i + 2 >= value.Length)
                {
                    throw new ArgumentException("Invalid HTTP header value for CloudEvent attribute: percent is not followed by two hex digits");
                }
                byte byte1 = DecodePercentEncodedByte(i);

                // Use the first byte to work out how long the character is in bytes.
                int byteCount = byte1 < 0x80 ? 1
                    : byte1 >= 0b11000000 && byte1 <= 0b11011111 ? 2
                    : byte1 >= 0b11100000 && byte1 <= 0b11101111 ? 3
                    : byte1 >= 0b11110000 && byte1 <= 0b11110111 ? 4
                    : throw new ArgumentException("Invalid HTTP header value for CloudEvent attribute: percent-encoded value is invalid UTF-8");

                // Note: we could allocate a byte array here and delegate to Encoding.UTF8, but for the common case of non-emoji, that
                // would allocate pointlessly - and it's not immediately obvious whether it performs all the validation we want.
                // With comprehensive conformance tests, we can tweak this.
                int utf32 = byteCount switch
                {
                    1 => byte1,
                    2 => ((byte1 & 0b11111) << 6) | DecodeUtf8ContinuationByte(i + 3),
                    3 => ((byte1 & 0b1111) << 12) | (DecodeUtf8ContinuationByte(i + 3) << 6) | DecodeUtf8ContinuationByte(i + 6),
                    4 => ((byte1 & 0b111) << 18) | (DecodeUtf8ContinuationByte(i + 3) << 12) | (DecodeUtf8ContinuationByte(i + 6) << 6) | DecodeUtf8ContinuationByte(i + 9),
                    _ => throw new InvalidOperationException("Can't get here due to previous switch statement")
                };
                // Validate the UTF-32 value:
                // - Not a high/low surrogate
                // - Not represented using an "overlong" encoding

                // Validate we don't have a high/low surrogate on its own.
                if (utf32 >= 0xd800 && utf32 <= 0xdfff)
                {
                    throw new ArgumentException("Invalid HTTP header value for CloudEvent attribute: percent-encoded value is invalid UTF-8");
                }

                // Validate we don't have an overlong sequence
                int expectedByteCount = utf32 < 0x80 ? 1
                    : utf32 < 0x800 ? 2
                    : utf32 < 0x10000 ? 3
                    : 4;

                if (expectedByteCount != byteCount)
                {
                    throw new ArgumentException("Invalid HTTP header value for CloudEvent attribute: percent-encoded value is invalid UTF-8");
                }

                // Finally, append the result of the percent-decoding
                if (utf32 < 0x10000)
                {
                    builder.Append((char) utf32);
                }
                else
                {
                    // Note: we could do this more efficiently, without allocating a string, but
                    // it's simpler to delegate to ConvertFromUtf32.
                    builder.Append(char.ConvertFromUtf32(utf32));
                }
                // Skip over the characters we've now handled.
                i += byteCount * 3 - 1;
            }
            return builder.ToString();

            int DecodeUtf8ContinuationByte(int index)
            {
                if (index >= value.Length || value[index] != '%')
                {
                    throw new ArgumentException("Invalid HTTP header value for CloudEvent attribute: percent-encoded value is invalid UTF-8");
                }
                byte result = DecodePercentEncodedByte(index);
                // Validate that top bit is set, and next bit is clear.
                // (The other six bits are the actual value.)
                if ((result >> 6) != 2)
                {
                    throw new ArgumentException("Invalid HTTP header value for CloudEvent attribute: percent-encoded value is invalid UTF-8");
                }
                return result & 0b0011_1111;
            }

            byte DecodePercentEncodedByte(int index)
            {
                if (index + 2 >= value.Length)
                {
                    throw new ArgumentException("Invalid HTTP header value for CloudEvent attribute: percent is not followed by two hex digits");
                }
                char high = value[index + 1];
                char low = value[index + 2];
                return (byte) ((ParseNybble(high) << 4) | ParseNybble(low));
            }

            static int ParseNybble(char nybble)
            {
                if (nybble >= '0' && nybble <= '9')
                {
                    return nybble - '0';
                }
                if (nybble >= 'A' && nybble <= 'F')
                {
                    return nybble - 'A' + 10;
                }
                if (nybble >= 'a' && nybble <= 'f')
                {
                    return nybble - 'a' + 10;
                }
                throw new ArgumentException("Invalid HTTP header value for CloudEvent attribute: percent is not followed by two hex digits");
            }
        }

        /// <summary>
        /// Applies the double-quoting part of https://tools.ietf.org/html/rfc7230#section-3.2.6 in reverse.
        /// </summary>
        private static string DecodeDoubleQuoted(string value)
        {
            if (value.IndexOf('\"') == -1)
            {
                return value;
            }
            StringBuilder builder = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == '\\' && inQuotes)
                {
                    if (i + 1 == value.Length)
                    {
                        throw new ArgumentException("Invalid HTTP header value for CloudEvent attribute: unterminated backslash escape");
                    }
                    builder.Append(value[i + 1]);
                    i++;
                }
                else
                {
                    builder.Append(c);
                }
            }
            if (inQuotes)
            {
                throw new ArgumentException("Invalid HTTP header value for CloudEvent attribute: unterminated quoted string");
            }
            return builder.ToString();
        }
    }
}
