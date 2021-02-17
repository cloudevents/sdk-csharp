// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.Http
{
    // TODO: Do we really need to have a subclass here? How about a static factory method instead?

    /// <summary>
    /// This class is for use with `HttpClient` and constructs content and headers for
    /// a HTTP request from a CloudEvent.
    /// </summary>
    public class CloudEventHttpContent : HttpContent
    {
        private readonly InnerByteArrayContent inner;                      

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cloudEvent">CloudEvent</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">Event formatter</param>
        public CloudEventHttpContent(CloudEvent cloudEvent, ContentMode contentMode, CloudEventFormatter formatter)
        {
            byte[] content;
            ContentType contentType;
            switch (contentMode)
            {
                case ContentMode.Structured:
                    content = formatter.EncodeStructuredModeMessage(cloudEvent, out contentType);
                    // This is optional in the specification, but can be useful.
                    MapHeaders(cloudEvent, includeDataContentType: true);
                    break;
                case ContentMode.Binary:
                    content = formatter.EncodeBinaryModeEventData(cloudEvent);
                    contentType = MimeUtilities.CreateContentTypeOrNull(cloudEvent.DataContentType);
                    MapHeaders(cloudEvent, includeDataContentType: false);
                    break;
                default:
                    throw new ArgumentException($"Unsupported content mode: {contentMode}");

            }
            inner = new InnerByteArrayContent(content);
            if (contentType is object)
            {
                Headers.ContentType = contentType.ToMediaTypeHeaderValue();
            }
            else if (content.Length != 0)
            {
                throw new ArgumentException(Strings.ErrorContentTypeUnspecified, nameof(cloudEvent));
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) =>
            inner.InnerSerializeToStreamAsync(stream, context);

        protected override bool TryComputeLength(out long length) =>
            inner.InnerTryComputeLength(out length);

        private void MapHeaders(CloudEvent cloudEvent, bool includeDataContentType)
        {
            Headers.Add(HttpUtilities.SpecVersionHttpHeader, HttpUtilities.EncodeHeaderValue(cloudEvent.SpecVersion.VersionId));
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
        class InnerByteArrayContent : ByteArrayContent
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
    }
}