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

    public static class HttpClientExtension
    {
        static JsonEventFormatter jsonFormatter = new JsonEventFormatter();

        public static Task CopyFromAsync(this HttpListenerResponse httpListenerResponse, CloudEvent cloudEvent, ContentMode contentMode, IDictionary<string, string> extraHeaders,
            ICloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                var buffer = formatter.EncodeStructuredEvent(cloudEvent, out var contentType);
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
                stream = new MemoryStream(formatter.EncodeAttribute(CloudEventAttributes.DataAttributeName, cloudEvent.Data));
            }
            httpListenerResponse.ContentType = cloudEvent.ContentType.ToString();
            MapAttributesToListenerResponse(cloudEvent, httpListenerResponse);
            return stream.CopyToAsync(httpListenerResponse.OutputStream);
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
                        httpListenerResponse.Headers.Add("ce-" + attribute.Key,
                            Encoding.UTF8.GetString(jsonFormatter.EncodeAttribute(attribute.Key, attribute.Value)));
                        break;
                }
            }
        }

        public static bool HasCloudEvent(this HttpResponseMessage httpResponseMessage)
        {
            return ((httpResponseMessage.Content.Headers.ContentType != null &&
                     httpResponseMessage.Content.Headers.ContentType.MediaType.StartsWith("application/cloudevents")) ||
                    httpResponseMessage.Headers.Contains("ce-specversion"));
        }

        public static Task<HttpResponseMessage> PostCloudEventAsync(this HttpClient httpClient,
            Uri requestUri,
            CloudEvent cloudEvent,
            ContentMode contentMode = ContentMode.Structured,
            IDictionary<string, string> extraHeaders = null,
            ICloudEventFormatter formatter = null)
        {
            return PutPostCloudEventAsync(httpClient, httpClient.PostAsync, requestUri, cloudEvent, contentMode,
                extraHeaders, formatter, CancellationToken.None);
        }

        public static Task<HttpResponseMessage> PostCloudEventAsync(this HttpClient httpClient,
            Uri requestUri,
            CloudEvent cloudEvent,
            ContentMode contentMode,
            IDictionary<string, string> extraHeaders,
            ICloudEventFormatter formatter,
            CancellationToken cancellationToken)
        {
            return PutPostCloudEventAsync(httpClient, httpClient.PostAsync, requestUri, cloudEvent, contentMode,
                extraHeaders, formatter, cancellationToken);
        }

        public static Task<HttpResponseMessage> PutCloudEventAsync(this HttpClient httpClient,
            Uri requestUri,
            CloudEvent cloudEvent,
            ContentMode contentMode = ContentMode.Structured,
            IDictionary<string, string> extraHeaders = null,
            ICloudEventFormatter formatter = null)
        {
            return PutPostCloudEventAsync(httpClient, httpClient.PutAsync, requestUri, cloudEvent, contentMode,
                extraHeaders, formatter, CancellationToken.None);
        }

        public static Task<HttpResponseMessage> PutCloudEventAsync(this HttpClient httpClient,
            Uri requestUri,
            CloudEvent cloudEvent,
            ContentMode contentMode,
            IDictionary<string, string> extraHeaders,
            ICloudEventFormatter formatter,
            CancellationToken cancellationToken)
        {
            return PutPostCloudEventAsync(httpClient, httpClient.PutAsync, requestUri, cloudEvent, contentMode,
                extraHeaders, formatter, cancellationToken);
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
               httpListenerRequest.ContentType.StartsWith("application/cloudevents",
                   StringComparison.InvariantCultureIgnoreCase))
            {
                // handle structured mode
                if (formatter == null)
                {
                    // if we didn't get a formatter, pick one
                    if (httpListenerRequest.ContentType.EndsWith("+json",
                        StringComparison.InvariantCultureIgnoreCase))
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
                    if (httpResponseHeader.StartsWith("ce-", StringComparison.InvariantCultureIgnoreCase))
                    {
                        attributes.Add(
                            httpResponseHeader.Substring(3).ToLowerInvariant(),
                            httpListenerRequest.Headers[httpResponseHeader]);
                    }
                }

                cloudEvent.ContentType = httpListenerRequest.ContentType != null
                    ? new ContentType(httpListenerRequest.ContentType)
                    : null;
                cloudEvent.Data = httpListenerRequest.InputStream;
                return cloudEvent;
            }
        }

        static void MapHeadersToHttpContent(CloudEvent cloudEvent, HttpContent content)
        {
            foreach (var attribute in cloudEvent.GetAttributes())
            {
                switch (attribute.Key)
                {
                    case CloudEventAttributes.ContentTypeAttributeName:
                        break;
                    default:
                        content.Headers.Add("ce-" + attribute.Key,
                            Encoding.UTF8.GetString(jsonFormatter.EncodeAttribute(attribute.Key, attribute.Value)));
                        break;
                }
            }
        }

        static Task<HttpResponseMessage> PutPostCloudEventAsync(HttpClient httpClient,
            Func<Uri, HttpContent, CancellationToken, Task<HttpResponseMessage>> putpostFunc,
            Uri requestUri,
            CloudEvent cloudEvent,
            ContentMode contentMode,
            IDictionary<string, string> extraHeaders,
            ICloudEventFormatter formatter,
            CancellationToken cancellationToken)
        {
            HttpContent content = null;
            if (contentMode == ContentMode.Structured)
            {
                content = new ByteArrayContent(formatter.EncodeStructuredEvent(cloudEvent, out var contentType));
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType.ToString());
                MapHeadersToHttpContent(cloudEvent, content);
                return putpostFunc(requestUri, content, cancellationToken);
            }

            if (cloudEvent.Data is byte[])
            {
                content = new ByteArrayContent((byte[])cloudEvent.Data);
            }
            else
            {
                content = new ByteArrayContent(formatter.EncodeAttribute(CloudEventAttributes.DataAttributeName,
                    cloudEvent.Data));
            }

            content.Headers.ContentType = new MediaTypeHeaderValue(cloudEvent.ContentType?.MediaType);
            MapHeadersToHttpContent(cloudEvent, content);
            return putpostFunc(requestUri, content, cancellationToken);
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
                    if (httpResponseHeader.Key.StartsWith("ce-", StringComparison.InvariantCultureIgnoreCase))
                    {
                        attributes.Add(
                            httpResponseHeader.Key.Substring(3).ToLowerInvariant(),
                            httpResponseHeader.Value);
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