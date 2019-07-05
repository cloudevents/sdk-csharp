// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.IntegrationTests.AspNetCore
{
    using System;
    using System.Net;
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

        [Fact]
        public async Task Controller_WithValidCloudEvent_DeserializesUsingPipeline()
        {
            // Arrange
            var cloudEvent = new CloudEvent("test-type", new Uri("urn:integration-tests"))
            {
                Id = Guid.NewGuid().ToString(),
            };
            var content = new CloudEventContent(cloudEvent, ContentMode.Structured, new JsonEventFormatter());

            // Act
            var result = await _factory.CreateClient().PostAsync("/api/events/receive", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Contains(cloudEvent.Id, await result.Content.ReadAsStringAsync());
        }
    }
}
