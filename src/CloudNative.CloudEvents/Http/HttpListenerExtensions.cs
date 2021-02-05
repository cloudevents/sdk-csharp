// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net;
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
            ContentMode contentMode, ICloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                var buffer = formatter.EncodeStructuredEvent(cloudEvent, out var contentType);
                httpListenerResponse.ContentType = contentType.ToString();
                MapAttributesToListenerResponse(cloudEvent, httpListenerResponse);
                return httpListenerResponse.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }

            Stream stream = HttpUtilities.MapDataAttributeToStream(cloudEvent, formatter);
            // TODO: Check the defaulting to JSON here...
            httpListenerResponse.ContentType = cloudEvent.DataContentType?.ToString() ?? "application/json";
            MapAttributesToListenerResponse(cloudEvent, httpListenerResponse);
            return stream.CopyToAsync(httpListenerResponse.OutputStream);
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
        /// <returns>A CloudEvent instance or 'null' if the request message doesn't hold a CloudEvent</returns>
        public static CloudEvent ToCloudEvent(this HttpListenerRequest httpListenerRequest,
            ICloudEventFormatter formatter, params CloudEventAttribute[] extensionAttributes)
        {
            if (HasCloudEventsContentType(httpListenerRequest))
            {
                // FIXME: Handle no formatter being specified.
                return formatter.DecodeStructuredEvent(httpListenerRequest.InputStream, extensionAttributes);
            }
            else
            {
                CloudEventsSpecVersion version = CloudEventsSpecVersion.Default;
                if (httpListenerRequest.Headers[HttpUtilities.SpecVersionHttpHeader] is string versionId)
                {
                    version = CloudEventsSpecVersion.FromVersionId(versionId);
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

                // TODO: Check that this doesn't come through as a header already
                cloudEvent.DataContentType = httpListenerRequest.ContentType;

                // TODO: This is a bit ugly.
                var memoryStream = new MemoryStream();
                httpListenerRequest.InputStream.CopyTo(memoryStream);
                if (memoryStream.Length != 0)
                {
                    cloudEvent.Data = formatter.DecodeData(memoryStream.ToArray(), cloudEvent.DataContentType);
                }
                return cloudEvent;
            }
        }

        private static void MapAttributesToListenerResponse(CloudEvent cloudEvent, HttpListenerResponse httpListenerResponse)
        {
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
