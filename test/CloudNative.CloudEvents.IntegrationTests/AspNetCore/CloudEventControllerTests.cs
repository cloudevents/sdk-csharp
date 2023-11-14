// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.AspNetCoreSample;
using CloudNative.CloudEvents.Http;
using CloudNative.CloudEvents.NewtonsoftJson;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace CloudNative.CloudEvents.IntegrationTests.AspNetCore
{
    public class CloudEventControllerTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;

        public CloudEventControllerTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData(ContentMode.Structured)]
        [InlineData(ContentMode.Binary)]
        public async Task Controller_WithValidCloudEvent_NoContent_DeserializesUsingPipeline(ContentMode contentMode)
        {
            // Arrange
            var expectedExtensionKey = "comexampleextension1";
            var expectedExtensionValue = Guid.NewGuid().ToString();
            var cloudEvent = new CloudEvent
            {
                Type = "test-type-æøå",
                Source = new Uri("urn:integration-tests"),
                Id = Guid.NewGuid().ToString(),
                DataContentType = "application/json",
                Data = new { key1 = "value1" },
                [expectedExtensionKey] = expectedExtensionValue
            };

            var httpContent = cloudEvent.ToHttpContent(contentMode, new JsonEventFormatter());

            // Act
            var result = await _factory.CreateClient().PostAsync("/api/events/receive", httpContent);

            // Assert
            string resultContent = await result.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Contains(cloudEvent.Id, resultContent);
            Assert.Contains(cloudEvent.Type, resultContent);
            Assert.Contains($"\"{expectedExtensionKey}\": \"{expectedExtensionValue}\"", resultContent);
        }
    }
}
