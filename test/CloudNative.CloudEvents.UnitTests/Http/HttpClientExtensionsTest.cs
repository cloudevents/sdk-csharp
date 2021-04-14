// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.NewtonsoftJson;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.Http.UnitTests
{
    public class HttpClientExtensionsTest : HttpTestBase
    {

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
            AssertTimestampsEqual(SampleTimestamp, receivedCloudEvent.Time.Value);
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
            AssertTimestampsEqual(SampleTimestamp, receivedCloudEvent.Time.Value);
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
    }
}
