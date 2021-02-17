// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

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

        // TODO: What does this (web hook validation) have to do with CloudEvents?
        // Well, it's specced in the same repo: https://github.com/cloudevents/spec/blob/v1.0.1/http-webhook.md

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

            var message = new HttpResponseMessage(statusCode);
            if (allowedOrigin is object)
            {
                message.Headers.Add("Allow", "POST");
                message.Headers.Add("WebHook-Allowed-Origin", allowedOrigin);
                if (allowedRate is object)
                {
                    message.Headers.Add("WebHook-Allowed-Rate", allowedRate);
                }
            }
            return message;
        }

        /// <summary>
        /// Indicates whether this HttpResponseMessage holds a CloudEvent
        /// </summary>
        /// <param name="httpResponseMessage"></param>
        /// <returns>true, if the response is a CloudEvent</returns>
        public static bool IsCloudEvent(this HttpResponseMessage httpResponseMessage) =>
            HasCloudEventsContentType(httpResponseMessage.Content) ||
            httpResponseMessage.Headers.Contains(HttpUtilities.SpecVersionHttpHeader);

        /// <summary>
        /// Indicates whether this HttpListenerRequest is a web hook validation request
        /// </summary>
        public static bool IsWebHookValidationRequest(this HttpRequestMessage httpRequestMessage) =>
            httpRequestMessage.Method.Method.Equals("options", StringComparison.InvariantCultureIgnoreCase) &&
            httpRequestMessage.Headers.Contains("WebHook-Request-Origin");

        /// <summary>
        /// Converts this response message into a CloudEvent object, with the given extensions and
        /// overriding the default formatter.
        /// </summary>
        /// <param name="httpResponseMessage">Response message.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent.</param>
        /// <returns>A CloudEvent instance or 'null' if the response message doesn't hold a CloudEvent</returns>
        public static Task<CloudEvent> ToCloudEventAsync(this HttpResponseMessage httpResponseMessage,
            CloudEventFormatter formatter, params CloudEventAttribute[] extensionAttributes) =>
            ToCloudEventInternalAsync(httpResponseMessage.Headers, httpResponseMessage.Content, formatter, extensionAttributes);

        /// <summary>
        /// Converts this HTTP request message into a CloudEvent object, with the given extension attributes.
        /// </summary>
        /// <param name="httpRequestMessage">HTTP request message</param>
        /// <param name="formatter"></param>
        /// <param name="extensionAttributes">List of extension instances</param>
        /// <returns>A CloudEvent instance or 'null' if the request message doesn't hold a CloudEvent</returns>
        public static Task<CloudEvent> ToCloudEventAsync(this HttpRequestMessage httpRequestMessage,
            CloudEventFormatter formatter,
            params CloudEventAttribute[] extensionAttributes) =>
            ToCloudEventInternalAsync(httpRequestMessage.Headers, httpRequestMessage.Content, formatter, extensionAttributes);

        private static async Task<CloudEvent> ToCloudEventInternalAsync(HttpHeaders headers, HttpContent content,
            CloudEventFormatter formatter, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            if (HasCloudEventsContentType(content))
            {
                // FIXME: Handle no formatter being specified.
                var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
                return await formatter.DecodeStructuredEventAsync(stream, extensionAttributes).ConfigureAwait(false);
            }
            else
            {
                string versionId = headers.Contains(HttpUtilities.SpecVersionHttpHeader)
                    ? headers.GetValues(HttpUtilities.SpecVersionHttpHeader).First()
                    : null;
                if (versionId is null)
                {
                    throw new ArgumentException("Request is not a CloudEvent");
                }
                var version = CloudEventsSpecVersion.FromVersionId(versionId);
                if (version is null)
                {
                    throw new ArgumentException($"Unsupported CloudEvents spec version '{versionId}'");
                }

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
                    cloudEvent.Data = formatter.DecodeData(data, cloudEvent.DataContentType);
                }
                return cloudEvent;
            }
        }

        // TODO: This would include "application/cloudeventsarerubbish" for example...
        private static bool HasCloudEventsContentType(HttpContent content) =>
            content?.Headers?.ContentType is var contentType &&
            contentType.MediaType.StartsWith(CloudEvent.MediaType, StringComparison.InvariantCultureIgnoreCase);
    }
}