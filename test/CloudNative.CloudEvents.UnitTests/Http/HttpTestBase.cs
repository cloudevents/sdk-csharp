// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        private readonly Task processingTask;
        private volatile bool disposed;

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
            processingTask = ProcessRequestsAsync();
        }

        public void Dispose()
        {
            // Note: we don't protected against multiple disposal, but that's not
            // expected to be a problem. (We're not disposing of this manually.)
            disposed = true;
            listener.Stop();
            if (!processingTask.Wait(1000))
            {
                throw new InvalidOperationException("Processing task did not complete");
            }
        }

        private async Task ProcessRequestsAsync()
        {
            while (!disposed)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                // The listener throws when it's stopped.
                // We want to handle that gracefully, but allow any other error to bubble up.
                catch (Exception e) when (disposed && (e is ObjectDisposedException || e is HttpListenerException))
                {
                    return;
                }
                try
                {
                    await HandleContext(context).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    var response = context.Response;
                    var responseContent = Encoding.UTF8.GetBytes($"Error processing request: {e}");
                    response.ContentLength64 = responseContent.Length;
                    response.StatusCode = 500;
                    response.OutputStream.Write(responseContent);
                }
                context.Response.Close();
            }
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
            else
            {
                throw new Exception($"Request with context header '{ctxHeaderValue}' was not handled");
            }
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
