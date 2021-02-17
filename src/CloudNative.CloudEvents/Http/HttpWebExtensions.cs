// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.Http
{
    /// <summary>
    /// Extension methods for <see cref="HttpWebRequest"/> and related types.
    /// </summary>
    public static class HttpWebExtensions
    {
        // TODO: HttpWebResponse as well?
        // TODO: Change to a CopyTo extension method on CloudEvent?

        /// <summary>
        /// Copies a CloudEvent into the specified HttpWebRequest instance.
        /// </summary>
        /// <param name="httpWebRequest">The request to populate.</param>
        /// <param name="cloudEvent">CloudEvent to copy</param>
        /// <param name="contentMode">Content mode (structured or binary)</param>
        /// <param name="formatter">Formatter</param>
        /// <returns>Task</returns>
        public static async Task CopyFromAsync(this HttpWebRequest httpWebRequest, CloudEvent cloudEvent,
            ContentMode contentMode, CloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                var buffer = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
                httpWebRequest.ContentType = contentType.ToString();
                await httpWebRequest.GetRequestStream().WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                return;
            }

            httpWebRequest.ContentType = cloudEvent.DataContentType?.ToString() ?? "application/json";
            MapAttributesToWebRequest(cloudEvent, httpWebRequest);
            byte[] content = formatter.EncodeBinaryModeEventData(cloudEvent);
            await httpWebRequest.GetRequestStream().WriteAsync(content, 0, content.Length).ConfigureAwait(false);
        }

        static void MapAttributesToWebRequest(CloudEvent cloudEvent, HttpWebRequest httpWebRequest)
        {
            httpWebRequest.Headers.Add(HttpUtilities.SpecVersionHttpHeader, HttpUtilities.EncodeHeaderValue(cloudEvent.SpecVersion.VersionId));
            foreach (var attributeAndValue in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = attributeAndValue.Key;
                var value = attributeAndValue.Value;
                if (attribute != cloudEvent.SpecVersion.DataContentTypeAttribute)
                {
                    string headerValue = HttpUtilities.EncodeHeaderValue(attribute.Format(value));
                    httpWebRequest.Headers.Add(HttpUtilities.HttpHeaderPrefix + attribute.Name, headerValue);
                }
            }
        }
    }
}
