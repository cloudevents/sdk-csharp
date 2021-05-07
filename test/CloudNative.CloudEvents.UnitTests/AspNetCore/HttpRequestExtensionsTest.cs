// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.NewtonsoftJson;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.AspNetCore.UnitTests
{
    public class HttpRequestExtensionsTest
    {
        public static TheoryData<string, string, IDictionary<string, string>> SingleCloudEventMessages = new TheoryData<string, string, IDictionary<string, string>>
        {
            {
                "Binary",
                "text/plain",
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
                "application/cloudevents+json",
                null
            }
        };

        public static TheoryData<string, string, IDictionary<string, string>> BatchMessages = new TheoryData<string, string, IDictionary<string, string>>
        {
            {
                "Batch",
                "application/cloudevents-batch+json",
                null
            }
        };

        public static TheoryData<string, string, IDictionary<string, string>> NonCloudEventMessages = new TheoryData<string, string, IDictionary<string, string>>
        {
            {
                "Plain text",
                "text/plain",
                null
            }
        };

        [Theory]
        [MemberData(nameof(SingleCloudEventMessages))]
        public void IsCloudEvent_True(string description, string contentType, IDictionary<string, string> headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = CreateRequest(new byte[0], new ContentType(contentType));
            CopyHeaders(headers, request);
            Assert.True(request.IsCloudEvent());
        }

        [Theory]
        [MemberData(nameof(BatchMessages))]
        [MemberData(nameof(NonCloudEventMessages))]
        public void IsCloudEvent_False(string description, string contentType, IDictionary<string, string> headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = CreateRequest(new byte[0], new ContentType(contentType));
            CopyHeaders(headers, request);
            Assert.False(request.IsCloudEvent());
        }

        [Theory]
        [MemberData(nameof(BatchMessages))]
        public void IsCloudEventBatch_True(string description, string contentType, IDictionary<string, string> headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = CreateRequest(new byte[0], new ContentType(contentType));
            CopyHeaders(headers, request);
            Assert.True(request.IsCloudEventBatch());
        }

        [Theory]
        [MemberData(nameof(SingleCloudEventMessages))]
        [MemberData(nameof(NonCloudEventMessages))]
        public void IsCloudEventBatch_False(string description, string contentType, IDictionary<string, string> headers)
        {
            // Really only present for display purposes.
            Assert.NotNull(description);

            var request = CreateRequest(new byte[0], new ContentType(contentType));
            CopyHeaders(headers, request);
            Assert.False(request.IsCloudEventBatch());
        }

        // TODO: Non-batch conversion tests

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
            await Assert.ThrowsAsync<ArgumentException>(() => CreateRequest(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionArray));
            await Assert.ThrowsAsync<ArgumentException>(() => CreateRequest(contentBytes, contentType).ToCloudEventBatchAsync(formatter, EmptyExtensionSequence));
        }

        private static HttpRequest CreateRequest(byte[] content, ContentType contentType) =>
            new DefaultHttpRequest(new DefaultHttpContext())
            {
                ContentType = contentType.ToString(),
                Body = new MemoryStream(content)
            };

        private static void CopyHeaders(IDictionary<string, string> source, HttpRequest target)
        {
            if (source is null)
            {
                return;
            }
            foreach (var header in source)
            {
                target.Headers.Add(header.Key, header.Value);
            }
        }
    }
}
