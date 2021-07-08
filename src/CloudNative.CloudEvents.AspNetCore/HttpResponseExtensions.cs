// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.Http;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.AspNetCore
{
    /// <summary>
    /// Extension methods to convert between HTTP responses and CloudEvents.
    /// </summary>
    public static class HttpResponseExtensions
    {
        /// <summary>
        /// Copies a <see cref="CloudEvent"/> into an <see cref="HttpResponse" />.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to copy. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="destination">The response to copy the CloudEvent to. Must not be null.</param>
        /// <param name="contentMode">Content mode (structured or binary)</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CopyToHttpResponseAsync(this CloudEvent cloudEvent, HttpResponse destination,
            ContentMode contentMode, CloudEventFormatter formatter)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
            Validation.CheckNotNull(destination, nameof(destination));
            Validation.CheckNotNull(formatter, nameof(formatter));

            ReadOnlyMemory<byte> content;
            ContentType? contentType;
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
                throw new ArgumentException("The 'datacontenttype' attribute value must be specified", nameof(cloudEvent));
            }

            // Map headers in either mode.
            // Including the headers in structured mode is optional in the spec (as they're already within the body) but
            // can be useful.            
            destination.Headers.Add(HttpUtilities.SpecVersionHttpHeader, HttpUtilities.EncodeHeaderValue(cloudEvent.SpecVersion.VersionId));
            foreach (var attributeAndValue in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = attributeAndValue.Key;
                var value = attributeAndValue.Value;
                // The content type is already handled based on the content mode.
                if (attribute != cloudEvent.SpecVersion.DataContentTypeAttribute)
                {
                    string headerValue = HttpUtilities.EncodeHeaderValue(attribute.Format(value));
                    destination.Headers.Add(HttpUtilities.HttpHeaderPrefix + attribute.Name, headerValue);
                }
            }

            destination.ContentLength = content.Length;
            await BinaryDataUtilities.CopyToStreamAsync(content, destination.Body).ConfigureAwait(false);
        }

        /// <summary>
        /// Copies a <see cref="CloudEvent"/> batch into an <see cref="HttpResponse" />.
        /// </summary>
        /// <param name="cloudEvents">The CloudEvent batch to copy. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="destination">The response to copy the CloudEvent to. Must not be null.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CopyToHttpResponseAsync(this IReadOnlyList<CloudEvent> cloudEvents,
            HttpResponse destination, CloudEventFormatter formatter)
        {
            Validation.CheckCloudEventBatchArgument(cloudEvents, nameof(cloudEvents));
            Validation.CheckNotNull(destination, nameof(destination));
            Validation.CheckNotNull(formatter, nameof(formatter));

            // TODO: Validate that all events in the batch have the same version?
            // See https://github.com/cloudevents/spec/issues/807

            ReadOnlyMemory<byte> content = formatter.EncodeBatchModeMessage(cloudEvents, out var contentType);
            destination.ContentType = contentType.ToString();
            destination.ContentLength = content.Length;
            await BinaryDataUtilities.CopyToStreamAsync(content, destination.Body).ConfigureAwait(false);
        }
    }
}
