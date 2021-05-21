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
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.Http
{
    /// <summary>
    /// Extension methods for <see cref="HttpClient"/> and related classes
    /// (<see cref="HttpRequestMessage"/>, <see cref="HttpResponseMessage"/> etc).
    /// </summary>
    public static class HttpClientExtension
    {
        // TODO: CloudEvent.ToHttpRequestMessage?
        // TODO: CloudEvent.ToHttpResponseMessage?

        /// <summary>
        /// Handle the request as WebHook validation request
        /// </summary>
        /// <param name="httpRequestMessage">Request</param>
        /// <param name="validateOrigin">Callback that returns whether the given origin may push events. If 'null', all origins are acceptable.</param>
        /// <param name="validateRate">Callback that returns the acceptable request rate. If 'null', the rate is not limited.</param>
        /// <returns>Response</returns>
        public static async Task<HttpResponseMessage> HandleAsWebHookValidationRequest(
            this HttpRequestMessage httpRequestMessage, Func<string, bool> validateOrigin,
            Func<string, string> validateRate)
        {
            if (!IsWebHookValidationRequest(httpRequestMessage))
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }
            var (statusCode, allowedOrigin, allowedRate) = await HttpUtilities.HandleWebHookValidationAsync(httpRequestMessage,
                (request, headerName) => request.Headers.TryGetValues(headerName, out var values) ? values.FirstOrDefault() : null,
                validateOrigin, validateRate);

            // Note: it's a little odd to create an empty ByteArrayContent, but the Allow header is a content header, so we need content.
            var message = new HttpResponseMessage(statusCode) { Content = new ByteArrayContent(Array.Empty<byte>()) };
            if (allowedOrigin is object)
            {
                message.Content.Headers.Add("Allow", "POST");
                message.Headers.Add("WebHook-Allowed-Origin", allowedOrigin);
                if (allowedRate is object)
                {
                    message.Headers.Add("WebHook-Allowed-Rate", allowedRate);
                }
            }
            return message;
        }

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
        /// Indicates whether this HttpListenerRequest is a web hook validation request
        /// </summary>
        public static bool IsWebHookValidationRequest(this HttpRequestMessage httpRequestMessage) =>
            httpRequestMessage.Method.Method == "OPTIONS" &&
            httpRequestMessage.Headers.Contains("WebHook-Request-Origin");

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

        // TODO: This would include "application/cloudeventsarerubbish" for example...
        private static bool HasCloudEventsContentType(HttpContent content) =>
            MimeUtilities.IsCloudEventsContentType(content?.Headers?.ContentType?.MediaType);

        private static bool HasCloudEventsBatchContentType(HttpContent content) =>
            MimeUtilities.IsCloudEventsBatchContentType(content?.Headers?.ContentType?.MediaType);
    }
}