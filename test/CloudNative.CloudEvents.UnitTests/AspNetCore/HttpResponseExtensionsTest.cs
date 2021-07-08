// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.NewtonsoftJson;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using System;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.AspNetCore.UnitTests
{
    public class HttpResponseExtensionsTest
    {
        [Fact]
        public async Task CopyToHttpResponseAsync_BinaryMode()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "plain text",
                DataContentType = "text/plain"
            }.PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            var response = CreateResponse();
            await cloudEvent.CopyToHttpResponseAsync(response, ContentMode.Binary, formatter);
            
            var content = GetContent(response);
            Assert.Equal("text/plain", response.ContentType);
            Assert.Equal("plain text", Encoding.UTF8.GetString(content.Span));
            Assert.Equal("1.0", response.Headers["ce-specversion"]);
            Assert.Equal(cloudEvent.Type, response.Headers["ce-type"]);
            Assert.Equal(cloudEvent.Id, response.Headers["ce-id"]);
            Assert.Equal(CloudEventAttributeType.UriReference.Format(cloudEvent.Source!), response.Headers["ce-source"]);
            // There's no data content type header; the content type itself is used for that.
            Assert.False(response.Headers.ContainsKey("ce-datacontenttype"));
        }
        
        [Fact]
        public async Task CopyToHttpResponseAsync_ContentButNoContentType()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "plain text",
            }.PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            var response = CreateResponse();
            await Assert.ThrowsAsync<ArgumentException>(() => cloudEvent.CopyToHttpResponseAsync(response, ContentMode.Binary, formatter));
        }

        [Fact]
        public async Task CopyToHttpResponseAsync_BadContentMode()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            var response = CreateResponse();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cloudEvent.CopyToHttpResponseAsync(response, (ContentMode)100, formatter));
        }

        [Fact]
        public async Task CopyToHttpResponseAsync_StructuredMode()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "plain text",
                DataContentType = "text/plain"
            }.PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            var response = CreateResponse();
            await cloudEvent.CopyToHttpResponseAsync(response, ContentMode.Structured, formatter);
            var content = GetContent(response);
            Assert.Equal(MimeUtilities.MediaType + "+json; charset=utf-8", response.ContentType);

            var parsed = new JsonEventFormatter().DecodeStructuredModeMessage(content, new ContentType(response.ContentType), extensionAttributes: null);
            AssertCloudEventsEqual(cloudEvent, parsed);
            Assert.Equal(cloudEvent.Data, parsed.Data);

            // We populate headers even though we don't strictly need to; let's validate that.
            Assert.Equal("1.0", response.Headers["ce-specversion"]);
            Assert.Equal(cloudEvent.Type, response.Headers["ce-type"]);
            Assert.Equal(cloudEvent.Id, response.Headers["ce-id"]);
            Assert.Equal(CloudEventAttributeType.UriReference.Format(cloudEvent.Source!), response.Headers["ce-source"]);
            // We don't populate the data content type header
            Assert.False(response.Headers.ContainsKey("ce-datacontenttype"));
        }

        [Fact]
        public async Task CopyToHttpResponseAsync_Batch()
        {
            var batch = CreateSampleBatch();
            var response = CreateResponse();
            await batch.CopyToHttpResponseAsync(response, new JsonEventFormatter());

            var content = GetContent(response);
            Assert.Equal(MimeUtilities.BatchMediaType + "+json; charset=utf-8", response.ContentType);
            var parsedBatch = new JsonEventFormatter().DecodeBatchModeMessage(content, new ContentType(response.ContentType), extensionAttributes: null);
            AssertBatchesEqual(batch, parsedBatch);
        }
        
        private static HttpResponse CreateResponse() => new DefaultHttpResponse(new DefaultHttpContext()) { Body = new MemoryStream() };
        private static ReadOnlyMemory<byte> GetContent(HttpResponse response)
        {
            response.Body.Position = 0;
            return BinaryDataUtilities.ToReadOnlyMemory(response.Body);
        }
    }
}
