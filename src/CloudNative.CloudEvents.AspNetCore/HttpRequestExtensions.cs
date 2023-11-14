// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.Http;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.AspNetCore
{
    /// <summary>
    /// Extension methods to convert between HTTP requests and CloudEvents.
    /// </summary>
    public static class HttpRequestExtensions
    {
        // TODO: CopyToHttpRequest, and deal with HttpResponse as well.

        /// <summary>
        /// Indicates whether this <see cref="HttpRequest"/> holds a single CloudEvent.
        /// </summary>
        /// <remarks>
        /// This method returns false for batch requests, as they need to be parsed differently.
        /// </remarks>
        /// <param name="httpRequest">The request to check for the presence of a CloudEvent. Must not be null.</param>
        /// <returns>true, if the request is a CloudEvent</returns>
        public static bool IsCloudEvent(this HttpRequest httpRequest) =>
            httpRequest.Headers.ContainsKey(HttpUtilities.SpecVersionHttpHeader) ||
            HasCloudEventsContentType(httpRequest);

        /// <summary>
        /// Indicates whether this <see cref="HttpRequest"/> holds a batch of CloudEvents.
        /// </summary>
        /// <param name="httpRequest">The request to check for the presence of a CloudEvent batch. Must not be null.</param>
        /// <returns>true, if the request is a CloudEvent batch</returns>
        public static bool IsCloudEventBatch(this HttpRequest httpRequest) =>
            HasCloudEventsBatchContentType(httpRequest);

        /// <summary>
        /// Converts this HTTP request into a CloudEvent object.
        /// </summary>
        /// <param name="httpRequest">The HTTP request to decode. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to process the request body. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The decoded CloudEvent.</returns>
        /// <exception cref="ArgumentException">The request does not contain a CloudEvent.</exception>
        public static Task<CloudEvent> ToCloudEventAsync(
            this HttpRequest httpRequest,
            CloudEventFormatter formatter,
            params CloudEventAttribute[]? extensionAttributes) =>
            ToCloudEventAsync(httpRequest, formatter, (IEnumerable<CloudEventAttribute>?) extensionAttributes);

        /// <summary>
        /// Converts this HTTP request into a CloudEvent object.
        /// </summary>
        /// <param name="httpRequest">The HTTP request to decode. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to process the request body. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The decoded CloudEvent.</returns>
        /// <exception cref="ArgumentException">The request does not contain a CloudEvent.</exception>
        public static async Task<CloudEvent> ToCloudEventAsync(
            this HttpRequest httpRequest,
            CloudEventFormatter formatter,
            IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(httpRequest, nameof(httpRequest));
            Validation.CheckNotNull(formatter, nameof(formatter));
            if (HasCloudEventsContentType(httpRequest))
            {
                var contentType = MimeUtilities.CreateContentTypeOrNull(httpRequest.ContentType);
                return await formatter.DecodeStructuredModeMessageAsync(httpRequest.Body, contentType, extensionAttributes).ConfigureAwait(false);
            }
            else
            {
                var headers = httpRequest.Headers;
                headers.TryGetValue(HttpUtilities.SpecVersionHttpHeader, out var versionId);
                if (versionId.Count == 0)
                {
                    throw new ArgumentException($"Request does not represent a CloudEvent. It has neither a {HttpUtilities.SpecVersionHttpHeader} header, nor a suitable content type.", nameof(httpRequest));
                }
                var version = CloudEventsSpecVersion.FromVersionId(versionId.FirstOrDefault())
                    ?? throw new ArgumentException($"Unknown CloudEvents spec version '{versionId}'", nameof(httpRequest));

                if (version is null)
                {
                    throw new ArgumentException($"Unsupported CloudEvents spec version '{versionId.First()}'");
                }

                var cloudEvent = new CloudEvent(version, extensionAttributes);
                foreach (var header in headers)
                {
                    string? attributeName = HttpUtilities.GetAttributeNameFromHeaderName(header.Key);
                    if (attributeName is null || attributeName == CloudEventsSpecVersion.SpecVersionAttribute.Name)
                    {
                        continue;
                    }
                    string attributeValue = HttpUtilities.DecodeHeaderValue(header.Value.First());

                    cloudEvent.SetAttributeFromString(attributeName, attributeValue);
                }

                cloudEvent.DataContentType = httpRequest.ContentType;
                if (httpRequest.Body is Stream body)
                {
                    ReadOnlyMemory<byte> data = await BinaryDataUtilities.ToReadOnlyMemoryAsync(body).ConfigureAwait(false);
                    formatter.DecodeBinaryModeEventData(data, cloudEvent);
                }
                return Validation.CheckCloudEventArgument(cloudEvent, nameof(httpRequest));
            }
        }

        /// <summary>
        /// Converts this HTTP request into a batch of CloudEvents.
        /// </summary>
        /// <param name="httpRequest">The HTTP request to decode. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to process the request body. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvents. May be null.</param>
        /// <returns>The decoded batch of CloudEvents.</returns>
        /// <exception cref="ArgumentException">The request does not contain a CloudEvent batch.</exception>
        public static Task<IReadOnlyList<CloudEvent>> ToCloudEventBatchAsync(
            this HttpRequest httpRequest,
            CloudEventFormatter formatter,
            params CloudEventAttribute[]? extensionAttributes) =>
            ToCloudEventBatchAsync(httpRequest, formatter, (IEnumerable<CloudEventAttribute>?) extensionAttributes);

        /// <summary>
        /// Converts this HTTP request into a batch of CloudEvents.
        /// </summary>
        /// <param name="httpRequest">The HTTP request to decode. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to process the request body. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvents. May be null.</param>
        /// <returns>The decoded batch of CloudEvents.</returns>
        /// <exception cref="ArgumentException">The request does not contain a CloudEvent batch.</exception>
        public static async Task<IReadOnlyList<CloudEvent>> ToCloudEventBatchAsync(
            this HttpRequest httpRequest,
            CloudEventFormatter formatter,
            IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(httpRequest, nameof(httpRequest));
            Validation.CheckNotNull(formatter, nameof(formatter));

            if (HasCloudEventsBatchContentType(httpRequest))
            {
                var contentType = MimeUtilities.CreateContentTypeOrNull(httpRequest.ContentType);
                return await formatter.DecodeBatchModeMessageAsync(httpRequest.Body, contentType, extensionAttributes).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException("HTTP message does not represent a CloudEvents batch.", nameof(httpRequest));
            }
        }

        private static bool HasCloudEventsContentType(HttpRequest request) =>
            MimeUtilities.IsCloudEventsContentType(request?.ContentType);

        private static bool HasCloudEventsBatchContentType(HttpRequest request) =>
            MimeUtilities.IsCloudEventsBatchContentType(request?.ContentType);
    }
}
