// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService.Model;
using CloudNative.CloudEvents.AwsSns;
using CloudNative.CloudEvents.AwsSqs;
using CloudNative.CloudEvents.NewtonsoftJson;
using Xunit;
using static CloudNative.CloudEvents.IntegrationTests.TestHelpers;

namespace CloudNative.CloudEvents.IntegrationTests.Aws.Sns
{
    [Collection("LocalStack")]
    public class AwsSnsIntegrationTests : IAsyncLifetime
    {
        private readonly AwsSqsSnsClientAdapter _snsSqsAdapter;

        public AwsSnsIntegrationTests(LocalStackTestCollectionFixture testFixture)
        {
            _snsSqsAdapter = new AwsSqsSnsClientAdapter(
                testFixture.LocalStackContainer.AwsCredentials,
                testFixture.LocalStackContainer.ServiceUrl);
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await _snsSqsAdapter.DisposeAsync();
        }

        [Fact]
        public async Task ItShouldPublishMessageInStructuredContentMode()
        {
            var infrastructure =  await _snsSqsAdapter.CreateTopicWithSubscribedQueueAsync(rawMessageDelivery: true);
            var formatter = new JsonEventFormatter();

            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull"),
                Subject = "123",
                Id = "A234-1234-1234",
                Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero),
                DataContentType = MediaTypeNames.Text.Xml,
                Data = "<much wow=\"xml\"/>",
                ["comexampleextension1"] = "value"
            };

            var publishRequest = new PublishRequest
            {
                TopicArn = infrastructure.TopicArn,
            };

            cloudEvent.CopyToSnsPublishRequest(publishRequest, ContentMode.Structured, formatter);

            await _snsSqsAdapter.SnsPublishMessageAsync(publishRequest);

            var message = await _snsSqsAdapter.SqsReceiveMessageAsync(infrastructure.QueueUrl);

            var cloudEvents = message.ToCloudEvents(formatter);
            var receivedCloudEvent = cloudEvents.Single();

            AssertCloudEventsEqual(cloudEvent, receivedCloudEvent);
        }

        [Fact]
        public async Task ItShouldPublishMessageInBinaryContentMode()
        {
            var infrastructure = await _snsSqsAdapter.CreateTopicWithSubscribedQueueAsync(rawMessageDelivery: true);
            var formatter = new JsonEventFormatter();

            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull"),
                Subject = "123",
                Id = "A234-1234-1234",
                Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero),
                DataContentType = MediaTypeNames.Text.Xml,
                Data = "<much wow=\"xml\"/>",
                ["comexampleextension1"] = "test value",
                ["comexampleextension2"] = "extension 2",
                ["comexampleextension3"] = "extension 3",
                ["comexampleextension4"] = "4 extension",
            };

            var publishRequest = new PublishRequest
            {
                TopicArn = infrastructure.TopicArn,
            };

            cloudEvent.CopyToSnsPublishRequest(publishRequest, ContentMode.Binary, formatter);

            await _snsSqsAdapter.SnsPublishMessageAsync(publishRequest);

            var message = await _snsSqsAdapter.SqsReceiveMessageAsync(infrastructure.QueueUrl);

            var cloudEvents = message.ToCloudEvents(formatter);
            var receivedCloudEvent = cloudEvents.Single();

            AssertCloudEventsEqual(cloudEvent, receivedCloudEvent);
        }

        [Fact]
        public async Task ItShouldFailInBinaryContentModeWhenRawMessageDeliveryIsDisabled()
        {
            var infrastructure = await _snsSqsAdapter.CreateTopicWithSubscribedQueueAsync(rawMessageDelivery: false);
            var formatter = new JsonEventFormatter();

            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull"),
                Subject = "123",
                Id = "A234-1234-1234",
                Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero),
                DataContentType = MediaTypeNames.Text.Xml,
                Data = "<much wow=\"xml\"/>",
                ["comexampleextension1"] = "test value",
                ["comexampleextension2"] = "extension 2",
                ["comexampleextension3"] = "extension 3",
                ["comexampleextension4"] = "4 extension",
            };

            var publishRequest = new PublishRequest
            {
                TopicArn = infrastructure.TopicArn,
            };

            cloudEvent.CopyToSnsPublishRequest(publishRequest, ContentMode.Binary, formatter);

            await _snsSqsAdapter.SnsPublishMessageAsync(publishRequest);

            var message = await _snsSqsAdapter.SqsReceiveMessageAsync(infrastructure.QueueUrl);

            Assert.Throws<ArgumentException>(() => message.ToCloudEvents(formatter));
        }
    }
}