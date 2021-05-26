// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.Http
{
    /// <summary>
    /// Extension methods for <see cref="HttpWebRequest"/> and related types.
    /// </summary>
    public static class HttpWebExtensions
    {
        // TODO: HttpWebResponse as well?

        /// <summary>
        /// Copies a <see cref="CloudEvent"/> into the specified <see cref="HttpWebRequest"/>.
        /// </summary>
        /// <param name="cloudEvent">CloudEvent to copy. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="destination">The request to populate. Must not be null.</param>
        /// <param name="contentMode">Content mode (structured or binary)</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CopyToHttpWebRequestAsync(this CloudEvent cloudEvent, HttpWebRequest destination,
            ContentMode contentMode, CloudEventFormatter formatter)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
            Validation.CheckNotNull(destination, nameof(destination));
            Validation.CheckNotNull(formatter, nameof(formatter));

            ReadOnlyMemory<byte> content;
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

            if (contentType is object)
            {
                destination.ContentType = contentType.ToString();
            }
            else if (content.Length != 0)
            {
                throw new ArgumentException(Strings.ErrorContentTypeUnspecified, nameof(cloudEvent));
            }

            // Map headers in either mode.
            // Including the headers in structured mode is optional in the spec (as they're already within the body) but
            // can be useful.            
            destination.Headers.Add(HttpUtilities.SpecVersionHttpHeader, HttpUtilities.EncodeHeaderValue(cloudEvent.SpecVersion.VersionId));
            foreach (var attributeAndValue in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = attributeAndValue.Key;
                var value = attributeAndValue.Value;
                if (attribute != cloudEvent.SpecVersion.DataContentTypeAttribute)
                {
                    string headerValue = HttpUtilities.EncodeHeaderValue(attribute.Format(value));
                    destination.Headers.Add(HttpUtilities.HttpHeaderPrefix + attribute.Name, headerValue);
                }
            }
            await BinaryDataUtilities.CopyToStreamAsync(content, destination.GetRequestStream()).ConfigureAwait(false);
        }

        /// <summary>
        /// Copies a <see cref="CloudEvent"/> batch into the specified <see cref="HttpWebRequest"/>.
        /// </summary>
        /// <param name="cloudEvents">CloudEvent batch to copy. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="destination">The request to populate. Must not be null.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CopyToHttpWebRequestAsync(this IReadOnlyList<CloudEvent> cloudEvents,
            HttpWebRequest destination,
            CloudEventFormatter formatter)
        {
            Validation.CheckCloudEventBatchArgument(cloudEvents, nameof(cloudEvents));
            Validation.CheckNotNull(destination, nameof(destination));
            Validation.CheckNotNull(formatter, nameof(formatter));

            ReadOnlyMemory<byte> content = formatter.EncodeBatchModeMessage(cloudEvents, out var contentType);
            destination.ContentType = contentType.ToString();
            await BinaryDataUtilities.CopyToStreamAsync(content, destination.GetRequestStream()).ConfigureAwait(false);
        }
    }
}
