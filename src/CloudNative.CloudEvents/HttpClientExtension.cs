// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Mime;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public static class HttpClientExtension
    {
        const string HttpHeaderPrefix = "ce-";

        const string SpecVersionHttpHeader1 = HttpHeaderPrefix + "cloudEventsVersion";

        const string SpecVersionHttpHeader2 = HttpHeaderPrefix + "specversion";

        static JsonEventFormatter jsonFormatter = new JsonEventFormatter();

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
                var buffer =
                    formatter.EncodeStructuredEvent(cloudEvent, out var contentType);
                httpListenerResponse.ContentType = contentType.ToString();
                MapAttributesToListenerResponse(cloudEvent, httpListenerResponse);
                return httpListenerResponse.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }

            Stream stream = MapDataAttributeToStream(cloudEvent, formatter);
            httpListenerResponse.ContentType = cloudEvent.DataContentType?.ToString() ?? "application/json";
            MapAttributesToListenerResponse(cloudEvent, httpListenerResponse);
            return stream.CopyToAsync(httpListenerResponse.OutputStream);
        }

        /// <summary>
        /// Copies the CloudEvent into this HttpWebRequest instance
        /// </summary>
        /// <param name="httpWebRequest">this</param>
        /// <param name="cloudEvent">CloudEvent to copy</param>
        /// <param name="contentMode">Content mode (structured or binary)</param>
        /// <param name="formatter">Formatter</param>
        /// <returns>Task</returns>
        public static async Task CopyFromAsync(this HttpWebRequest httpWebRequest, CloudEvent cloudEvent,
            ContentMode contentMode, ICloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                var buffer =
                    formatter.EncodeStructuredEvent(cloudEvent, out var contentType);
                httpWebRequest.ContentType = contentType.ToString();
                await (httpWebRequest.GetRequestStream()).WriteAsync(buffer, 0, buffer.Length);
                return;
            }

            Stream stream = MapDataAttributeToStream(cloudEvent, formatter);
            httpWebRequest.ContentType = cloudEvent.DataContentType?.ToString() ?? "application/json";
            MapAttributesToWebRequest(cloudEvent, httpWebRequest);
            await stream.CopyToAsync(httpWebRequest.GetRequestStream());
        }

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
            if (IsWebHookValidationRequest(httpRequestMessage))
            {
                var origin = httpRequestMessage.Headers.GetValues("WebHook-Request-Origin").FirstOrDefault();
                var rate = httpRequestMessage.Headers.GetValues("WebHook-Request-Rate").FirstOrDefault();

                if (origin != null && (validateOrigin == null || validateOrigin(origin)))
                {
                    if (rate != null)
                    {
                        if (validateRate != null)
                        {
                            rate = validateRate(rate);
                        }
                        else
                        {
                            rate = "*";
                        }
                    }

                    if (httpRequestMessage.Headers.Contains("WebHook-Request-Callback"))
                    {
                        var uri = httpRequestMessage.Headers.GetValues("WebHook-Request-Callback").FirstOrDefault();
                        try
                        {
                            HttpClient client = new HttpClient();
                            var response = await client.GetAsync(new Uri(uri));
                            return new HttpResponseMessage(response.StatusCode);
                        }
                        catch (Exception)
                        {
                            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                        }
                    }
                    else
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.OK);
                        response.Headers.Add("Allow", "POST");
                        response.Headers.Add("WebHook-Allowed-Origin", origin);
                        response.Headers.Add("WebHook-Allowed-Rate", rate);
                        return response;
                    }
                }
            }

            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

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
            if (IsWebHookValidationRequest(context.Request))
            {
                var origin = context.Request.Headers.Get("WebHook-Request-Origin");
                var rate = context.Request.Headers.Get("WebHook-Request-Rate");

                if (origin != null && (validateOrigin == null || validateOrigin(origin)))
                {
                    if (rate != null)
                    {
                        if (validateRate != null)
                        {
                            rate = validateRate(rate);
                        }
                        else
                        {
                            rate = "*";
                        }
                    }

                    if (context.Request.Headers["WebHook-Request-Callback"] != null)
                    {
                        var uri = context.Request.Headers.Get("WebHook-Request-Callback");
                        try
                        {
                            HttpClient client = new HttpClient();
                            var response = await client.GetAsync(new Uri(uri));
                            context.Response.StatusCode = (int)response.StatusCode;
                            context.Response.Close();
                            return;
                        }
                        catch (Exception)
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            context.Response.Close();
                            return;
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.Headers.Add("Allow", "POST");
                        context.Response.Headers.Add("WebHook-Allowed-Origin", origin);
                        context.Response.Headers.Add("WebHook-Allowed-Rate", rate);
                        context.Response.Close();
                        return;
                    }
                }
            }

            context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            context.Response.Close();
        }

        /// <summary>
        /// Indicates whether this HttpResponseMessage holds a CloudEvent
        /// </summary>
        /// <param name="httpResponseMessage"></param>
        /// <returns>true, if the response is a CloudEvent</returns>
        public static bool IsCloudEvent(this HttpResponseMessage httpResponseMessage)
        {
            return (httpResponseMessage.Content != null && httpResponseMessage.Content.Headers.ContentType != null &&
                    httpResponseMessage.Content.Headers.ContentType.MediaType.StartsWith(CloudEvent.MediaType)) ||
                   httpResponseMessage.Headers.Contains(SpecVersionHttpHeader1) ||
                   httpResponseMessage.Headers.Contains(SpecVersionHttpHeader2);
        }

        /// <summary>
        /// Indicates whether this HttpListenerRequest holds a CloudEvent
        /// </summary>
        public static bool IsCloudEvent(this HttpListenerRequest httpListenerRequest)
        {
            return (httpListenerRequest.Headers["content-type"] != null &&
                    httpListenerRequest.Headers["content-type"].StartsWith(CloudEvent.MediaType)) ||
                   httpListenerRequest.Headers[SpecVersionHttpHeader1] != null ||
                   httpListenerRequest.Headers[SpecVersionHttpHeader2] != null;
        }

        /// <summary>
        /// Indicates whether this HttpListenerRequest is a web hook validation request
        /// </summary>
        public static bool IsWebHookValidationRequest(this HttpRequestMessage httpRequestMessage)
        {
            return (httpRequestMessage.Method.Method.Equals("options", StringComparison.InvariantCultureIgnoreCase) &&
                    httpRequestMessage.Headers.Contains("WebHook-Request-Origin"));
        }

        /// <summary>
        /// Indicates whether this HttpListenerRequest is a web hook validation request
        /// </summary>
        public static bool IsWebHookValidationRequest(this HttpListenerRequest httpRequestMessage)
        {
            return (httpRequestMessage.HttpMethod.Equals("options", StringComparison.InvariantCultureIgnoreCase) &&
                    httpRequestMessage.Headers["WebHook-Request-Origin"] != null);
        }

        /// <summary>
        /// Converts this response message into a CloudEvent object, with the given extensions.
        /// </summary>
        /// <param name="httpResponseMessage">Response message</param>
        /// <param name="extensions">List of extension instances</param>
        /// <returns>A CloudEvent instance or 'null' if the response message doesn't hold a CloudEvent</returns>
        public static CloudEvent ToCloudEvent(this HttpResponseMessage httpResponseMessage,
            params ICloudEventExtension[] extensions)
        {
            return ToCloudEventInternal(httpResponseMessage, null, extensions);
        }

        /// <summary>
        /// Converts this response message into a CloudEvent object, with the given extensions and
        /// overriding the default formatter.
        /// </summary>
        /// <param name="httpResponseMessage">Response message</param>
        /// <param name="formatter"></param>
        /// <param name="extensions">List of extension instances</param>
        /// <returns>A CloudEvent instance or 'null' if the response message doesn't hold a CloudEvent</returns>
        public static CloudEvent ToCloudEvent(this HttpResponseMessage httpResponseMessage,
            ICloudEventFormatter formatter, params ICloudEventExtension[] extensions)
        {
            return ToCloudEventInternal(httpResponseMessage, formatter, extensions);
        }

        /// <summary>
        /// Converts this listener request into a CloudEvent object, with the given extensions.
        /// </summary>
        /// <param name="httpListenerRequest">Listener request</param>
        /// <param name="extensions">List of extension instances</param>
        /// <returns>A CloudEvent instance or 'null' if the request message doesn't hold a CloudEvent</returns>
        public static CloudEvent ToCloudEvent(this HttpListenerRequest httpListenerRequest,
            params ICloudEventExtension[] extensions)
        {
            return ToCloudEvent(httpListenerRequest, null, extensions);
        }

        /// <summary>
        /// Converts this listener request into a CloudEvent object, with the given extensions,
        /// overriding the formatter.
        /// </summary>
        /// <param name="httpListenerRequest">Listener request</param>
        /// <param name="formatter"></param>
        /// <param name="extensions">List of extension instances</param>
        /// <returns>A CloudEvent instance or 'null' if the request message doesn't hold a CloudEvent</returns>
        public static CloudEvent ToCloudEvent(this HttpListenerRequest httpListenerRequest,
            ICloudEventFormatter formatter = null,
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
                    var contentType = httpListenerRequest.ContentType.Split(';');
                    if (contentType[0].Trim().EndsWith(JsonEventFormatter.MediaTypeSuffix,
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
                CloudEventsSpecVersion version = CloudEventsSpecVersion.Default;
                if (httpListenerRequest.Headers[SpecVersionHttpHeader1] != null)
                {
                    version = CloudEventsSpecVersion.V0_1;
                }

                if (httpListenerRequest.Headers[SpecVersionHttpHeader2] != null)
                {
                    version = httpListenerRequest.Headers[SpecVersionHttpHeader2] == "0.2"
                        ? CloudEventsSpecVersion.V0_2 : httpListenerRequest.Headers[SpecVersionHttpHeader2] == "0.3"
                            ? CloudEventsSpecVersion.V0_3 : CloudEventsSpecVersion.Default;
                }

                var cloudEvent = new CloudEvent(version, extensions);
                var attributes = cloudEvent.GetAttributes();
                foreach (var httpRequestHeaders in httpListenerRequest.Headers.AllKeys)
                {
                    if (httpRequestHeaders.Equals(SpecVersionHttpHeader1,
                            StringComparison.InvariantCultureIgnoreCase) ||
                        httpRequestHeaders.Equals(SpecVersionHttpHeader2, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    if (httpRequestHeaders.StartsWith(HttpHeaderPrefix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        string headerValue = httpListenerRequest.Headers[httpRequestHeaders];
                        headerValue = WebUtility.UrlDecode(headerValue);
                        // maps in headers have been abolished in 1.0
                        if (version != CloudEventsSpecVersion.V1_0 &&
                            (headerValue.StartsWith("{") && headerValue.EndsWith("}") ||
                            headerValue.StartsWith("[") && headerValue.EndsWith("]")))
                        {
                            attributes[httpRequestHeaders.Substring(3)] =
                                JsonConvert.DeserializeObject(headerValue);
                        }
                        else
                        {
                            attributes[httpRequestHeaders.Substring(3)] = headerValue;
                        }
                    }
                }

                cloudEvent.DataContentType = httpListenerRequest.ContentType != null
                    ? new ContentType(httpListenerRequest.ContentType)
                    : null;
                cloudEvent.Data = httpListenerRequest.InputStream;
                return cloudEvent;
            }
        }


        /// <summary>
        /// Converts this listener request into a CloudEvent object, with the given extensions,
        /// overriding the formatter.
        /// </summary>
        /// <param name="httpListenerRequest">Listener request</param>
        /// <param name="formatter"></param>
        /// <param name="extensions">List of extension instances</param>
        /// <returns>A CloudEvent instance or 'null' if the request message doesn't hold a CloudEvent</returns>
        public static CloudEvent ToCloudEvent(this HttpRequestMessage httpListenerRequest,
            ICloudEventFormatter formatter = null,
            params ICloudEventExtension[] extensions)
        {
            if (httpListenerRequest.Content != null && httpListenerRequest.Content.Headers.ContentType != null &&
                httpListenerRequest.Content.Headers.ContentType.MediaType.StartsWith(CloudEvent.MediaType,
                    StringComparison.InvariantCultureIgnoreCase))
            {
                // handle structured mode
                if (formatter == null)
                {
                    // if we didn't get a formatter, pick one
                    if (httpListenerRequest.Content.Headers.ContentType.MediaType.EndsWith(
                        JsonEventFormatter.MediaTypeSuffix,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        formatter = jsonFormatter;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unsupported CloudEvents encoding");
                    }
                }

                return formatter.DecodeStructuredEvent(
                    httpListenerRequest.Content.ReadAsStreamAsync().GetAwaiter().GetResult(), extensions);
            }
            else
            {
                CloudEventsSpecVersion version = CloudEventsSpecVersion.Default;
                if (httpListenerRequest.Headers.Contains(SpecVersionHttpHeader1))
                {
                    version = CloudEventsSpecVersion.V0_1;
                }

                if (httpListenerRequest.Headers.Contains(SpecVersionHttpHeader2))
                {
                    version = httpListenerRequest.Headers.GetValues(SpecVersionHttpHeader2).First() == "0.2"
                        ? CloudEventsSpecVersion.V0_2
                        : CloudEventsSpecVersion.Default;
                }

                var cloudEvent = new CloudEvent(version, extensions);
                var attributes = cloudEvent.GetAttributes();
                foreach (var httpRequestHeaders in httpListenerRequest.Headers)
                {
                    if (httpRequestHeaders.Key.Equals(SpecVersionHttpHeader1,
                            StringComparison.InvariantCultureIgnoreCase) ||
                        httpRequestHeaders.Key.Equals(SpecVersionHttpHeader2,
                            StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    if (httpRequestHeaders.Key.StartsWith(HttpHeaderPrefix,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        string headerValue = httpListenerRequest.Headers.GetValues(httpRequestHeaders.Key).First();
                        headerValue = WebUtility.UrlDecode(headerValue);
                        // maps in headers have been abolished in version 1.0 
                        if (version != CloudEventsSpecVersion.V1_0 &&
                            (headerValue.StartsWith("{") && headerValue.EndsWith("}") ||
                            headerValue.StartsWith("[") && headerValue.EndsWith("]")))
                        {
                            attributes[httpRequestHeaders.Key.Substring(3)] =
                                JsonConvert.DeserializeObject(headerValue);
                        }
                        else
                        {
                             attributes[httpRequestHeaders.Key.Substring(3)] = headerValue;
                        }
                    }
                }

                cloudEvent.DataContentType = httpListenerRequest.Content?.Headers.ContentType != null
                    ? new ContentType(httpListenerRequest.Content.Headers.ContentType.MediaType)
                    : null;
                cloudEvent.Data = httpListenerRequest.Content?.ReadAsStreamAsync().GetAwaiter().GetResult();
                return cloudEvent;
            }
        }

        static void MapAttributesToListenerResponse(CloudEvent cloudEvent, HttpListenerResponse httpListenerResponse)
        {
            foreach (var attribute in cloudEvent.GetAttributes())
            {
                if (!attribute.Key.Equals(CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion)) &&
                    !attribute.Key.Equals(CloudEventAttributes.DataContentTypeAttributeName(cloudEvent.SpecVersion)))
                {
                    string headerValue = UrlEncodeAttributeAsHeaderValue(
                        attribute.Key, attribute.Value, cloudEvent.SpecVersion, cloudEvent.Extensions.Values);
                    httpListenerResponse.Headers.Add(HttpHeaderPrefix + attribute.Key, headerValue);
                }
            }
        }

        static void MapAttributesToWebRequest(CloudEvent cloudEvent, HttpWebRequest httpWebRequest)
        {
            foreach (var attribute in cloudEvent.GetAttributes())
            {
                if (!attribute.Key.Equals(CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion)) &&
                    !attribute.Key.Equals(CloudEventAttributes.DataContentTypeAttributeName(cloudEvent.SpecVersion)))
                {
                    string headerValue = UrlEncodeAttributeAsHeaderValue(
                        attribute.Key, attribute.Value, cloudEvent.SpecVersion, cloudEvent.Extensions.Values);
                    httpWebRequest.Headers.Add(HttpHeaderPrefix + attribute.Key, headerValue);
                }
            }
        }

        static string UrlEncodeAttributeAsHeaderValue(string key, object attributeValue,
            CloudEventsSpecVersion specVersion, IEnumerable<ICloudEventExtension> extensions)
        {
            return WebUtility.UrlEncode(ConvertToString());
            string ConvertToString()
            {
                switch (attributeValue)
                {
                    case string text: return text;
                    case DateTime dateTime: return dateTime.ToString("u");
                    case Uri uri: return uri.ToString();
                    case int integer: return integer.ToString(CultureInfo.InvariantCulture);
                    default:
                        byte[] binaryValue = jsonFormatter.EncodeAttribute(specVersion, key, attributeValue, extensions);
                        return Encoding.UTF8.GetString(binaryValue);
                }
            };
        }

        static Stream MapDataAttributeToStream(CloudEvent cloudEvent, ICloudEventFormatter formatter)
        {
            Stream stream;
            if (cloudEvent.Data is byte[])
            {
                stream = new MemoryStream((byte[])cloudEvent.Data);
            }
            else if (cloudEvent.Data is string)
            {
                stream = new MemoryStream(Encoding.UTF8.GetBytes((string)cloudEvent.Data));
            }
            else if (cloudEvent.Data is Stream)
            {
                stream = (Stream)cloudEvent.Data;
            }
            else
            {
                stream = new MemoryStream(formatter.EncodeAttribute(cloudEvent.SpecVersion,
                    CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion),
                    cloudEvent.Data, cloudEvent.Extensions.Values));
            }

            return stream;
        }

        static CloudEvent ToCloudEventInternal(HttpResponseMessage httpResponseMessage,
            ICloudEventFormatter formatter, ICloudEventExtension[] extensions)
        {
            if (httpResponseMessage.Content?.Headers.ContentType != null &&
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

                return formatter.DecodeStructuredEvent(
                    httpResponseMessage.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult(),
                    extensions);
            }
            else
            {
                CloudEventsSpecVersion version = CloudEventsSpecVersion.Default;
                if (httpResponseMessage.Headers.Contains(SpecVersionHttpHeader1))
                {
                    version = CloudEventsSpecVersion.V0_1;
                }

                if (httpResponseMessage.Headers.Contains(SpecVersionHttpHeader2))
                {
                    version = httpResponseMessage.Headers.GetValues(SpecVersionHttpHeader2).First() == "0.2"
                        ? CloudEventsSpecVersion.V0_2 : httpResponseMessage.Headers.GetValues(SpecVersionHttpHeader2).First() == "0.3"
                            ? CloudEventsSpecVersion.V0_3 : CloudEventsSpecVersion.Default;
                }

                var cloudEvent = new CloudEvent(version, extensions);
                var attributes = cloudEvent.GetAttributes();
                foreach (var httpResponseHeader in httpResponseMessage.Headers)
                {
                    if (httpResponseHeader.Key.Equals(SpecVersionHttpHeader1,
                            StringComparison.InvariantCultureIgnoreCase) ||
                        httpResponseHeader.Key.Equals(SpecVersionHttpHeader2,
                            StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    if (httpResponseHeader.Key.StartsWith(HttpHeaderPrefix,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        string headerValue = WebUtility.UrlDecode(httpResponseHeader.Value.First());
                        var name = httpResponseHeader.Key.Substring(3);

                        // abolished structures in headers in 1.0
                        if (version != CloudEventsSpecVersion.V1_0 && (headerValue.StartsWith("\"") && headerValue.EndsWith("\"") ||
                            headerValue.StartsWith("'") && headerValue.EndsWith("'") ||
                            headerValue.StartsWith("{") && headerValue.EndsWith("}") ||
                            headerValue.StartsWith("[") && headerValue.EndsWith("]")))
                        {
                            attributes[name] = jsonFormatter.DecodeAttribute(version, name,
                                Encoding.UTF8.GetBytes(headerValue), extensions);
                        }
                        else
                        {
                            attributes[name] = headerValue;
                        }
                    }
                }

                cloudEvent.DataContentType = httpResponseMessage.Content?.Headers.ContentType != null
                    ? new ContentType(httpResponseMessage.Content.Headers.ContentType.ToString())
                    : null;
                cloudEvent.Data = httpResponseMessage.Content?.ReadAsStreamAsync().GetAwaiter().GetResult();
                return cloudEvent;
            }
        }
    }
}