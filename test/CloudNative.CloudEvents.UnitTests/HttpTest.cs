// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.UnitTests
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Mime;
    using System.Threading.Tasks;
    using Xunit;
    using static TestHelpers;

    public class HttpTest : IDisposable
    {
        private static readonly DateTimeOffset sampleTimestamp = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero);

        const string listenerAddress = "http://localhost:52671/";

        const string testContextHeader = "testcontext";

        HttpListener listener;

        ConcurrentDictionary<string, Func<HttpListenerContext, Task>> pendingRequests =
            new ConcurrentDictionary<string, Func<HttpListenerContext, Task>>();

        public HttpTest()
        {
            listener = new HttpListener()
            {
                AuthenticationSchemes = AuthenticationSchemes.Anonymous,
                Prefixes = { listenerAddress }
            };
            listener.Start();
            listener.GetContextAsync().ContinueWith(async t =>
            {
                if (t.IsCompleted)
                {
                    await HandleContext(t.Result);
                }
            });
        }

        public void Dispose()
        {
            listener.Stop();
        }

        async Task HandleContext(HttpListenerContext requestContext)
        {
            var ctxHeaderValue = requestContext.Request.Headers[testContextHeader];

            if (requestContext.Request.IsWebHookValidationRequest())
            {
                await requestContext.HandleAsWebHookValidationRequest(null, null);
                return;
            }

            if (pendingRequests.TryRemove(ctxHeaderValue, out var pending))
            {
                await pending(requestContext);
            }
            await listener.GetContextAsync().ContinueWith(async t =>
            {
                if (t.IsCompleted)
                {
                    await HandleContext(t.Result);
                }
            });
        }

        [Fact]
        async Task HttpWebHookValidation()
        {
            var httpClient = new HttpClient();
            var req = new HttpRequestMessage(HttpMethod.Options, new Uri(listenerAddress + "ep"));
            req.Headers.Add("WebHook-Request-Origin", "example.com");
            req.Headers.Add("WebHook-Request-Rate", "120");
            var result = await httpClient.SendAsync( req );
            Assert.Equal("example.com", result.Headers.GetValues("WebHook-Allowed-Origin").First());
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        async Task HttpBinaryClientReceiveTest()
        {
            string ctx = Guid.NewGuid().ToString();
            pendingRequests.TryAdd(ctx, async context =>
            {
                try
                {
                    var cloudEvent = new CloudEvent("com.github.pull.create",
                        new Uri("https://github.com/cloudevents/spec/pull/123"))
                    {
                        Id = "A234-1234-1234",
                        Time = sampleTimestamp,
                        DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                        Data = "<much wow=\"xml\"/>"
                    };

                    var attrs = cloudEvent.GetAttributes();
                    attrs["comexampleextension1"] = "value";
                    attrs["utf8examplevalue"] = "æøå";

                    await context.Response.CopyFromAsync(cloudEvent, ContentMode.Binary, new JsonEventFormatter());
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                }
                catch (Exception e)
                {
                    using (var sw = new StreamWriter(context.Response.OutputStream))
                    {
                        sw.Write(e.ToString());
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                }

                context.Response.Close();
            });

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add(testContextHeader, ctx);
            var result = await httpClient.GetAsync(new Uri(listenerAddress + "ep"));

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);

            // The non-ASCII attribute value should have been URL-encoded using UTF-8 for the header.
            Assert.True(result.Headers.TryGetValues("ce-utf8examplevalue", out var utf8ExampleValues));
            Assert.Equal("%C3%A6%C3%B8%C3%A5", utf8ExampleValues.Single());

            var receivedCloudEvent = result.ToCloudEvent();

            Assert.Equal(CloudEventsSpecVersion.V1_0, receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            AssertTimestampsEqual(sampleTimestamp, receivedCloudEvent.Time.Value);
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), receivedCloudEvent.DataContentType);
            using (var sr = new StreamReader((Stream)receivedCloudEvent.Data))
            {
                Assert.Equal("<much wow=\"xml\"/>", sr.ReadToEnd());
            }

            var attr = receivedCloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
            Assert.Equal("æøå", (string) attr["utf8examplevalue"]);
        }

        [Fact]
        async Task HttpBinaryClientSendTest()
        {
            var cloudEvent = new CloudEvent("com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"))
            {
                Id = "A234-1234-1234",
                Time = sampleTimestamp,
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["utf8examplevalue"] = "æøå";

            string ctx = Guid.NewGuid().ToString();
            var content = new CloudEventContent(cloudEvent, ContentMode.Binary, new JsonEventFormatter());
            content.Headers.Add(testContextHeader, ctx);

            pendingRequests.TryAdd(ctx, context =>
            {
                try
                {
                    Assert.True(context.Request.IsCloudEvent());

                    var receivedCloudEvent = context.Request.ToCloudEvent(new JsonEventFormatter());

                    Assert.Equal(CloudEventsSpecVersion.V1_0, receivedCloudEvent.SpecVersion);
                    Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
                    Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
                    Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
                    AssertTimestampsEqual(sampleTimestamp, cloudEvent.Time.Value);
                    Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), receivedCloudEvent.DataContentType);

                    // The non-ASCII attribute value should have been URL-encoded using UTF-8 for the header.
                    Assert.True(content.Headers.TryGetValues("ce-utf8examplevalue", out var utf8ExampleValues));
                    Assert.Equal("%C3%A6%C3%B8%C3%A5", utf8ExampleValues.Single());

                    using (var sr = new StreamReader((Stream)receivedCloudEvent.Data))
                    {
                        Assert.Equal("<much wow=\"xml\"/>", sr.ReadToEnd());
                    }

                    var attr = receivedCloudEvent.GetAttributes();
                    Assert.Equal("value", (string)attr["comexampleextension1"]);
                    // The non-ASCII attribute value should have been correctly URL-decoded.
                    Assert.Equal("æøå", (string) attr["utf8examplevalue"]);
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                }
                catch (Exception e)
                {
                    using (var sw = new StreamWriter(context.Response.OutputStream))
                    {
                        sw.Write(e.ToString());
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                }

                context.Response.Close();
                return Task.CompletedTask;
            });

            var httpClient = new HttpClient();
            var result = (await httpClient.PostAsync(new Uri(listenerAddress + "ep"), content));
            if (result.StatusCode != HttpStatusCode.NoContent)
            {
                throw new InvalidOperationException(result.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
        }

        [Fact]
        async Task HttpStructuredClientReceiveTest()
        {
            string ctx = Guid.NewGuid().ToString();
            pendingRequests.TryAdd(ctx, async context =>
            {
                try
                {
                    var cloudEvent = new CloudEvent("com.github.pull.create",
                        new Uri("https://github.com/cloudevents/spec/pull/123"))
                    {
                        Id = "A234-1234-1234",
                        Time = sampleTimestamp,
                        DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                        Data = "<much wow=\"xml\"/>"
                    };

                    var attrs = cloudEvent.GetAttributes();
                    attrs["comexampleextension1"] = "value";
                    attrs["utf8examplevalue"] = "æøå";

                    await context.Response.CopyFromAsync(cloudEvent, ContentMode.Structured, new JsonEventFormatter());
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                }
                catch (Exception e)
                {
                    using (var sw = new StreamWriter(context.Response.OutputStream))
                    {
                        sw.Write(e.ToString());
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                }

                context.Response.Close();
            });

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add(testContextHeader, ctx);
            var result = await httpClient.GetAsync(new Uri(listenerAddress + "ep"));

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.True(result.IsCloudEvent());
            var receivedCloudEvent = result.ToCloudEvent();

            Assert.Equal(CloudEventsSpecVersion.V1_0, receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            AssertTimestampsEqual(sampleTimestamp, receivedCloudEvent.Time.Value);
            Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), receivedCloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

            var attr = receivedCloudEvent.GetAttributes();
            Assert.Equal("value", (string)attr["comexampleextension1"]);
            Assert.Equal("æøå", (string) attr["utf8examplevalue"]);
        }

        [Fact]
        async Task HttpStructuredClientSendTest()
        {
            var cloudEvent = new CloudEvent("com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"))
            {
                Id = "A234-1234-1234",
                Time = sampleTimestamp,
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["utf8examplevalue"] = "æøå";

            string ctx = Guid.NewGuid().ToString();
            var content = new CloudEventContent(cloudEvent, ContentMode.Structured, new JsonEventFormatter());
            content.Headers.Add(testContextHeader, ctx);

            pendingRequests.TryAdd(ctx, context =>
            {
                try
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
                    Assert.Equal("application/cloudevents+json", context.Request.ContentType);
                    Assert.Equal("value", headers["ce-comexampleextension1"]);
                    // The non-ASCII attribute value should have been URL-encoded using UTF-8 for the header.
                    Assert.Equal("%C3%A6%C3%B8%C3%A5", headers["ce-utf8examplevalue"]);

                    var receivedCloudEvent = context.Request.ToCloudEvent(new JsonEventFormatter());

                    Assert.Equal(CloudEventsSpecVersion.V1_0, receivedCloudEvent.SpecVersion);
                    Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
                    Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
                    Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
                    AssertTimestampsEqual(sampleTimestamp, cloudEvent.Time.Value);
                    Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), receivedCloudEvent.DataContentType);
                    Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

                    var attr = receivedCloudEvent.GetAttributes();
                    Assert.Equal("value", (string)attr["comexampleextension1"]);
                    Assert.Equal("æøå", (string)attr["utf8examplevalue"]);
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                }
                catch (Exception e)
                {
                    using (var sw = new StreamWriter(context.Response.OutputStream))
                    {
                        sw.Write(e.ToString());
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                }

                context.Response.Close();
                return Task.CompletedTask;
            });

            var httpClient = new HttpClient();
            var result = (await httpClient.PostAsync(new Uri(listenerAddress + "ep"), content));
            if (result.StatusCode != HttpStatusCode.NoContent)
            {
                throw new InvalidOperationException(result.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
        }

        [Fact]
        async Task HttpBinaryWebRequestSendTest()
        {
            var cloudEvent = new CloudEvent("com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"))
            {
                Id = "A234-1234-1234",
                Time = sampleTimestamp,
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["utf8examplevalue"] = "æøå";

            string ctx = Guid.NewGuid().ToString();
            HttpWebRequest httpWebRequest = WebRequest.CreateHttp(listenerAddress + "ep");
            httpWebRequest.Method = "POST";
            await httpWebRequest.CopyFromAsync(cloudEvent, ContentMode.Binary, new JsonEventFormatter());
            httpWebRequest.Headers.Add(testContextHeader, ctx);

            pendingRequests.TryAdd(ctx, context =>
            {
                try
                {
                    var receivedCloudEvent = context.Request.ToCloudEvent(new JsonEventFormatter());

                    Assert.Equal(CloudEventsSpecVersion.V1_0, receivedCloudEvent.SpecVersion);
                    Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
                    Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
                    Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
                    AssertTimestampsEqual(sampleTimestamp, cloudEvent.Time.Value);
                    Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), receivedCloudEvent.DataContentType);
                    using (var sr = new StreamReader((Stream) receivedCloudEvent.Data))
                    {
                        Assert.Equal("<much wow=\"xml\"/>", sr.ReadToEnd());
                    }

                    // The non-ASCII attribute value should have been URL-encoded using UTF-8 for the header.
                    Assert.Equal("%C3%A6%C3%B8%C3%A5", context.Request.Headers["ce-utf8examplevalue"]);

                    var attr = receivedCloudEvent.GetAttributes();
                    Assert.Equal("value", (string) attr["comexampleextension1"]);
                    Assert.Equal("æøå", (string) attr["utf8examplevalue"]);
                    context.Response.StatusCode = (int) HttpStatusCode.NoContent;
                }
                catch (Exception e)
                {
                    using (var sw = new StreamWriter(context.Response.OutputStream))
                    {
                        sw.Write(e.ToString());
                        context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                    }
                }

                context.Response.Close();
                return Task.CompletedTask;
            });

            var result = (HttpWebResponse) await httpWebRequest.GetResponseAsync();
            if (result.StatusCode != HttpStatusCode.NoContent)
            {
                throw new InvalidOperationException(result.StatusCode.ToString());
            }
        }

        [Fact]
        async Task HttpStructuredWebRequestSendTest()
        {
            var cloudEvent = new CloudEvent("com.github.pull.create",
                new Uri("https://github.com/cloudevents/spec/pull/123"))
            {
                Id = "A234-1234-1234",
                Time = sampleTimestamp,
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = "<much wow=\"xml\"/>"
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["utf8examplevalue"] = "æøå";

            string ctx = Guid.NewGuid().ToString();
            HttpWebRequest httpWebRequest = WebRequest.CreateHttp(listenerAddress + "ep");
            httpWebRequest.Method = "POST";
            await httpWebRequest.CopyFromAsync(cloudEvent, ContentMode.Structured, new JsonEventFormatter());
            httpWebRequest.Headers.Add(testContextHeader, ctx);

            pendingRequests.TryAdd(ctx, context =>
            {
                try
                {
                    // Structured events do not contain any CloudEvent HTTP headers.
                    Assert.Empty(context.Request.Headers.AllKeys.Where(key => key.StartsWith("ce-")));
                    
                    var receivedCloudEvent = context.Request.ToCloudEvent(new JsonEventFormatter());

                    Assert.Equal(CloudEventsSpecVersion.V1_0, receivedCloudEvent.SpecVersion);
                    Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
                    Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
                    Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
                    AssertTimestampsEqual(sampleTimestamp, cloudEvent.Time.Value);
                    Assert.Equal(new ContentType(MediaTypeNames.Text.Xml), receivedCloudEvent.DataContentType);
                    Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

                    var attr = receivedCloudEvent.GetAttributes();
                    Assert.Equal("value", (string)attr["comexampleextension1"]);
                    Assert.Equal("æøå", (string) attr["utf8examplevalue"]);
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                }
                catch (Exception e)
                {
                    using (var sw = new StreamWriter(context.Response.OutputStream))
                    {
                        sw.Write(e.ToString());
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                }

                context.Response.Close();
                return Task.CompletedTask;
            });

            var result = (HttpWebResponse)await httpWebRequest.GetResponseAsync();
            if (result.StatusCode != HttpStatusCode.NoContent)
            {
                throw new InvalidOperationException(result.StatusCode.ToString());
            }
        }
    }
}