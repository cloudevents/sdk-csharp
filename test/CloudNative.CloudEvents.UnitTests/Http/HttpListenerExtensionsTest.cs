// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.NewtonsoftJson;
using CloudNative.CloudEvents.UnitTests;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CloudNative.CloudEvents.Http.UnitTests
{
    public class HttpListenerExtensionsTest : HttpTestBase
    {
        [Fact]
        public async Task HttpWebHookValidation()
        {
            var httpClient = new HttpClient();
            var req = new HttpRequestMessage(HttpMethod.Options, new Uri(ListenerAddress + "ep"));
            req.Headers.Add("WebHook-Request-Origin", "example.com");
            req.Headers.Add("WebHook-Request-Rate", "120");
            var result = await httpClient.SendAsync(req);
            Assert.Equal("example.com", result.Headers.GetValues("WebHook-Allowed-Origin").First());
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
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
                Content = new ByteArrayContent(bytes)
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
                context.Response.StatusCode = (int) HttpStatusCode.NoContent;
            };

            var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, content);
            Assert.True(executed);
            return result;
        }
    }
}
