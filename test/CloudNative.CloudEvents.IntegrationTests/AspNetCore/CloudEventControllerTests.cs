// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.IntegrationTests.AspNetCore
{
    using CloudNative.CloudEvents.AspNetCoreSample;
    using Microsoft.AspNetCore.Mvc.Testing;
    using System;
    using System.Net;
    using System.Net.Mime;
    using System.Threading.Tasks;
    using Xunit;

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
        public async Task Controller_WithValidCloudEvent_DeserializesUsingPipeline(ContentMode contentMode)
        {
            // Arrange
            var expectedExtensionKey = "comexampleextension1";
            var expectedExtensionValue = Guid.NewGuid().ToString();
            var cloudEvent = new CloudEvent("test-type-æøå", new Uri("urn:integration-tests"))
            {
                Id = Guid.NewGuid().ToString(),
                DataContentType = new ContentType("application/json")
            };
            var attrs = cloudEvent.GetAttributes();
            attrs[expectedExtensionKey] = expectedExtensionValue;

            var content = new CloudEventContent(cloudEvent, contentMode, new JsonEventFormatter());

            // Act
            var result = await _factory.CreateClient().PostAsync("/api/events/receive", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Contains(cloudEvent.Id, await result.Content.ReadAsStringAsync());
            Assert.Contains(cloudEvent.Type, await result.Content.ReadAsStringAsync());
            Assert.Contains($"\"{expectedExtensionKey}\":\"{expectedExtensionValue}\"", await result.Content.ReadAsStringAsync());
        }
    }
}
