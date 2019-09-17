// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// This class is for use with `HttpClient` and constructs content and headers for
    /// a HTTP request from a CloudEvent.
    /// </summary>
    public class CloudEventContent : HttpContent
    {
        IInnerContent inner;                      
        static JsonEventFormatter jsonFormatter = new JsonEventFormatter();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cloudEvent">CloudEvent</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">Event formatter</param>
        public CloudEventContent(CloudEvent cloudEvent, ContentMode contentMode, ICloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                inner = new InnerByteArrayContent(formatter.EncodeStructuredEvent(cloudEvent, out var contentType));
                Headers.ContentType = new MediaTypeHeaderValue(contentType.MediaType);
                MapHeaders(cloudEvent);
                return;
            }

            if (cloudEvent.Data is byte[])
            {
                inner = new InnerByteArrayContent((byte[])cloudEvent.Data);
            }
            else if (cloudEvent.Data is string)
            {
                inner = new InnerStringContent((string)cloudEvent.Data);
            }
            else if (cloudEvent.Data is Stream)
            {
                inner = new InnerStreamContent((Stream)cloudEvent.Data);
            }
            else
            {
                inner = new InnerByteArrayContent(formatter.EncodeAttribute(cloudEvent.SpecVersion, CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion),
                    cloudEvent.Data, cloudEvent.Extensions.Values));
            }

            Headers.ContentType = new MediaTypeHeaderValue(cloudEvent.DataContentType?.MediaType);
            MapHeaders(cloudEvent);
        }

        interface IInnerContent
        {
            Task InnerSerializeToStreamAsync(Stream stream, TransportContext context);
            bool InnerTryComputeLength(out long length);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return inner.InnerSerializeToStreamAsync(stream, context);
        }

        protected override bool TryComputeLength(out long length)
        {
            return inner.InnerTryComputeLength(out length);
        }

        void MapHeaders(CloudEvent cloudEvent)
        {
            foreach (var attribute in cloudEvent.GetAttributes())
            {
                if (!(attribute.Key.Equals(CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion)) ||
                      attribute.Key.Equals(CloudEventAttributes.DataContentTypeAttributeName(cloudEvent.SpecVersion))))
                {
                    if (attribute.Value is string)
                    {
                        Headers.Add("ce-" + attribute.Key, attribute.Value.ToString());
                    }
                    else if (attribute.Value is DateTime)
                    {
                        Headers.Add("ce-" + attribute.Key, ((DateTime)attribute.Value).ToString("u"));
                    }
                    else if (attribute.Value is Uri || attribute.Value is int)
                    {
                        Headers.Add("ce-" + attribute.Key, attribute.Value.ToString());
                    }
                    else
                    {
                        Headers.Add("ce-" + attribute.Key,
                            Encoding.UTF8.GetString(jsonFormatter.EncodeAttribute(cloudEvent.SpecVersion, attribute.Key, attribute.Value,
                                cloudEvent.Extensions.Values)));
                    }
                }
            }
        }

        /// <summary>
        /// This inner class is required to get around the 'protected'-ness of the
        /// override functions of HttpContent for enabling containment/delegation
        /// </summary>
        class InnerByteArrayContent : ByteArrayContent, IInnerContent
        {
            public InnerByteArrayContent(byte[] content) : base(content)
            {
            }

            public Task InnerSerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return base.SerializeToStreamAsync(stream, context);
            }

            public bool InnerTryComputeLength(out long length)
            {
                return base.TryComputeLength(out length);
            }
        }

        /// <summary>
        /// This inner class is required to get around the 'protected'-ness of the
        /// override functions of HttpContent for enabling containment/delegation
        /// </summary>
        class InnerStreamContent : StreamContent, IInnerContent
        {
            public InnerStreamContent(Stream content) : base(content)
            {
            }

            public Task InnerSerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return base.SerializeToStreamAsync(stream, context);
            }

            public bool InnerTryComputeLength(out long length)
            {
                return base.TryComputeLength(out length);
            }
        }

        /// <summary>
        /// This inner class is required to get around the 'protected'-ness of the
        /// override functions of HttpContent for enabling containment/delegation
        /// </summary>
        class InnerStringContent : StringContent, IInnerContent
        {
            public InnerStringContent(string content) : base(content)
            {
            }

            public Task InnerSerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return base.SerializeToStreamAsync(stream, context);
            }

            public bool InnerTryComputeLength(out long length)
            {
                return base.TryComputeLength(out length);
            }
        }
    }
}