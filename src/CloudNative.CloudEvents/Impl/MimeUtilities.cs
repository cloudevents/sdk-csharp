// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;

namespace CloudNative.CloudEvents.Core
{
    // TODO: Consider this name and namespace carefully. It really does need to be public, as all the event formatters are elsewhere.
    // But it's not ideal...

    /// <summary>
    /// Utility methods around MIME.
    /// </summary>
    public static class MimeUtilities
    {
        // TODO: Should we return null, and force the caller to do the appropriate defaulting?
        /// <summary>
        /// Returns an encoding from a content type, defaulting to UTF-8.
        /// </summary>
        /// <param name="contentType">The content type, or null if no content type is known.</param>
        /// <returns>An encoding suitable for the charset specified in <paramref name="contentType"/>,
        /// or UTF-8 if no charset has been specified.</returns>
        public static Encoding GetEncoding(ContentType contentType) =>
            contentType?.CharSet is string charSet ? Encoding.GetEncoding(charSet) : Encoding.UTF8;

        /// <summary>
        /// Converts a <see cref="MediaTypeHeaderValue"/> into a <see cref="ContentType"/>.
        /// </summary>
        /// <param name="headerValue">The header value to convert. May be null.</param>
        /// <returns>The converted content type, or null if <paramref name="headerValue"/> is null.</returns>
        public static ContentType ToContentType(MediaTypeHeaderValue headerValue) =>
            headerValue is null ? null : new ContentType(headerValue.ToString());

        /// <summary>
        /// Converts a <see cref="ContentType"/> into a <see cref="MediaTypeHeaderValue"/>.
        /// </summary>
        /// <param name="contentType">The content type to convert. May be null.</param>
        /// <returns>The converted media type header value, or null if <paramref name="contentType"/> is null.</returns>
        public static MediaTypeHeaderValue ToMediaTypeHeaderValue(ContentType contentType)
        {
            if (contentType is null)
            {
                return null;
            }
            var header = new MediaTypeHeaderValue(contentType.MediaType);
            foreach (string parameterName in contentType.Parameters.Keys)
            {
                header.Parameters.Add(new NameValueHeaderValue(parameterName, contentType.Parameters[parameterName].ToString()));
            }
            return header;
        }

        /// <summary>
        /// Creates a <see cref="ContentType"/> from the given value, or returns null
        /// if the input is null.
        /// </summary>
        /// <param name="contentType">The content type textual value. May be null.</param>
        /// <returns>The converted content type, or null if <paramref name="contentType"/> is null.</returns>
        public static ContentType CreateContentTypeOrNull(string contentType) =>
            contentType is null ? null : new ContentType(contentType);
    }
}
