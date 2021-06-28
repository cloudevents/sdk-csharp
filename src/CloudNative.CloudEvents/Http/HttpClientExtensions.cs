// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.Http
{
    /// <summary>
    /// Extension methods for <see cref="HttpClient"/> and related classes
    /// (<see cref="HttpRequestMessage"/>, <see cref="HttpResponseMessage"/>, <see cref="HttpContent"/> etc).
    /// </summary>
    public static class HttpClientExtensions
    {
        // TODO: CloudEvent.ToHttpRequestMessage?
        // TODO: CloudEvent.ToHttpResponseMessage?

        /// <summary>
        /// Indicates whether this <see cref="HttpRequestMessage"/> holds a single CloudEvent.
        /// </summary>
        /// <remarks>
        /// This method returns false for batch requests, as they need to be parsed differently.
        /// </remarks>
        /// <param name="httpRequestMessage">The message to check for the presence of a CloudEvent. Must not be null.</param>
        /// <returns>true, if the request is a CloudEvent</returns>
        public static bool IsCloudEvent(this HttpRequestMessage httpRequestMessage)
        {
            Validation.CheckNotNull(httpRequestMessage, nameof(httpRequestMessage));
            return HasCloudEventsContentType(httpRequestMessage.Content) ||
                httpRequestMessage.Headers.Contains(HttpUtilities.SpecVersionHttpHeader);
        }

        /// <summary>
        /// Indicates whether this <see cref="HttpResponseMessage"/> holds a single CloudEvent.
        /// </summary>
        /// <param name="httpResponseMessage">The message to check for the presence of a CloudEvent. Must not be null.</param>
        /// <returns>true, if the response is a CloudEvent</returns>
        public static bool IsCloudEvent(this HttpResponseMessage httpResponseMessage)
        {
            Validation.CheckNotNull(httpResponseMessage, nameof(httpResponseMessage));
            return HasCloudEventsContentType(httpResponseMessage.Content) ||
                httpResponseMessage.Headers.Contains(HttpUtilities.SpecVersionHttpHeader);
        }

        /// <summary>
        /// Indicates whether this <see cref="HttpRequestMessage"/> holds a batch of CloudEvents.
        /// </summary>
        /// <param name="httpRequestMessage">The message to check for the presence of a CloudEvent batch. Must not be null.</param>
        /// <returns>true, if the request is a CloudEvent batch</returns>
        public static bool IsCloudEventBatch(this HttpRequestMessage httpRequestMessage)
        {
            Validation.CheckNotNull(httpRequestMessage, nameof(httpRequestMessage));
            return HasCloudEventsBatchContentType(httpRequestMessage.Content);
        }
        
        /// <summary>
        /// Indicates whether this <see cref="HttpResponseMessage"/> holds a batch of CloudEvents.
        /// </summary>
        /// <param name="httpResponseMessage">The message to check for the presence of a CloudEvent batch. Must not be null.</param>
        /// <returns>true, if the response is a CloudEvent batch</returns>
        public static bool IsCloudEventBatch(this HttpResponseMessage httpResponseMessage)
        {
            Validation.CheckNotNull(httpResponseMessage, nameof(httpResponseMessage));
            return HasCloudEventsBatchContentType(httpResponseMessage.Content);
        }

        /// <summary>
        /// Converts this HTTP response message into a CloudEvent object
        /// </summary>
        /// <param name="httpResponseMessage">The HTTP response message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static Task<CloudEvent> ToCloudEventAsync(
            this HttpResponseMessage httpResponseMessage,
            CloudEventFormatter formatter,
            params CloudEventAttribute[] extensionAttributes) =>
            ToCloudEventAsync(httpResponseMessage, formatter, (IEnumerable<CloudEventAttribute>) extensionAttributes);

        /// <summary>
        /// Converts this HTTP response message into a CloudEvent object
        /// </summary>
        /// <param name="httpResponseMessage">The HTTP response message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static Task<CloudEvent> ToCloudEventAsync(
            this HttpResponseMessage httpResponseMessage,
            CloudEventFormatter formatter,
            IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            Validation.CheckNotNull(httpResponseMessage, nameof(httpResponseMessage));
            return ToCloudEventInternalAsync(httpResponseMessage.Headers, httpResponseMessage.Content, formatter, extensionAttributes, nameof(httpResponseMessage));
        }

        /// <summary>
        /// Converts this HTTP request message into a CloudEvent object.
        /// </summary>
        /// <param name="httpRequestMessage">The HTTP request message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static Task<CloudEvent> ToCloudEventAsync(
            this HttpRequestMessage httpRequestMessage,
            CloudEventFormatter formatter,
            params CloudEventAttribute[] extensionAttributes) =>
            ToCloudEventAsync(httpRequestMessage, formatter, (IEnumerable<CloudEventAttribute>) extensionAttributes);

        /// <summary>
        /// Converts this HTTP request message into a CloudEvent object.
        /// </summary>
        /// <param name="httpRequestMessage">The HTTP request message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static Task<CloudEvent> ToCloudEventAsync(
            this HttpRequestMessage httpRequestMessage,
            CloudEventFormatter formatter,
            IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            Validation.CheckNotNull(httpRequestMessage, nameof(httpRequestMessage));
            return ToCloudEventInternalAsync(httpRequestMessage.Headers, httpRequestMessage.Content, formatter, extensionAttributes, nameof(httpRequestMessage));
        }

        private static async Task<CloudEvent> ToCloudEventInternalAsync(HttpHeaders headers, HttpContent content,
            CloudEventFormatter formatter, IEnumerable<CloudEventAttribute> extensionAttributes, string paramName)
        {
            Validation.CheckNotNull(formatter, nameof(formatter));

            if (HasCloudEventsContentType(content))
            {
                var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
                return await formatter.DecodeStructuredModeMessageAsync(stream, MimeUtilities.ToContentType(content.Headers.ContentType), extensionAttributes).ConfigureAwait(false);
            }
            else
            {
                string versionId = headers.Contains(HttpUtilities.SpecVersionHttpHeader)
                    ? headers.GetValues(HttpUtilities.SpecVersionHttpHeader).First()
                    : null;
                if (versionId is null)
                {
                    throw new ArgumentException($"Request does not represent a CloudEvent. It has neither a {HttpUtilities.SpecVersionHttpHeader} header, nor a suitable content type.", nameof(paramName));
                }
                var version = CloudEventsSpecVersion.FromVersionId(versionId)
                    ?? throw new ArgumentException($"Unknown CloudEvents spec version '{versionId}'", paramName);

                var cloudEvent = new CloudEvent(version, extensionAttributes);
                foreach (var header in headers)
                {
                    string attributeName = HttpUtilities.GetAttributeNameFromHeaderName(header.Key);
                    if (attributeName is null || attributeName == CloudEventsSpecVersion.SpecVersionAttribute.Name)
                    {
                        continue;
                    }
                    string attributeValue = HttpUtilities.DecodeHeaderValue(header.Value.First());

                    cloudEvent.SetAttributeFromString(attributeName, attributeValue);
                }
                if (content is object)
                {
                    // TODO: Should this just be the media type? We probably need to take a full audit of this...
                    cloudEvent.DataContentType = content.Headers?.ContentType?.ToString();
                    var data = await content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    formatter.DecodeBinaryModeEventData(data, cloudEvent);
                }
                return Validation.CheckCloudEventArgument(cloudEvent, paramName);
            }
        }

        /// <summary>
        /// Converts this HTTP response message into a CloudEvent object
        /// </summary>
        /// <param name="httpResponseMessage">The HTTP response message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static Task<IReadOnlyList<CloudEvent>> ToCloudEventBatchAsync(
            this HttpResponseMessage httpResponseMessage,
            CloudEventFormatter formatter,
            params CloudEventAttribute[] extensionAttributes) =>
            ToCloudEventBatchAsync(httpResponseMessage, formatter, (IEnumerable<CloudEventAttribute>) extensionAttributes);

        /// <summary>
        /// Converts this HTTP response message into a CloudEvent object
        /// </summary>
        /// <param name="httpResponseMessage">The HTTP response message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static Task<IReadOnlyList<CloudEvent>> ToCloudEventBatchAsync(
            this HttpResponseMessage httpResponseMessage,
            CloudEventFormatter formatter,
            IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            Validation.CheckNotNull(httpResponseMessage, nameof(httpResponseMessage));
            return ToCloudEventBatchInternalAsync(httpResponseMessage.Content, formatter, extensionAttributes, nameof(httpResponseMessage));
        }

        /// <summary>
        /// Converts this HTTP request message into a CloudEvent object.
        /// </summary>
        /// <param name="httpRequestMessage">The HTTP request message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static Task<IReadOnlyList<CloudEvent>> ToCloudEventBatchAsync(
            this HttpRequestMessage httpRequestMessage,
            CloudEventFormatter formatter,
            params CloudEventAttribute[] extensionAttributes) =>
            ToCloudEventBatchAsync(httpRequestMessage, formatter, (IEnumerable<CloudEventAttribute>) extensionAttributes);

        /// <summary>
        /// Converts this HTTP request message into a CloudEvent object.
        /// </summary>
        /// <param name="httpRequestMessage">The HTTP request message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A reference to a validated CloudEvent instance.</returns>
        public static Task<IReadOnlyList<CloudEvent>> ToCloudEventBatchAsync(
            this HttpRequestMessage httpRequestMessage,
            CloudEventFormatter formatter,
            IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            Validation.CheckNotNull(httpRequestMessage, nameof(httpRequestMessage));
            return ToCloudEventBatchInternalAsync(httpRequestMessage.Content, formatter, extensionAttributes, nameof(httpRequestMessage));
        }

        private static async Task<IReadOnlyList<CloudEvent>> ToCloudEventBatchInternalAsync(HttpContent content,
            CloudEventFormatter formatter, IEnumerable<CloudEventAttribute> extensionAttributes, string paramName)
        {
            Validation.CheckNotNull(formatter, nameof(formatter));

            if (HasCloudEventsBatchContentType(content))
            {
                var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
                return await formatter
                    .DecodeBatchModeMessageAsync(stream, MimeUtilities.ToContentType(content.Headers.ContentType), extensionAttributes)
                    .ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException("HTTP message does not represent a CloudEvents batch.", paramName);
            }
        }

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
            ByteArrayContent ret = ToByteArrayContent(content);
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

        /// <summary>
        /// Converts a CloudEvent batch to <see cref="HttpContent"/>.
        /// </summary>
        /// <param name="cloudEvents">The CloudEvent batch to convert. Must not be null, and every element must be non-null reference to a valid CloudEvent.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        public static HttpContent ToHttpContent(this IReadOnlyList<CloudEvent> cloudEvents, CloudEventFormatter formatter)
        {
            Validation.CheckCloudEventBatchArgument(cloudEvents, nameof(cloudEvents));
            Validation.CheckNotNull(formatter, nameof(formatter));

            // TODO: Validate that all events in the batch have the same version?
            // See https://github.com/cloudevents/spec/issues/807

            ReadOnlyMemory<byte> content = formatter.EncodeBatchModeMessage(cloudEvents, out var contentType);

            // Note: we don't populate any other headers for batch mode.
            var ret = ToByteArrayContent(content);
            ret.Headers.ContentType = MimeUtilities.ToMediaTypeHeaderValue(contentType);
            return ret;
        }

        private static ByteArrayContent ToByteArrayContent(ReadOnlyMemory<byte> content) =>
            MemoryMarshal.TryGetArray(content, out var segment)
            ? new ByteArrayContent(segment.Array, segment.Offset, segment.Count)
            // TODO: Just throw?
            : new ByteArrayContent(content.ToArray());

        // TODO: This would include "application/cloudeventsarerubbish" for example...
        private static bool HasCloudEventsContentType(HttpContent content) =>
            MimeUtilities.IsCloudEventsContentType(content?.Headers?.ContentType?.MediaType);

        private static bool HasCloudEventsBatchContentType(HttpContent content) =>
            MimeUtilities.IsCloudEventsBatchContentType(content?.Headers?.ContentType?.MediaType);
    }
}