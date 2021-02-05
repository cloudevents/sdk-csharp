// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
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
    }
}
