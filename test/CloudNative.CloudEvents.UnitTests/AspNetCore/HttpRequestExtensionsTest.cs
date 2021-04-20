// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.NewtonsoftJson;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using System;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.AspNetCore.UnitTests
{
    public class HttpRequestExtensionsTest
    {
        // TODO: Non-batch tests

        [Fact]
        public async Task ToCloudEventBatchAsync_Valid()
        {
            var batch = CreateSampleBatch();

            var formatter = new JsonEventFormatter();
            var contentBytes = formatter.EncodeBatchModeMessage(batch, out var contentType);

            AssertBatchesEqual(batch, await CreateRequest(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionArray));
            AssertBatchesEqual(batch, await CreateRequest(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionSequence));
        }

        [Fact]
        public async Task ToCloudEventBatchAsync_Invalid()
        {
            // Most likely accident: calling ToCloudEventBatchAsync with a single event in structured mode.
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var formatter = new JsonEventFormatter();
            var contentBytes = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
            await Assert.ThrowsAsync<ArgumentException>(() => CreateRequest(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionArray).AsTask());
            await Assert.ThrowsAsync<ArgumentException>(() => CreateRequest(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionSequence).AsTask());
        }

        private static HttpRequest CreateRequest(byte[] content, ContentType contentType) =>
            new DefaultHttpRequest(new DefaultHttpContext())
            {
                ContentType = contentType.ToString(),
                Body = new MemoryStream(content)
            };
    }
}
