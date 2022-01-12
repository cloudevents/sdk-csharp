// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Net.Mime;
using Amazon.SimpleNotificationService.Model;
using CloudNative.CloudEvents.AwsSns;
using CloudNative.CloudEvents.NewtonsoftJson;
using Xunit;
using MessageAttributeValueSns = Amazon.SimpleNotificationService.Model.MessageAttributeValue;
using MessageAttributeValueSqs = Amazon.SQS.Model.MessageAttributeValue;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.UnitTests.AwsSns
{
    public class SnsRequestExtensionTests
    {
        [Theory]
        [InlineData(ContentMode.Structured)]
        [InlineData(ContentMode.Binary)]
        public void ShouldNotChangeAnySnsPublishRequestAttributesWhenCopyTo(ContentMode contentMode)
        {
            // Given 
            var jsonEventFormatter = new JsonEventFormatter();

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
                TopicArn = "Test topic",
                MessageDeduplicationId = Guid.NewGuid().ToString("N"),
                MessageGroupId = Guid.NewGuid().ToString("N"),
                PhoneNumber = "1245",
                Subject = "Message subject",
                TargetArn = "Test target"
            };
            publishRequest.MessageAttributes.Add("my-custom-attribute", new MessageAttributeValueSns
            {
                StringValue = "my-custom-attribute-value",
                DataType = "String"
            });

            var publishRequestCopy = DeepCopy(publishRequest);

            // When
            cloudEvent.CopyToSnsPublishRequest(publishRequest, contentMode, jsonEventFormatter);

            // Then
            var customAttribute = GetMessageAttribute(publishRequest, "my-custom-attribute");
            Assert.Equal("String", customAttribute.DataType);
            Assert.Equal("my-custom-attribute-value", customAttribute.StringValue);
            Assert.Equal(publishRequestCopy.MessageDeduplicationId, publishRequest.MessageDeduplicationId);
            Assert.Equal(publishRequestCopy.MessageGroupId, publishRequest.MessageGroupId);
            Assert.Equal(publishRequestCopy.PhoneNumber, publishRequest.PhoneNumber);
            Assert.Equal(publishRequestCopy.Subject, publishRequest.Subject);
            Assert.Equal(publishRequestCopy.TargetArn, publishRequest.TargetArn);
            Assert.Equal(publishRequestCopy.TopicArn, publishRequest.TopicArn);
        }

        [Fact]
        public void ShouldAssignContentTypeAttributeInStructuredContentMode()
        {
            // Given 
            const string expectedContentType = "application/cloudevents+json; charset=utf-8";
            var jsonEventFormatter = new JsonEventFormatter();

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

            // When
            var publishRequest = cloudEvent.ToSnsPublishRequest(ContentMode.Structured, jsonEventFormatter);

            // Then
            var contentTypeAttribute = GetMessageAttribute(publishRequest, "content-type");
            Assert.Equal("String", contentTypeAttribute.DataType);
            Assert.Equal(expectedContentType, contentTypeAttribute.StringValue);
            Assert.Equal("String", publishRequest.MessageStructure);
        }

        [Theory]
        [InlineData(MediaTypeNames.Text.Xml, MediaTypeNames.Text.Xml)]
        [InlineData(MediaTypeNames.Text.Plain, MediaTypeNames.Text.Plain)]
        [InlineData(null, "application/json; charset=utf-8")]
        public void ItShouldAssignContentTypeAttributeInBinaryContentMode(string? dataContentType,
            string expectedContentType)
        {
            // Given 
            var jsonEventFormatter = new JsonEventFormatter();

            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull"),
                Subject = "123",
                Id = "A234-1234-1234",
                Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero),
                DataContentType = dataContentType,
                Data = "<much wow=\"xml\"/>",
                ["comexampleextension1"] = "value"
            };

            // When
            var publishRequest = cloudEvent.ToSnsPublishRequest(ContentMode.Binary, jsonEventFormatter);

            // Then
            var contentTypeAttribute = GetMessageAttribute(publishRequest, "content-type");
            Assert.Equal("String", contentTypeAttribute.DataType);
            Assert.Equal(expectedContentType, contentTypeAttribute.StringValue);
            Assert.Equal("String", publishRequest.MessageStructure);
        }

        [Theory]
        [InlineData(ContentMode.Structured)]
        [InlineData(ContentMode.Binary)]
        public void ShouldAssignMessageStructureProperty(ContentMode contentMode)
        {
            var publishRequest = GetMinimalCloudEvent().ToSnsPublishRequest(contentMode, new JsonEventFormatter());
            Assert.Equal("String", publishRequest.MessageStructure);
        }

        [Fact]
        public void ShouldThrowWhenCreateSnsPublishRequestWithInvalidCloudEvent()
        {
            Assert.Throws<ArgumentException>(() =>
                new CloudEvent()
                    .ToSnsPublishRequest(ContentMode.Structured, new JsonEventFormatter()));
        }

        [Fact]
        public void ShouldThrowWhenCopyToSnsPublishRequestWithInvalidCloudEvent()
        {
            Assert.Throws<ArgumentException>(() =>
                new CloudEvent()
                    .CopyToSnsPublishRequest(new PublishRequest(), ContentMode.Structured, new JsonEventFormatter()));
        }

        [Fact]
        public void ShouldThrowWhenCreateSnsPublishRequestWithNullFormatter()
        {
#nullable disable
            Assert.Throws<ArgumentNullException>(() =>
                GetMinimalCloudEvent()
                    .ToSnsPublishRequest(ContentMode.Structured, null));
#nullable enable
        }

        [Fact]
        public void ShouldThrowWhenCopyToSnsPublishRequestWithNullFormatter()
        {
#nullable disable
            Assert.Throws<ArgumentNullException>(() =>
                GetMinimalCloudEvent()
                    .CopyToSnsPublishRequest(new PublishRequest(), ContentMode.Structured, null));
#nullable enable
        }

        [Fact]
        public void ShouldThrowWhenCopyToSnsPublishRequestWithNullPublishRequest()
        {
#nullable disable
            Assert.Throws<ArgumentNullException>(() =>
                GetMinimalCloudEvent()
                    .CopyToSnsPublishRequest(null, ContentMode.Structured, new JsonEventFormatter()));
#nullable enable
        }

        private static MessageAttributeValueSns GetMessageAttribute(PublishRequest publishRequest, string key)
        {
            return publishRequest
                .MessageAttributes
                .Single(c => c.Key == key)
                .Value;
        }

        private static CloudEvent GetMinimalCloudEvent()
        {
            return new CloudEvent().PopulateRequiredAttributes();
        }
    }
}