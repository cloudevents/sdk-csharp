// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Http;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents
{
    public static class HttpRequestExtension
    {
        /// <summary>
        /// Converts this HTTP request into a CloudEvent object, with the given extensions,
        /// overriding the formatter.
        /// </summary>
        /// <param name="httpRequest">HTTP request</param>
        /// <param name="formatter"></param>
        /// <param name="extensions">List of extension instances</param>
        /// <returns>A CloudEvent instance or 'null' if the request message doesn't hold a CloudEvent</returns>
        public static async ValueTask<CloudEvent> ReadCloudEventAsync(this HttpRequest httpRequest,
            ICloudEventFormatter formatter,
            params CloudEventAttribute[] extensionAttributes)
        {
            if (HasCloudEventsContentType(httpRequest))
            {
                // TODO: Handle formatter being null
                return await formatter.DecodeStructuredEventAsync(httpRequest.Body, extensionAttributes).ConfigureAwait(false);
            }
            else
            {
                var headers = httpRequest.Headers;
                CloudEventsSpecVersion version = CloudEventsSpecVersion.Default;
                if (headers.TryGetValue(HttpUtilities.SpecVersionHttpHeader, out var values))
                {
                    string versionId = values.First();
                    version = CloudEventsSpecVersion.FromVersionId(versionId);
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

                cloudEvent.DataContentType = httpRequest.ContentType;
                if (httpRequest.Body is Stream body)
                {
                    // TODO: This is a bit ugly.
                    var memoryStream = new MemoryStream();
                    await body.CopyToAsync(memoryStream).ConfigureAwait(false);
                    if (memoryStream.Length != 0)
                    {
                        cloudEvent.Data = formatter.DecodeData(memoryStream.ToArray(), cloudEvent.DataContentType);
                    }
                }
                return cloudEvent;
            }
        }

        private static bool HasCloudEventsContentType(HttpRequest request) =>
            request?.ContentType is var contentType &&
            contentType.StartsWith(CloudEvent.MediaType, StringComparison.InvariantCultureIgnoreCase);
    }
}
