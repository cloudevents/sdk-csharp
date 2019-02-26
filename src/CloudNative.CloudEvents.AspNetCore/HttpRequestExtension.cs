// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Primitives;
    using Newtonsoft.Json;
    using System;
    using System.Net.Mime;

    public static class HttpRequestExtension
    {
        const string HttpHeaderPrefix = "ce-";

        const string SpecVersionHttpHeader1 = HttpHeaderPrefix + "cloudEventsVersion";

        const string SpecVersionHttpHeader2 = HttpHeaderPrefix + "specversion";

        static JsonEventFormatter jsonFormatter = new JsonEventFormatter();

        /// <summary>
        /// Converts this HTTP request into a CloudEvent object, with the given extensions.
        /// </summary>
        /// <param name="httpRequest">HTTP request</param>
        /// <param name="extensions">List of extension instances</param>
        /// <returns>A CloudEvent instance or 'null' if the request message doesn't hold a CloudEvent</returns>
        public static CloudEvent ToCloudEvent(this HttpRequest httpRequest,
            params ICloudEventExtension[] extensions)
        {
            return ToCloudEvent(httpRequest, null, extensions);
        }

        /// <summary>
        /// Converts this HTTP request into a CloudEvent object, with the given extensions,
        /// overriding the formatter.
        /// </summary>
        /// <param name="httpRequest">HTTP request</param>
        /// <param name="formatter"></param>
        /// <param name="extensions">List of extension instances</param>
        /// <returns>A CloudEvent instance or 'null' if the request message doesn't hold a CloudEvent</returns>
        public static CloudEvent ToCloudEvent(this HttpRequest httpRequest,
            ICloudEventFormatter formatter = null,
            params ICloudEventExtension[] extensions)
        {
            if (httpRequest.ContentType != null &&
                httpRequest.ContentType.StartsWith(CloudEvent.MediaType,
                    StringComparison.InvariantCultureIgnoreCase))
            {
                // handle structured mode
                if (formatter == null)
                {
                    // if we didn't get a formatter, pick one
                    if (httpRequest.ContentType.EndsWith(JsonEventFormatter.MediaTypeSuffix,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        formatter = jsonFormatter;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unsupported CloudEvents encoding");
                    }
                }

                return formatter.DecodeStructuredEvent(httpRequest.Body, extensions);
            }
            else
            {
                CloudEventsSpecVersion version = CloudEventsSpecVersion.Default;
                if (httpRequest.Headers[SpecVersionHttpHeader1] != StringValues.Empty)
                {
                    version = CloudEventsSpecVersion.V0_1;
                }

                if (httpRequest.Headers[SpecVersionHttpHeader2] != StringValues.Empty)
                {
                    version = httpRequest.Headers[SpecVersionHttpHeader2] == "0.2"
                        ? CloudEventsSpecVersion.V0_2
                        : CloudEventsSpecVersion.Default;
                }

                var cloudEvent = new CloudEvent(version, extensions);
                var attributes = cloudEvent.GetAttributes();
                foreach (var httpRequestHeader in httpRequest.Headers.Keys)
                {
                    if (httpRequestHeader.Equals(SpecVersionHttpHeader1,
                            StringComparison.InvariantCultureIgnoreCase) ||
                        httpRequestHeader.Equals(SpecVersionHttpHeader2, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    if (httpRequestHeader.StartsWith(HttpHeaderPrefix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        string headerValue = httpRequest.Headers[httpRequestHeader];
                        if (headerValue.StartsWith("{") && headerValue.EndsWith("}") ||
                            headerValue.StartsWith("[") && headerValue.EndsWith("]"))
                        {
                            attributes[httpRequestHeader.Substring(3)] =
                                JsonConvert.DeserializeObject(headerValue);
                        }
                        else
                        {
                            attributes[httpRequestHeader.Substring(3)] = headerValue;
                            attributes[httpRequestHeader.Substring(3)] = headerValue;
                        }
                    }
                }

                cloudEvent.ContentType = httpRequest.ContentType != null
                    ? new ContentType(httpRequest.ContentType)
                    : null;
                cloudEvent.Data = httpRequest.Body;
                return cloudEvent;
            }
        }

    }
}
