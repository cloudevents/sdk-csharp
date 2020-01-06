// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.IntegrationTests.AspNetCore
{
    using System;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using CloudNative.CloudEvents.AspNetCoreSample;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Xunit;

    public class CloudEventControllerTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;

        public CloudEventControllerTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("application/cloudevents+json")]
        [InlineData("application/json")]
        public async Task Controller_WithValidCloudEvent_DeserializesUsingPipeline(string contentType)
        {
            // Arrange
            var expectedExtensionKey = "comexampleextension1";
            var expectedExtensionValue = Guid.NewGuid().ToString();
            var cloudEvent = new CloudEvent("test-type-æøå", new Uri("urn:integration-tests"))
            {
                Id = Guid.NewGuid().ToString(),
            };
            var attrs = cloudEvent.GetAttributes();
            attrs[expectedExtensionKey] = expectedExtensionValue;

            var content = new CloudEventContent(cloudEvent, ContentMode.Structured, new JsonEventFormatter());
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

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
