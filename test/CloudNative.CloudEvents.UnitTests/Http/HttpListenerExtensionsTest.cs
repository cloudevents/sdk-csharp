// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.NewtonsoftJson;
using CloudNative.CloudEvents.UnitTests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.Http.UnitTests
{
    public class HttpListenerExtensionsTest : HttpTestBase
    {
        [Theory]
        [MemberData(nameof(HttpClientExtensionsTest.SingleCloudEventMessages), MemberType = typeof(HttpClientExtensionsTest))]
        public async Task IsCloudEvent_True(string description, HttpContent content, IDictionary<string, string> headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = new HttpRequestMessage(HttpMethod.Get, ListenerAddress) { Content = content };
            HttpClientExtensionsTest.CopyHeaders(headers, request.Headers);
            var result = await SendRequestAsync(request, context => Task.FromResult(context.Request.IsCloudEvent()));
            Assert.True(result);
        }

        [Theory]
        [MemberData(nameof(HttpClientExtensionsTest.BatchMessages), MemberType = typeof(HttpClientExtensionsTest))]
        [MemberData(nameof(HttpClientExtensionsTest.NonCloudEventMessages), MemberType = typeof(HttpClientExtensionsTest))]
        public async Task IsCloudEvent_False(string description, HttpContent content, IDictionary<string, string> headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = new HttpRequestMessage(HttpMethod.Get, ListenerAddress) { Content = content };
            HttpClientExtensionsTest.CopyHeaders(headers, request.Headers);
            var result = await SendRequestAsync(request, context => Task.FromResult(context.Request.IsCloudEvent()));
            Assert.False(result);
        }

        [Theory]
        [MemberData(nameof(HttpClientExtensionsTest.BatchMessages), MemberType = typeof(HttpClientExtensionsTest))]
        public async Task IsCloudEventBatch_True(string description, HttpContent content, IDictionary<string, string> headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = new HttpRequestMessage(HttpMethod.Get, ListenerAddress) { Content = content };
            HttpClientExtensionsTest.CopyHeaders(headers, request.Headers);
            var result = await SendRequestAsync(request, context => Task.FromResult(context.Request.IsCloudEventBatch()));
            Assert.True(result);
        }

        [Theory]
        [MemberData(nameof(HttpClientExtensionsTest.SingleCloudEventMessages), MemberType = typeof(HttpClientExtensionsTest))]
        [MemberData(nameof(HttpClientExtensionsTest.NonCloudEventMessages), MemberType = typeof(HttpClientExtensionsTest))]
        public async Task IsCloudEventBatch_False(string description, HttpContent content, IDictionary<string, string> headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = new HttpRequestMessage(HttpMethod.Get, ListenerAddress) { Content = content };
            HttpClientExtensionsTest.CopyHeaders(headers, request.Headers);
            var result = await SendRequestAsync(request, context => Task.FromResult(context.Request.IsCloudEventBatch()));
            Assert.False(result);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ToCloudEvent_BinaryMode(bool async)
        {
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(ListenerAddress),
                Headers =
                {
                    { "ce-specversion", "1.0" },
                    { "ce-type", "test-type" },
                    { "ce-id", "test-id" },
                    { "ce-source", TestHelpers.SampleUriReferenceText },
                },
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("test content"))
                {
                    Headers = { { "content-type", "text/plain" } }
                }
            };

            var cloudEvent = await SendRequestAsync(request, async context =>
                async
                    ? await context.Request.ToCloudEventAsync(new JsonEventFormatter())
                    : context.Request.ToCloudEvent(new JsonEventFormatter()));

            Assert.NotNull(cloudEvent);
            Assert.Equal("test-id", cloudEvent.Id);
            Assert.Equal("test-type", cloudEvent.Type);
            Assert.Equal(TestHelpers.SampleUriReference, cloudEvent.Source);
            Assert.Equal("text/plain", cloudEvent.DataContentType);
            Assert.Equal("test content", cloudEvent.Data);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ToCloudEvent_StructuredMode(bool async)
        {
            var originalCloudEvent = new CloudEvent
            {
                Data = "sample text",
                DataContentType = "text/plain"
            }.PopulateRequiredAttributes();

            var bytes = new JsonEventFormatter().EncodeStructuredModeMessage(originalCloudEvent, out var contentType);
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(ListenerAddress),
                Content = new ByteArrayContent(bytes.ToArray())
                {
                    Headers = { { "content-type", contentType.ToString() } }
                }
            };

            var parsedCloudEvent = await SendRequestAsync(request, async context =>
                async
                    ? await context.Request.ToCloudEventAsync(new JsonEventFormatter())
                    : context.Request.ToCloudEvent(new JsonEventFormatter()));

            Assert.NotNull(parsedCloudEvent);
            Assert.Equal(originalCloudEvent.Id, parsedCloudEvent.Id);
            Assert.Equal(originalCloudEvent.Type, parsedCloudEvent.Type);
            Assert.Equal(originalCloudEvent.Source, parsedCloudEvent.Source);
            Assert.Equal(originalCloudEvent.DataContentType, parsedCloudEvent.DataContentType);
            Assert.Equal(originalCloudEvent.Data, parsedCloudEvent.Data);
        }

        [Fact]
        public async Task ToCloudEventBatch_Valid()
        {
            var batch = CreateSampleBatch();

            var formatter = new JsonEventFormatter();
            var contentBytes = formatter.EncodeBatchModeMessage(batch, out var contentType);

            AssertBatchesEqual(batch, await GetBatchAsync(context => context.Request.ToCloudEventBatchAsync(formatter, EmptyExtensionArray)));
            AssertBatchesEqual(batch, await GetBatchAsync(context => context.Request.ToCloudEventBatchAsync(formatter, EmptyExtensionSequence)));
            AssertBatchesEqual(batch, await GetBatchAsync(context => Task.FromResult(context.Request.ToCloudEventBatch(formatter, EmptyExtensionArray))));
            AssertBatchesEqual(batch, await GetBatchAsync(context => Task.FromResult(context.Request.ToCloudEventBatch(formatter, EmptyExtensionSequence))));

            Task<IReadOnlyList<CloudEvent>> GetBatchAsync(Func<HttpListenerContext, Task<IReadOnlyList<CloudEvent>>> handler)
            {
                var request = HttpClientExtensionsTest.CreateRequestMessage(contentBytes, contentType);
                request.RequestUri = new Uri(ListenerAddress);
                return SendRequestAsync(request, handler);
            }
        }

        [Fact]
        public async Task ToCloudEventBatchAsync_Invalid()
        {
            // Most likely accident: calling ToCloudEventBatchAsync with a single event in structured mode.
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            var contentBytes = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);

            await ExpectFailure(context => context.Request.ToCloudEventBatchAsync(formatter, EmptyExtensionArray));
            await ExpectFailure(context => context.Request.ToCloudEventBatchAsync(formatter, EmptyExtensionSequence));
            await ExpectFailure(context => Task.FromResult(context.Request.ToCloudEventBatch(formatter, EmptyExtensionArray)));
            await ExpectFailure(context => Task.FromResult(context.Request.ToCloudEventBatch(formatter, EmptyExtensionSequence)));

            async Task ExpectFailure(Func<HttpListenerContext, Task<IReadOnlyList<CloudEvent>>> handler)
            {
                var request = HttpClientExtensionsTest.CreateRequestMessage(contentBytes, contentType);
                request.RequestUri = new Uri(ListenerAddress);
                await SendRequestAsync(request, context => Assert.ThrowsAsync<ArgumentException>(() => handler(context)));
            }
        }

        [Fact]
        public async Task CopyToHttpListenerResponseAsync_BinaryMode()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "plain text",
                DataContentType = "text/plain"
            }.PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            var response = await GetResponseAsync(
                async context => await cloudEvent.CopyToHttpListenerResponseAsync(context.Response, ContentMode.Binary, formatter));
            response.EnsureSuccessStatusCode();
            var content = response.Content;
            Assert.Equal("text/plain", content.Headers.ContentType.MediaType);
            Assert.Equal("plain text", await content.ReadAsStringAsync());
            Assert.Equal("1.0", response.Headers.GetValues("ce-specversion").Single());
            Assert.Equal(cloudEvent.Type, response.Headers.GetValues("ce-type").Single());
            Assert.Equal(cloudEvent.Id, response.Headers.GetValues("ce-id").Single());
            Assert.Equal(CloudEventAttributeType.UriReference.Format(cloudEvent.Source), response.Headers.GetValues("ce-source").Single());
            // There's no data content type header; the content type itself is used for that.
            Assert.False(response.Headers.Contains("ce-datacontenttype"));
        }

        [Fact]
        public async Task CopyToListenerResponseAsync_ContentButNoContentType()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "plain text",
            }.PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            await GetResponseAsync(
                async context => await Assert.ThrowsAsync<ArgumentException>(() => cloudEvent.CopyToHttpListenerResponseAsync(context.Response, ContentMode.Binary, formatter)));
        }

        [Fact]
        public async Task CopyToListenerResponseAsync_BadContentMode()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            await GetResponseAsync(
                async context => await Assert.ThrowsAsync<ArgumentException>(() => cloudEvent.CopyToHttpListenerResponseAsync(context.Response, (ContentMode) 100, formatter)));
        }

        [Fact]
        public async Task CopyToHttpListenerResponseAsync_StructuredMode()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "plain text",
                DataContentType = "text/plain"
            }.PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            var response = await GetResponseAsync(
                async context => await cloudEvent.CopyToHttpListenerResponseAsync(context.Response, ContentMode.Structured, formatter));
            response.EnsureSuccessStatusCode();
            var content = response.Content;
            Assert.Equal(MimeUtilities.MediaType + "+json", content.Headers.ContentType.MediaType);
            var bytes = await content.ReadAsByteArrayAsync();

            var parsed = new JsonEventFormatter().DecodeStructuredModeMessage(bytes, MimeUtilities.ToContentType(content.Headers.ContentType), extensionAttributes: null);
            AssertCloudEventsEqual(cloudEvent, parsed);
            Assert.Equal(cloudEvent.Data, parsed.Data);

            // We populate headers even though we don't strictly need to; let's validate that.
            Assert.Equal("1.0", response.Headers.GetValues("ce-specversion").Single());
            Assert.Equal(cloudEvent.Type, response.Headers.GetValues("ce-type").Single());
            Assert.Equal(cloudEvent.Id, response.Headers.GetValues("ce-id").Single());
            Assert.Equal(CloudEventAttributeType.UriReference.Format(cloudEvent.Source), response.Headers.GetValues("ce-source").Single());
            // We don't populate the data content type header
            Assert.False(response.Headers.Contains("ce-datacontenttype"));
        }

        [Fact]
        public async Task CopyToHttpListenerResponseAsync_Batch()
        {
            var batch = CreateSampleBatch();

            var response = await GetResponseAsync(async context =>
            {
                await batch.CopyToHttpListenerResponseAsync(context.Response, new JsonEventFormatter());
                context.Response.StatusCode = 200;
            });

            response.EnsureSuccessStatusCode();
            var content = response.Content;
            Assert.Equal(MimeUtilities.BatchMediaType + "+json", content.Headers.ContentType.MediaType);
            var bytes = await content.ReadAsByteArrayAsync();
            var parsedBatch = new JsonEventFormatter().DecodeBatchModeMessage(bytes, MimeUtilities.ToContentType(content.Headers.ContentType), extensionAttributes: null);
            AssertBatchesEqual(batch, parsedBatch);
        }

        /// <summary>
        /// Executes the given request, expecting the given handler to be called.
        /// An empty response is proided on success.
        /// </summary>
        private async Task<T> SendRequestAsync<T>(HttpRequestMessage request, Func<HttpListenerContext, Task<T>> handler)
        {
            var guid = Guid.NewGuid().ToString();
            request.Headers.Add(TestContextHeader, guid);

            T result = default;
            bool executed = false;

            PendingRequests[guid] = async context =>
            {
                executed = true;
                result = await handler(context);
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            };

            var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, content);
            Assert.True(executed);
            return result;
        }

        /// <summary>
        /// Executes a simple GET request that will invoke the given handler, and returns the response (as an HttpResponseMessage).
        /// The handler is responsible for writing the response.
        /// </summary>
        private async Task<HttpResponseMessage> GetResponseAsync(Func<HttpListenerContext, Task> handler)
        {
            var guid = Guid.NewGuid().ToString();
            var request = new HttpRequestMessage(HttpMethod.Get, ListenerAddress);
            request.Headers.Add(TestContextHeader, guid);

            bool executed = false;

            PendingRequests[guid] = async context =>
            {
                executed = true;
                await handler(context);
            };

            var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(request);
            Assert.True(executed);
            return response;
        }
    }
}
