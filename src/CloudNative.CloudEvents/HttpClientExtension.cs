// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Mime;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public static class HttpClientExtension
    {
        const string HttpHeaderPrefix = "ce-";
        const string SpecVersionHttpHeader = HttpHeaderPrefix + "specversion";
        static JsonEventFormatter jsonFormatter = new JsonEventFormatter();

        public static Task CopyFromAsync(this HttpListenerResponse httpListenerResponse, CloudEvent cloudEvent, ContentMode contentMode, ICloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                var buffer = formatter.EncodeStructuredEvent(cloudEvent, out var contentType, cloudEvent.Extensions.Values);
                httpListenerResponse.ContentType = contentType.ToString();
                MapAttributesToListenerResponse(cloudEvent, httpListenerResponse);
                return httpListenerResponse.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }

            Stream stream;
            if (cloudEvent.Data is byte[])
            {
                stream = new MemoryStream((byte[])cloudEvent.Data);
            }
            else if (cloudEvent.Data is Stream)
            {
                stream = (Stream)cloudEvent.Data;
            }
            else
            {
                stream = new MemoryStream(formatter.EncodeAttribute(CloudEventAttributes.DataAttributeName, cloudEvent.Data, cloudEvent.Extensions.Values));
            }
            httpListenerResponse.ContentType = cloudEvent.ContentType.ToString();
            MapAttributesToListenerResponse(cloudEvent, httpListenerResponse);
            return stream.CopyToAsync(httpListenerResponse.OutputStream);
        }

        public static async Task CopyFromAsync(this HttpWebRequest httpWebRequest, CloudEvent cloudEvent, ContentMode contentMode, ICloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                var buffer = formatter.EncodeStructuredEvent(cloudEvent, out var contentType, cloudEvent.Extensions.Values);
                httpWebRequest.ContentType = contentType.ToString();
                MapAttributesToWebRequest(cloudEvent, httpWebRequest);
                await (httpWebRequest.GetRequestStream()).WriteAsync(buffer, 0, buffer.Length);
                return;
            }

            Stream stream;
            if (cloudEvent.Data is byte[])
            {
                stream = new MemoryStream((byte[])cloudEvent.Data);
            }
            else if (cloudEvent.Data is Stream)
            {
                stream = (Stream)cloudEvent.Data;
            }
            else
            {
                stream = new MemoryStream(formatter.EncodeAttribute(CloudEventAttributes.DataAttributeName, cloudEvent.Data, cloudEvent.Extensions.Values));
            }
            httpWebRequest.ContentType = cloudEvent.ContentType.ToString();
            MapAttributesToWebRequest(cloudEvent, httpWebRequest);
            await stream.CopyToAsync(httpWebRequest.GetRequestStream());
        }

        static void MapAttributesToListenerResponse(CloudEvent cloudEvent, HttpListenerResponse httpListenerResponse)
        {
            foreach (var attribute in cloudEvent.GetAttributes())
            {
                switch (attribute.Key)
                {
                    case CloudEventAttributes.ContentTypeAttributeName:
                        break;
                    default:
                        httpListenerResponse.Headers.Add(HttpHeaderPrefix + attribute.Key,
                            Encoding.UTF8.GetString(jsonFormatter.EncodeAttribute(attribute.Key, attribute.Value, cloudEvent.Extensions.Values)));
                        break;
                }
            }
        }

        static void MapAttributesToWebRequest(CloudEvent cloudEvent, HttpWebRequest httpWebRequest)
        {
            foreach (var attribute in cloudEvent.GetAttributes())
            {
                switch (attribute.Key)
                {
                    case CloudEventAttributes.ContentTypeAttributeName:
                        break;
                    default:
                        httpWebRequest.Headers.Add(HttpHeaderPrefix + attribute.Key,
                            Encoding.UTF8.GetString(jsonFormatter.EncodeAttribute(attribute.Key, attribute.Value, cloudEvent.Extensions.Values)));
                        break;
                }
            }
        }

        public static bool HasCloudEvent(this HttpResponseMessage httpResponseMessage)
        {
            return ((httpResponseMessage.Content.Headers.ContentType != null &&
                     httpResponseMessage.Content.Headers.ContentType.MediaType.StartsWith(CloudEvent.MediaType)) ||
                    httpResponseMessage.Headers.Contains(SpecVersionHttpHeader));
        }

        public static Task<CloudEvent> ToCloudEvent(this HttpResponseMessage httpResponseMessage,
            params ICloudEventExtension[] extensions)
        {
            return ToCloudEventInternalAsync(httpResponseMessage, null, extensions);
        }

        public static Task<CloudEvent> ToCloudEventAsync(this HttpResponseMessage httpResponseMessage,
            ICloudEventFormatter formatter, params ICloudEventExtension[] extensions)
        {
            return ToCloudEventInternalAsync(httpResponseMessage, formatter, extensions);
        }

        public static async Task<CloudEvent> ToCloudEventAsync(this HttpListenerRequest httpListenerRequest,
            ICloudEventFormatter formatter,
            params ICloudEventExtension[] extensions)
        {
            if (httpListenerRequest.ContentType != null &&
               httpListenerRequest.ContentType.StartsWith(CloudEvent.MediaType,
                   StringComparison.InvariantCultureIgnoreCase))
            {
                // handle structured mode
                if (formatter == null)
                {
                    // if we didn't get a formatter, pick one
                    if (httpListenerRequest.ContentType.EndsWith(JsonEventFormatter.MediaTypeSuffix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        formatter = jsonFormatter;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unsupported CloudEvents encoding");
                    }
                }

                return formatter.DecodeStructuredEvent(httpListenerRequest.InputStream, extensions);
            }
            else
            {
                var cloudEvent = new CloudEvent(extensions);
                var attributes = cloudEvent.GetAttributes();
                foreach (var httpResponseHeader in httpListenerRequest.Headers.AllKeys)
                {
                    if (httpResponseHeader.StartsWith(HttpHeaderPrefix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        string headerValue = httpListenerRequest.Headers[httpResponseHeader];
                        if (headerValue.StartsWith("{") && headerValue.EndsWith("}") || headerValue.StartsWith("[") && headerValue.EndsWith("]"))
                        {
                            attributes[httpResponseHeader.Substring(3).ToLowerInvariant()] =
                                JsonConvert.DeserializeObject(headerValue);
                        }
                        else
                        {
                            attributes[httpResponseHeader.Substring(3).ToLowerInvariant()] = headerValue;
                        }
                    }
                }

                cloudEvent.ContentType = httpListenerRequest.ContentType != null
                    ? new ContentType(httpListenerRequest.ContentType)
                    : null;
                cloudEvent.Data = httpListenerRequest.InputStream;
                return cloudEvent;
            }
        }


        static async Task<CloudEvent> ToCloudEventInternalAsync(HttpResponseMessage httpResponseMessage,
            ICloudEventFormatter formatter, ICloudEventExtension[] extensions)
        {
            if (httpResponseMessage.Content.Headers.ContentType != null &&
                httpResponseMessage.Content.Headers.ContentType.MediaType.StartsWith("application/cloudevents",
                    StringComparison.InvariantCultureIgnoreCase))
            {
                // handle structured mode
                if (formatter == null)
                {
                    // if we didn't get a formatter, pick one
                    if (httpResponseMessage.Content.Headers.ContentType.MediaType.EndsWith("+json",
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        formatter = jsonFormatter;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unsupported CloudEvents encoding");
                    }
                }

                return formatter.DecodeStructuredEvent(await httpResponseMessage.Content.ReadAsByteArrayAsync(),
                    extensions);
            }
            else
            {
                var cloudEvent = new CloudEvent(extensions);
                var attributes = cloudEvent.GetAttributes();
                foreach (var httpResponseHeader in httpResponseMessage.Headers)
                {
                    if (httpResponseHeader.Key.StartsWith(HttpHeaderPrefix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        attributes[httpResponseHeader.Key.Substring(3).ToLowerInvariant()] =
                            httpResponseHeader.Value;
                    }
                }

                cloudEvent.ContentType = httpResponseMessage.Content.Headers.ContentType != null
                    ? new ContentType(httpResponseMessage.Content.Headers.ContentType.ToString())
                    : null;
                cloudEvent.Data = await httpResponseMessage.Content.ReadAsStreamAsync();
                return cloudEvent;
            }
        }
    }
}