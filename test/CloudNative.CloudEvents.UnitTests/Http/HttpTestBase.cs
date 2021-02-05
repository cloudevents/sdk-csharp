// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.Http.UnitTests
{
    /// <summary>
    /// Base class for HTTP tests, which sets up an HttpListener.
    /// </summary>
    public abstract class HttpTestBase : IDisposable
    {
        internal static readonly DateTimeOffset SampleTimestamp = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero);
        internal string ListenerAddress { get; }
        internal const string TestContextHeader = "testcontext";
        private readonly HttpListener listener;

        internal ConcurrentDictionary<string, Func<HttpListenerContext, Task>> PendingRequests { get; } =
            new ConcurrentDictionary<string, Func<HttpListenerContext, Task>>();

        public HttpTestBase()
        {
            var port = GetRandomUnusedPort();
            ListenerAddress = $"http://localhost:{port}/";
            listener = new HttpListener()
            {
                AuthenticationSchemes = AuthenticationSchemes.Anonymous,
                Prefixes = { ListenerAddress }
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

        private async Task HandleContext(HttpListenerContext requestContext)
        {
            var ctxHeaderValue = requestContext.Request.Headers[TestContextHeader];

            if (requestContext.Request.IsWebHookValidationRequest())
            {
                await requestContext.HandleAsWebHookValidationRequest(null, null);
                return;
            }

            if (PendingRequests.TryRemove(ctxHeaderValue, out var pending))
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

        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
