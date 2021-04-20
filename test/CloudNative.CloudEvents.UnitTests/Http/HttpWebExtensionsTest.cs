// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.NewtonsoftJson;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.Http.UnitTests
{
    public class HttpWebExtensionsTest : HttpTestBase
    {
        [Fact]
        public async Task HttpBinaryWebRequestSendTest()
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
            HttpWebRequest httpWebRequest = WebRequest.CreateHttp(ListenerAddress + "ep");
            httpWebRequest.Method = "POST";
            await cloudEvent.CopyToHttpWebRequestAsync(httpWebRequest, ContentMode.Binary, new JsonEventFormatter());
            httpWebRequest.Headers.Add(TestContextHeader, ctx);

            PendingRequests.TryAdd(ctx, context =>
            {
                var receivedCloudEvent = context.Request.ToCloudEvent(new JsonEventFormatter());

                Assert.Equal(CloudEventsSpecVersion.V1_0, receivedCloudEvent.SpecVersion);
                Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
                Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
                Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
                AssertTimestampsEqual(SampleTimestamp, cloudEvent.Time.Value);
                Assert.Equal(MediaTypeNames.Text.Xml, receivedCloudEvent.DataContentType);
                Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

                // The non-ASCII attribute value should have been URL-encoded using UTF-8 for the header.
                Assert.Equal("%C3%A6%C3%B8%C3%A5", context.Request.Headers["ce-utf8examplevalue"]);

                Assert.Equal("value", receivedCloudEvent["comexampleextension1"]);
                Assert.Equal("æøå", receivedCloudEvent["utf8examplevalue"]);
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                return Task.CompletedTask;
            });

            var result = (HttpWebResponse)await httpWebRequest.GetResponseAsync();
            var content = new StreamReader(result.GetResponseStream()).ReadToEnd();
            Assert.True(result.StatusCode == HttpStatusCode.NoContent, content);
        }

        [Fact]
        public async Task HttpStructuredWebRequestSendTest()
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
            HttpWebRequest httpWebRequest = WebRequest.CreateHttp(ListenerAddress + "ep");
            httpWebRequest.Method = "POST";
            await cloudEvent.CopyToHttpWebRequestAsync(httpWebRequest, ContentMode.Structured, new JsonEventFormatter());
            httpWebRequest.Headers.Add(TestContextHeader, ctx);

            PendingRequests.TryAdd(ctx, context =>
            {
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

            var result = (HttpWebResponse) await httpWebRequest.GetResponseAsync();
            var content = new StreamReader(result.GetResponseStream()).ReadToEnd();
            Assert.True(result.StatusCode == HttpStatusCode.NoContent, content);
        }

        [Fact]
        public async Task CopyToHttpWebRequestAsync_Batch()
        {
            var batch = CreateSampleBatch();
            HttpWebRequest request = WebRequest.CreateHttp(ListenerAddress + "ep");
            request.Method = "POST";
            await batch.CopyToHttpWebRequestAsync(request, new JsonEventFormatter());

            var (bytes, contentType) = await SendRequestAsync(request, async context =>
            {
                var bytes = await BinaryDataUtilities.ToByteArrayAsync(context.Request.InputStream);
                var contentType = context.Request.Headers["Content-Type"];
                return (bytes, contentType);
            });

            Assert.Equal(MimeUtilities.BatchMediaType + "+json; charset=utf-8", contentType);
            var parsedBatch = new JsonEventFormatter().DecodeBatchModeMessage(bytes, MimeUtilities.CreateContentTypeOrNull(contentType), extensionAttributes: null);
            AssertBatchesEqual(batch, parsedBatch);
        }

        /// <summary>
        /// Executes the given request, expecting the given handler to be called.
        /// An empty response is proided on success.
        /// </summary>
        private async Task<T> SendRequestAsync<T>(HttpWebRequest request, Func<HttpListenerContext, Task<T>> handler)
        {
            var guid = Guid.NewGuid().ToString();
            request.Headers.Add(TestContextHeader, guid);

            T result = default;
            bool executed = false;

            PendingRequests[guid] = async context =>
            {
                executed = true;
                result = await handler(context);
                context.Response.StatusCode = (int) HttpStatusCode.NoContent;
            };

            using var response = (HttpWebResponse) await request.GetResponseAsync();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.True(executed);
            return result;
        }
    }
}
