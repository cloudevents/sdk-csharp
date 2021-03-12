// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.Http
{
    /// <summary>
    /// Common functionality used by all Http code.
    /// TODO: Can we make this internal again? It's useful in the ASP.NET Core package, but I don't really like having it public...
    /// </summary>
    public static class HttpUtilities
    {
        public const string HttpHeaderPrefix = "ce-";

        public const string SpecVersionHttpHeader = HttpHeaderPrefix + "specversion";

        /// <summary>
        /// Checks whether the given HTTP header name starts with "ce-", and if so, converts it into
        /// a lower-case attribute name.
        /// </summary>
        /// <returns>The corresponding attribute name if the header name matches the CloudEvents header prefix;
        /// null otherwise.</returns>
        public static string GetAttributeNameFromHeaderName(string headerName) =>
            headerName.StartsWith(HttpHeaderPrefix, StringComparison.InvariantCultureIgnoreCase)
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

        public static string EncodeHeaderValue(string value)
        {
            // Apply https://tools.ietf.org/html/rfc3986#section-2.4
            // then https://tools.ietf.org/html/rfc7230#section-3.2.6
            // FIXME: Do this properly, including handling values that contain commas.
            // It's possible that the underlying HTTP libraries do this already, of course...
            // need to use Wireshark to check exactly what's happening on the wire.
            // (Test with headers containing commas, double quotes, surrogate pairs etc)

            // For the moment, encode:
            // % as %25
            // all non-ASCII characters, not worrying about surrogate pairs...
            var builder = new StringBuilder();
            foreach (char c in value)
            {
                if (c < ' ' || c > '\u007f' || c == '%')
                {
                    builder.Append(WebUtility.UrlEncode(c.ToString()));
                }
                else
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

        public static string DecodeHeaderValue(string value)
        {
            // Apply https://tools.ietf.org/html/rfc7230#section-3.2.6 in reverse,
            // then https://tools.ietf.org/html/rfc3986#section-2.4 in reverse


            // FIXME: Do this properly
            // For the moment, just decode all % escapes.
            return WebUtility.UrlDecode(value);
        }
    }
}
