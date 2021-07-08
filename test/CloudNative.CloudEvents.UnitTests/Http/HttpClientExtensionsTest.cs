// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.NewtonsoftJson;
using CloudNative.CloudEvents.UnitTests;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.Http.UnitTests
{
    public class HttpClientExtensionsTest : HttpTestBase
    {
        public static TheoryData<string, HttpContent, IDictionary<string, string>?> SingleCloudEventMessages => new TheoryData<string, HttpContent, IDictionary<string, string>?>
        {
            {
                "Binary",
                new StringContent("content is ignored", Encoding.UTF8, "text/plain"),
                new Dictionary<string, string>
                {
                    { "ce-specversion", "1.0" },
                    { "ce-type", "test-type" },
                    { "ce-id", "test-id" },
                    { "ce-source", "//test" }
                }
            },
            {
                "Structured",
                new StringContent("content is ignored", Encoding.UTF8, "application/cloudevents+json"),
                null
            }
        };

        public static TheoryData<string, HttpContent, IDictionary<string, string>?> BatchMessages => new TheoryData<string, HttpContent, IDictionary<string, string>?>
        {
            {
                "Batch",
                new StringContent("content is ignored", Encoding.UTF8, "application/cloudevents-batch+json"),
                null
            }
        };

        public static TheoryData<string, HttpContent, IDictionary<string, string>?> NonCloudEventMessages => new TheoryData<string, HttpContent, IDictionary<string, string>?>
        {
            {
                "Plain text",
                new StringContent("content is ignored", Encoding.UTF8, "text/plain"),
                null
            }
        };

        [Theory]
        [MemberData(nameof(SingleCloudEventMessages))]
        public void IsCloudEvent_True(string description, HttpContent content, IDictionary<string, string>? headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = new HttpRequestMessage { Content = content };
            CopyHeaders(headers, request.Headers);
            Assert.True(request.IsCloudEvent());

            var response = new HttpResponseMessage { Content = content };
            CopyHeaders(headers, response.Headers);
            Assert.True(request.IsCloudEvent());
        }

        [Theory]
        [MemberData(nameof(BatchMessages))]
        [MemberData(nameof(NonCloudEventMessages))]
        public void IsCloudEvent_False(string description, HttpContent content, IDictionary<string, string>? headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = new HttpRequestMessage { Content = content };
            CopyHeaders(headers, request.Headers);
            Assert.False(request.IsCloudEvent());

            var response = new HttpResponseMessage { Content = content };
            CopyHeaders(headers, response.Headers);
            Assert.False(request.IsCloudEvent());
        }

        [Theory]
        [MemberData(nameof(BatchMessages))]
        public void IsCloudEventBatch_True(string description, HttpContent content, IDictionary<string, string>? headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = new HttpRequestMessage { Content = content };
            CopyHeaders(headers, request.Headers);
            Assert.True(request.IsCloudEventBatch());

            var response = new HttpResponseMessage { Content = content };
            CopyHeaders(headers, response.Headers);
            Assert.True(request.IsCloudEventBatch());
        }

        [Theory]
        [MemberData(nameof(SingleCloudEventMessages))]
        [MemberData(nameof(NonCloudEventMessages))]
        public void IsCloudEventBatch_False(string description, HttpContent content, IDictionary<string, string>? headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = new HttpRequestMessage { Content = content };
            CopyHeaders(headers, request.Headers);
            Assert.False(request.IsCloudEventBatch());

            var response = new HttpResponseMessage { Content = content };
            CopyHeaders(headers, response.Headers);
            Assert.False(request.IsCloudEventBatch());
        }

        [Fact]
        public async Task ToCloudEventBatchAsync_Valid()
        {
            var batch = CreateSampleBatch();

            var formatter = new JsonEventFormatter();
            var contentBytes = formatter.EncodeBatchModeMessage(batch, out var contentType);

            AssertBatchesEqual(batch, await CreateRequestMessage(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionArray));
            AssertBatchesEqual(batch, await CreateRequestMessage(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionSequence));
            AssertBatchesEqual(batch, await CreateResponseMessage(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionArray));
            AssertBatchesEqual(batch, await CreateResponseMessage(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionSequence));
        }

        [Fact]
        public async Task ToCloudEventBatchAsync_Invalid()
        {
            // Most likely accident: calling ToCloudEventBatchAsync with a single event in structured mode.
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            var contentBytes = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
            await Assert.ThrowsAsync<ArgumentException>(() => CreateRequestMessage(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionArray));
            await Assert.ThrowsAsync<ArgumentException>(() => CreateRequestMessage(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionSequence));
            await Assert.ThrowsAsync<ArgumentException>(() => CreateResponseMessage(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionArray));
            await Assert.ThrowsAsync<ArgumentException>(() => CreateResponseMessage(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionSequence));
        }

        [Fact]
        public async Task ToCloudEvent_Valid()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            var contentBytes = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
            var parsedRequest = await CreateRequestMessage(contentBytes, contentType).ToCloudEventAsync(formatter);
            var parsedResponse = await CreateResponseMessage(contentBytes, contentType).ToCloudEventAsync(formatter);

            AssertCloudEventsEqual(parsedRequest, cloudEvent);
            AssertCloudEventsEqual(parsedResponse, cloudEvent);
        }

        [Fact]
        public async Task ToCloudEvent_Invalid()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            var contentBytes = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
            // Remove the required 'id' attribute
            var obj = JObject.Parse(BinaryDataUtilities.GetString(contentBytes, Encoding.UTF8));
            obj.Remove("id");
            contentBytes = Encoding.UTF8.GetBytes(obj.ToString());

            await Assert.ThrowsAsync<ArgumentException>(() => CreateRequestMessage(contentBytes, contentType).ToCloudEventAsync(formatter));
            await Assert.ThrowsAsync<ArgumentException>(() => CreateResponseMessage(contentBytes, contentType).ToCloudEventAsync(formatter));
        }

        [Fact]
        public async Task HttpBinaryClientReceiveTest()
        {
            string ctx = Guid.NewGuid().ToString();
            PendingRequests.TryAdd(ctx, async context =>
            {
                var cloudEvent = new CloudEvent()
                {
                    Type = "com.github.pull.create",
                    Source = new Uri("https://github.com/cloudevents/spec/pull/123"),
                    Id = "A234-1234-1234",
                    Time = SampleTimestamp,
                    DataContentType = MediaTypeNames.Text.Xml,
                    // TODO: This isn't JSON, so maybe we shouldn't be using a JSON formatter?
                    // Further thought: separate out payload formatting from event formatting.
                    Data = "<much wow=\"xml\"/>",
                    ["comexampleextension1"] = "value",
                    ["utf8examplevalue"] = "æøå"
                };

                await cloudEvent.CopyToHttpListenerResponseAsync(context.Response, ContentMode.Binary, new JsonEventFormatter());
                context.Response.StatusCode = (int)HttpStatusCode.OK;
            });

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add(TestContextHeader, ctx);
            var result = await httpClient.GetAsync(new Uri(ListenerAddress + "ep"));

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);

            // The non-ASCII attribute value should have been URL-encoded using UTF-8 for the header.
            Assert.True(result.Headers.TryGetValues("ce-utf8examplevalue", out var utf8ExampleValues));
            Assert.Equal("%C3%A6%C3%B8%C3%A5", utf8ExampleValues.Single());

            var receivedCloudEvent = await result.ToCloudEventAsync(new JsonEventFormatter());

            Assert.Equal(CloudEventsSpecVersion.V1_0, receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            AssertTimestampsEqual(SampleTimestamp, receivedCloudEvent.Time!.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, receivedCloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

            Assert.Equal("value", receivedCloudEvent["comexampleextension1"]);
            Assert.Equal("æøå", receivedCloudEvent["utf8examplevalue"]);
        }

        [Fact]
        public async Task HttpBinaryClientSendTest()
        {
            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull/123"),
                Id = "A234-1234-1234",
                Time = SampleTimestamp,
                DataContentType = MediaTypeNames.Text.Xml,
                Data = "<much wow=\"xml\"/>",
                ["comexampleextension1"] = "value",
                ["utf8examplevalue"] = "æøå"
            };

            string ctx = Guid.NewGuid().ToString();
            var content = cloudEvent.ToHttpContent(ContentMode.Binary, new JsonEventFormatter());
            content.Headers.Add(TestContextHeader, ctx);

            PendingRequests.TryAdd(ctx, context =>
            {
                Assert.True(context.Request.IsCloudEvent());

                var receivedCloudEvent = context.Request.ToCloudEvent(new JsonEventFormatter());

                Assert.Equal(CloudEventsSpecVersion.V1_0, receivedCloudEvent.SpecVersion);
                Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
                Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
                Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
                AssertTimestampsEqual(SampleTimestamp, cloudEvent.Time.Value);
                Assert.Equal(MediaTypeNames.Text.Xml, receivedCloudEvent.DataContentType);

                // The non-ASCII attribute value should have been URL-encoded using UTF-8 for the header.
                Assert.True(content.Headers.TryGetValues("ce-utf8examplevalue", out var utf8ExampleValues));
                Assert.Equal("%C3%A6%C3%B8%C3%A5", utf8ExampleValues.Single());
                Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

                Assert.Equal("value", receivedCloudEvent["comexampleextension1"]);
                // The non-ASCII attribute value should have been correctly URL-decoded.
                Assert.Equal("æøå", receivedCloudEvent["utf8examplevalue"]);
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                return Task.CompletedTask;
            });

            var httpClient = new HttpClient();
            var result = await httpClient.PostAsync(new Uri(ListenerAddress + "ep"), content);
            if (result.StatusCode != HttpStatusCode.NoContent)
            {
                throw new InvalidOperationException(result.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
        }

        [Fact]
        public async Task HttpStructuredClientReceiveTest()
        {
            string ctx = Guid.NewGuid().ToString();
            PendingRequests.TryAdd(ctx, async context =>
            {
                var cloudEvent = new CloudEvent
                {
                    Type = "com.github.pull.create",
                    Source = new Uri("https://github.com/cloudevents/spec/pull/123"),
                    Id = "A234-1234-1234",
                    Time = SampleTimestamp,
                    DataContentType = MediaTypeNames.Text.Xml,
                    Data = "<much wow=\"xml\"/>",
                    ["comexampleextension1"] = "value",
                    ["utf8examplevalue"] = "æøå"
                };

                await cloudEvent.CopyToHttpListenerResponseAsync(context.Response, ContentMode.Structured, new JsonEventFormatter());
                context.Response.StatusCode = (int)HttpStatusCode.OK;
            });

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add(TestContextHeader, ctx);
            var result = await httpClient.GetAsync(new Uri(ListenerAddress + "ep"));

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.True(result.IsCloudEvent());
            var receivedCloudEvent = await result.ToCloudEventAsync(new JsonEventFormatter());

            Assert.Equal(CloudEventsSpecVersion.V1_0, receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            AssertTimestampsEqual(SampleTimestamp, receivedCloudEvent.Time!.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, receivedCloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

            Assert.Equal("value", receivedCloudEvent["comexampleextension1"]);
            Assert.Equal("æøå", receivedCloudEvent["utf8examplevalue"]);
        }

        [Fact]
        public async Task HttpStructuredClientSendTest()
        {
            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull/123"),
                Id = "A234-1234-1234",
                Time = SampleTimestamp,
                DataContentType = MediaTypeNames.Text.Xml,
                Data = "<much wow=\"xml\"/>",
                ["comexampleextension1"] = "value",
                ["utf8examplevalue"] = "æøå"
            };

            string ctx = Guid.NewGuid().ToString();
            var content = cloudEvent.ToHttpContent(ContentMode.Structured, new JsonEventFormatter());
            content.Headers.Add(TestContextHeader, ctx);

            PendingRequests.TryAdd(ctx, context =>
            {
                // Structured events contain a copy of the CloudEvent attributes as HTTP headers.
                var headers = context.Request.Headers;
                Assert.Equal("1.0", headers["ce-specversion"]);
                Assert.Equal("com.github.pull.create", headers["ce-type"]);
                Assert.Equal("https://github.com/cloudevents/spec/pull/123", headers["ce-source"]);
                Assert.Equal("A234-1234-1234", headers["ce-id"]);
                Assert.Equal("2018-04-05T17:31:00Z", headers["ce-time"]);
                // Note that datacontenttype is mapped in this case, but would not be included in binary mode.
                Assert.Equal("text/xml", headers["ce-datacontenttype"]);
                Assert.Equal("application/cloudevents+json; charset=utf-8", context.Request.ContentType);
                Assert.Equal("value", headers["ce-comexampleextension1"]);
                // The non-ASCII attribute value should have been URL-encoded using UTF-8 for the header.
                Assert.Equal("%C3%A6%C3%B8%C3%A5", headers["ce-utf8examplevalue"]);

                var receivedCloudEvent = context.Request.ToCloudEvent(new JsonEventFormatter());

                Assert.Equal(CloudEventsSpecVersion.V1_0, receivedCloudEvent.SpecVersion);
                Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
                Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
                Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
                AssertTimestampsEqual(SampleTimestamp, cloudEvent.Time.Value);
                Assert.Equal(MediaTypeNames.Text.Xml, receivedCloudEvent.DataContentType);
                Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

                Assert.Equal("value", receivedCloudEvent["comexampleextension1"]);
                Assert.Equal("æøå", receivedCloudEvent["utf8examplevalue"]);
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                return Task.CompletedTask;
            });

            var httpClient = new HttpClient();
            var result = (await httpClient.PostAsync(new Uri(ListenerAddress + "ep"), content));
            if (result.StatusCode != HttpStatusCode.NoContent)
            {
                throw new InvalidOperationException(result.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
        }

        [Fact]
        public void ContentType_FromCloudEvent_BinaryMode()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.DataContentType = "text/plain";
            var content = cloudEvent.ToHttpContent(ContentMode.Binary, new JsonEventFormatter());
            var expectedContentType = new MediaTypeHeaderValue("text/plain");
            Assert.Equal(expectedContentType, content.Headers.ContentType);
        }

        // We need to work out whether we want a modified version of this test.
        // It should be okay to not set a DataContentType if there's no data...
        // but what if there's a data value which is an empty string, empty byte array or empty stream?
        [Fact]
        public void NoContentType_NoContent()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var content = cloudEvent.ToHttpContent(ContentMode.Binary, new JsonEventFormatter());
            Assert.Null(content.Headers.ContentType);
        }

        [Fact]
        public void NoContentType_WithContent()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = "Some text";
            var exception = Assert.Throws<ArgumentException>(() => cloudEvent.ToHttpContent(ContentMode.Binary, new JsonEventFormatter()));
            Assert.StartsWith(Strings.ErrorContentTypeUnspecified, exception.Message);
        }

        [Fact]
        public async Task ToHttpContent_Batch()
        {
            var batch = CreateSampleBatch();

            var formatter = new JsonEventFormatter();
            var content = batch.ToHttpContent(formatter);

            var bytes = await content.ReadAsByteArrayAsync();
            var parsedBatch = formatter.DecodeBatchModeMessage(bytes, MimeUtilities.ToContentType(content.Headers.ContentType), extensionAttributes: null);
            AssertBatchesEqual(batch, parsedBatch);
        }

        internal static void CopyHeaders(IDictionary<string, string>? source, HttpHeaders target)
        {
            if (source is null)
            {
                return;
            }
            foreach (var header in source)
            {
                target.Add(header.Key, header.Value);
            }
        }

        internal static HttpRequestMessage CreateRequestMessage(ReadOnlyMemory<byte> content, ContentType contentType) =>
            new HttpRequestMessage
            {
                Content = new ByteArrayContent(content.ToArray())
                {
                    Headers = { ContentType = MimeUtilities.ToMediaTypeHeaderValue(contentType) }
                }
            };

        internal static HttpResponseMessage CreateResponseMessage(ReadOnlyMemory<byte> content, ContentType contentType) =>
            new HttpResponseMessage
            {
                Content = new ByteArrayContent(content.ToArray())
                {
                    Headers = { ContentType = MimeUtilities.ToMediaTypeHeaderValue(contentType) }
                }
            };
    }
}
