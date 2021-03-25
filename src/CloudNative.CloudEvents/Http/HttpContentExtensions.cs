// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Net.Http;
using System.Net.Mime;

namespace CloudNative.CloudEvents.Http
{
    /// <summary>
    /// CloudEvent extension methods related to <see cref="HttpContent"/>.
    /// </summary>
    public static class HttpContentExtensions
    {
        /// <summary>
        /// Converts a CloudEvent to <see cref="HttpContent"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        public static HttpContent ToHttpContent(this CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
            Validation.CheckNotNull(formatter, nameof(formatter));

            byte[] content;
            // The content type to include in the ContentType header - may be the data content type, or the formatter's content type.
            ContentType contentType;
            switch (contentMode)
            {
                case ContentMode.Structured:
                    content = formatter.EncodeStructuredModeMessage(cloudEvent, out contentType);
                    break;
                case ContentMode.Binary:
                    content = formatter.EncodeBinaryModeEventData(cloudEvent);
                    contentType = MimeUtilities.CreateContentTypeOrNull(cloudEvent.DataContentType);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contentMode), $"Unsupported content mode: {contentMode}");
            }
            var ret = new ByteArrayContent(content);
            if (contentType is object)
            {
                ret.Headers.ContentType = MimeUtilities.ToMediaTypeHeaderValue(contentType);
            }
            else if (content.Length != 0)
            {
                throw new ArgumentException(Strings.ErrorContentTypeUnspecified, nameof(cloudEvent));
            }

            // Map headers in either mode.
            // Including the headers in structured mode is optional in the spec (as they're already within the body) but
            // can be useful.
            ret.Headers.Add(HttpUtilities.SpecVersionHttpHeader, HttpUtilities.EncodeHeaderValue(cloudEvent.SpecVersion.VersionId));
            foreach (var attributeAndValue in cloudEvent.GetPopulatedAttributes())
            {
                CloudEventAttribute attribute = attributeAndValue.Key;
                string headerName = HttpUtilities.HttpHeaderPrefix + attribute.Name;
                object value = attributeAndValue.Value;

                // Skip the data content type attribute in binary mode, because it's already in the content type header.
                if (attribute == cloudEvent.SpecVersion.DataContentTypeAttribute && contentMode == ContentMode.Binary)
                {
                    continue;
                }
                else
                {
                    string headerValue = HttpUtilities.EncodeHeaderValue(attribute.Format(value));
                    ret.Headers.Add(headerName, headerValue);
                }
            }
            return ret;
        }
    }
}
