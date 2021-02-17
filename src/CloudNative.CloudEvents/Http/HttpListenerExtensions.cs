// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.Http
{
    /// <summary>
    /// Extension methods for <see cref="HttpListener"/> and related classes
    /// (<see cref="HttpListenerResponse"/> etc).
    /// </summary>
    public static class HttpListenerExtensions
    {
        // TODO: Check the pattern here. I suspect that cloudEvent.CopyToAsync(response) would be more natural.

        /// <summary>
        /// Copies the CloudEvent into this HttpListenerResponse instance
        /// </summary>
        /// <param name="httpListenerResponse">this</param>
        /// <param name="cloudEvent">CloudEvent to copy</param>
        /// <param name="contentMode">Content mode (structured or binary)</param>
        /// <param name="formatter">Formatter</param>
        /// <returns>Task</returns>
        public static Task CopyFromAsync(this HttpListenerResponse httpListenerResponse, CloudEvent cloudEvent,
            ContentMode contentMode, CloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                var buffer = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
                httpListenerResponse.ContentType = contentType.ToString();
                MapAttributesToListenerResponse(cloudEvent, httpListenerResponse);
                return httpListenerResponse.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            
            // TODO: Check the defaulting to JSON here...
            httpListenerResponse.ContentType = cloudEvent.DataContentType?.ToString() ?? "application/json";
            MapAttributesToListenerResponse(cloudEvent, httpListenerResponse);
            byte[] content = formatter.EncodeBinaryModeEventData(cloudEvent);
            return httpListenerResponse.OutputStream.WriteAsync(content, 0, content.Length);
        }

        // TODO: Do we want this? It's not about CloudEvents...

        /// <summary>
        /// Handle the request as WebHook validation request
        /// </summary>
        /// <param name="context">Request context</param>
        /// <param name="validateOrigin">Callback that returns whether the given origin may push events. If 'null', all origins are acceptable.</param>
        /// <param name="validateRate">Callback that returns the acceptable request rate. If 'null', the rate is not limited.</param>
        /// <returns>Task</returns>
        public static async Task HandleAsWebHookValidationRequest(this HttpListenerContext context,
            Func<string, bool> validateOrigin, Func<string, string> validateRate)
        {
            if (!IsWebHookValidationRequest(context.Request))
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                context.Response.Close();
            }

            var (statusCode, allowedOrigin, allowedRate) = await HttpUtilities.HandleWebHookValidationAsync(context.Request,
                (request, headerName) => request.Headers.Get(headerName), validateOrigin, validateRate);

            context.Response.StatusCode = (int)statusCode;
            if (allowedOrigin is object)
            {
                context.Response.Headers.Add("Allow", "POST");
                context.Response.Headers.Add("WebHook-Allowed-Origin", allowedOrigin);
                if (allowedRate is object)
                {
                    context.Response.Headers.Add("WebHook-Allowed-Rate", allowedRate);
                }
            }
            context.Response.Close();
        }

        /// <summary>
        /// Indicates whether this HttpListenerRequest holds a CloudEvent
        /// </summary>
        public static bool IsCloudEvent(this HttpListenerRequest httpListenerRequest) =>
            HasCloudEventsContentType(httpListenerRequest) ||
            httpListenerRequest.Headers[HttpUtilities.SpecVersionHttpHeader] is object;

        /// <summary>
        /// Indicates whether this HttpListenerRequest is a web hook validation request
        /// </summary>
        public static bool IsWebHookValidationRequest(this HttpListenerRequest httpRequestMessage) =>
            httpRequestMessage.HttpMethod.Equals("options", StringComparison.InvariantCultureIgnoreCase) &&
            httpRequestMessage.Headers["WebHook-Request-Origin"] is object;

        /// <summary>
        /// Converts this listener request into a CloudEvent object, with the given extension attributes.
        /// </summary>
        /// <param name="httpListenerRequest">Listener request</param>
        /// <param name="formatter"></param>
        /// <param name="extensions">List of extension instances</param>
        /// <returns>The CloudEvent corresponding to the given request.</returns>
        /// <exception cref="ArgumentException">The request does not represent a CloudEvent,
        /// or the event's specification version is not supported,
        /// or the event formatter cannot interpret it.</exception>
        public static CloudEvent ToCloudEvent(this HttpListenerRequest httpListenerRequest,
            CloudEventFormatter formatter, params CloudEventAttribute[] extensionAttributes)
        {
            if (HasCloudEventsContentType(httpListenerRequest))
            {
                return formatter.DecodeStructuredModeMessage(httpListenerRequest.InputStream, MimeUtilities.CreateContentTypeOrNull(httpListenerRequest.ContentType), extensionAttributes);
            }
            else
            {
                string versionId = httpListenerRequest.Headers[HttpUtilities.SpecVersionHttpHeader];
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
                var headers = httpListenerRequest.Headers;
                foreach (var key in headers.AllKeys)
                {
                    string attributeName = HttpUtilities.GetAttributeNameFromHeaderName(key);
                    if (attributeName is null || attributeName == CloudEventsSpecVersion.SpecVersionAttribute.Name)
                    {
                        continue;
                    }
                    string attributeValue = HttpUtilities.DecodeHeaderValue(headers[key]);
                    cloudEvent.SetAttributeFromString(attributeName, attributeValue);
                }

                // The data content type should not have been set via a "ce-" header; instead,
                // it's in the regular content type.
                cloudEvent.DataContentType = httpListenerRequest.ContentType;

                formatter.DecodeBinaryModeEventData(BinaryDataUtilities.ToByteArray(httpListenerRequest.InputStream), cloudEvent);
                return cloudEvent;
            }
        }

        private static void MapAttributesToListenerResponse(CloudEvent cloudEvent, HttpListenerResponse httpListenerResponse)
        {
            httpListenerResponse.Headers.Add(HttpUtilities.SpecVersionHttpHeader, HttpUtilities.EncodeHeaderValue(cloudEvent.SpecVersion.VersionId));
            foreach (var attributeAndValue in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = attributeAndValue.Key;
                var value = attributeAndValue.Value;
                if (attribute != cloudEvent.SpecVersion.DataContentTypeAttribute)
                {
                    string headerValue = HttpUtilities.EncodeHeaderValue(attribute.Format(value));
                    httpListenerResponse.Headers.Add(HttpUtilities.HttpHeaderPrefix + attribute.Name, headerValue);
                }
            }
        }

        private static bool HasCloudEventsContentType(HttpListenerRequest request) =>
            request.ContentType is string contentType &&
            contentType.StartsWith(CloudEvent.MediaType, StringComparison.InvariantCultureIgnoreCase);
    }
}
