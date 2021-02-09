// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.Http
{
    /// <summary>
    /// This class is for use with `HttpClient` and constructs content and headers for
    /// a HTTP request from a CloudEvent.
    /// </summary>
    public class CloudEventHttpContent : HttpContent
    {
        IInnerContent inner;                      

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cloudEvent">CloudEvent</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">Event formatter</param>
        public CloudEventHttpContent(CloudEvent cloudEvent, ContentMode contentMode, ICloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                inner = new InnerByteArrayContent(formatter.EncodeStructuredEvent(cloudEvent, out var contentType));
                // This is optional in the specification, but can be useful.
                MapHeaders(cloudEvent, includeDataContentType: true);
                Headers.ContentType = new MediaTypeHeaderValue(contentType.MediaType);
                return;
            }

            // TODO: Shouldn't we use the formatter in all cases? If I have a JSON formatter and
            // If we specify that the the data is a byte array, I'd expect to end up with a base64-encoded representation...
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
                inner = new InnerByteArrayContent(formatter.EncodeData(cloudEvent.Data));
            }

            // We don't require a data content type if there isn't any data.
            // We may not be able to tell whether the data is empty or not, but we're lenient
            // in that case.
            var dataContentType = cloudEvent.DataContentType;
            if (dataContentType is object)
            {
                var mediaType = new ContentType(dataContentType).MediaType;
                Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            }
            else if (TryComputeLength(out var length) && length != 0L)
            {
                throw new ArgumentException(Strings.ErrorContentTypeUnspecified, nameof(cloudEvent));
            }
            MapHeaders(cloudEvent, includeDataContentType: false);
        }

        private interface IInnerContent
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

        private void MapHeaders(CloudEvent cloudEvent, bool includeDataContentType)
        {
            Headers.Add(HttpUtilities.HttpHeaderPrefix + CloudEventsSpecVersion.SpecVersionAttribute.Name,
                HttpUtilities.EncodeHeaderValue(cloudEvent.SpecVersion.VersionId));
            foreach (var attributeAndValue in cloudEvent.GetPopulatedAttributes())
            {
                CloudEventAttribute attribute = attributeAndValue.Key;
                string headerName = HttpUtilities.HttpHeaderPrefix + attribute.Name;
                object value = attributeAndValue.Value;

                // Only map the data content type attribute to a header if we've been asked to
                if (attribute == cloudEvent.SpecVersion.DataContentTypeAttribute && !includeDataContentType)
                {
                    continue;
                }
                else
                {
                    string headerValue = HttpUtilities.EncodeHeaderValue(attribute.Format(value));
                    Headers.Add(headerName, headerValue);
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