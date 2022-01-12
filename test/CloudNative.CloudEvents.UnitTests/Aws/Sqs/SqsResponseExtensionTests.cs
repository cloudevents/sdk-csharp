// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using Amazon.SQS.Model;
using CloudNative.CloudEvents.AwsSns;
using CloudNative.CloudEvents.AwsSqs;
using CloudNative.CloudEvents.NewtonsoftJson;
using Xunit;
using MessageAttributeValueSns = Amazon.SimpleNotificationService.Model.MessageAttributeValue;
using MessageAttributeValueSqs = Amazon.SQS.Model.MessageAttributeValue;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.UnitTests.AwsSqs
{
    public class SqsResponseExtensionTests
    {
        [Fact]
        public void ItShouldConvertInContentModeWhenContentTypeIsApplicationCloudEvent()
        {
            var sqsResponse = new ReceiveMessageResponse
            {
                Messages = new List<Message>
                {
                    new Message
                    {
                        Body =
                            "{\"specversion\":\"1.0\",\"id\":\"Test Id\",\"source\":\"https://www.test_source\",\"type\":\"Test cloud event\",\"datacontenttype\":\"application/cloudevents+json; charset=utf-8\",\"data\":\"<much wow=\\\"xml\\\"/>\"}",
                        MessageAttributes = new Dictionary<string, MessageAttributeValueSqs>
                        {
                            {
                                "content-type",
                                new MessageAttributeValueSqs
                                {
                                    DataType = "String",
                                    StringValue = "application/cloudevents+json; charset=utf-8"
                                }
                            }
                        }
                    }
                }
            };

            var cloudEvent = sqsResponse.ToCloudEvents(new JsonEventFormatter()).Single();

            var expected = GetSampleCloudEvent("application/cloudevents+json; charset=utf-8");
            AssertCloudEventsEqual(expected, cloudEvent);
        }

        [Fact]
        public void ItShouldConvertInBinaryModeWhenContentTypeIsNotApplicationCloudEvent()
        {
            var sqsResponse = new ReceiveMessageResponse
            {
                Messages = new List<Message>
                {
                    new Message
                    {
                        Body = "PG11Y2ggd293PSJ4bWwiLz4=",
                        MessageAttributes = new Dictionary<string, MessageAttributeValueSqs>
                        {
                            {
                                "spec-version",
                                new MessageAttributeValueSqs
                                {
                                    DataType = "String",
                                    StringValue = CloudEventsSpecVersion.V1_0.VersionId
                                }
                            },
                            {
                                "content-type",
                                new MessageAttributeValueSqs
                                {
                                    DataType = "String",
                                    StringValue = MediaTypeNames.Text.Xml
                                }
                            },
                            {
                                $"{SqsResponseExtensions.CloudEventAttributeKeyPrefix}id",
                                new MessageAttributeValueSqs
                                {
                                    DataType = "String",
                                    StringValue = "Test Id"
                                }
                            },
                            {
                                $"{SqsResponseExtensions.CloudEventAttributeKeyPrefix}type",
                                new MessageAttributeValueSqs
                                {
                                    DataType = "String",
                                    StringValue = "Test cloud event"
                                }
                            },
                            {
                                $"{SqsResponseExtensions.CloudEventAttributeKeyPrefix}source",
                                new MessageAttributeValueSqs
                                {
                                    DataType = "String",
                                    StringValue = "https://www.test_source"
                                }
                            }
                        }
                    }
                }
            };

            var cloudEvent = sqsResponse.ToCloudEvents(new JsonEventFormatter()).Single();

            var expected = GetSampleCloudEvent("text/xml");
            AssertCloudEventsEqual(expected, cloudEvent);
        }

        [Fact]
        public void ItShouldThrowWhenConvertingWithNoAttributes()
        {
            var sqsResponse = new ReceiveMessageResponse
            {
                Messages = new List<Message>
                {
                    new Message
                    {
                        Body = "Message",
                        MessageAttributes = new Dictionary<string, MessageAttributeValueSqs>()
                    }
                }
            };

            Assert.Throws<ArgumentException>(() => sqsResponse.ToCloudEvents(new JsonEventFormatter()))
                .Message.Contains("Received SQS message has no attributes.");
        }

        [Fact]
        public void ItShouldThrowWhenConvertingInContentModeWithMissingContentTypeAttribute()
        {
            var sqsResponse = new ReceiveMessageResponse
            {
                Messages = new List<Message>
                {
                    new Message
                    {
                        Body = "Message",
                        MessageAttributes = new Dictionary<string, MessageAttributeValueSqs>
                        {
                            {
                                "some-attribute",
                                new MessageAttributeValueSqs
                                {
                                    DataType = "String",
                                    StringValue = "some value"
                                }
                            }
                        }
                    }
                }
            };

            Assert.Throws<ArgumentException>(() => sqsResponse.ToCloudEvents(new JsonEventFormatter()))
                .Message.Contains("Missing CloudEvent content type attribute.");
        }


        [Fact]
        public void ItShouldThrowWhenConvertingInBinaryModeWithMissingContentTypeAttribute()
        {
            var sqsResponse = new ReceiveMessageResponse
            {
                Messages = new List<Message>
                {
                    new Message
                    {
                        Body = "Message",
                        MessageAttributes = new Dictionary<string, MessageAttributeValueSqs>
                        {
                            {
                                "spec-version",
                                new MessageAttributeValueSqs
                                {
                                    DataType = "String",
                                    StringValue = CloudEventsSpecVersion.V1_0.VersionId
                                }
                            }
                        }
                    }
                }
            };

            Assert.Throws<ArgumentException>(() => sqsResponse.ToCloudEvents(new JsonEventFormatter()))
                .Message.Contains("Missing CloudEvent content type attribute.");
        }


        [Fact]
        public void ItShouldThrowWhenConvertingInBinaryModeWithUnknownSpecVersion()
        {
            var sqsResponse = new ReceiveMessageResponse
            {
                Messages = new List<Message>
                {
                    new Message
                    {
                        Body = "Message",
                        MessageAttributes = new Dictionary<string, MessageAttributeValueSqs>
                        {
                            {
                                "content-type",
                                new MessageAttributeValueSqs
                                {
                                    DataType = "String",
                                    StringValue = MediaTypeNames.Text.Xml
                                }
                            },
                            {
                                "spec-version",
                                new MessageAttributeValueSqs
                                {
                                    DataType = "String",
                                    StringValue = "random value"
                                }
                            }
                        }
                    }
                }
            };

            Assert.Throws<ArgumentException>(() => sqsResponse.ToCloudEvents(new JsonEventFormatter()))
                .Message.Contains("Unknown CloudEvent spec version attribute");
        }


        [Fact]
        public void ItShouldThrowWhenConvertingToCloudEventsWithNullFormatter()
        {
#nullable disable
            Assert.Throws<ArgumentNullException>(() => new ReceiveMessageResponse().ToCloudEvents(null));
#nullable enable
        }

        [Fact]
        public void ItShouldThrowWhenConvertingToCloudEventsWithNullResponse()
        {
#nullable disable
            ReceiveMessageResponse sqsResponse = null;
            Assert.Throws<ArgumentNullException>(() => sqsResponse.ToCloudEvents(new JsonEventFormatter()));
#nullable enable
        }

        [Fact]
        public void AttributeValuesShouldBeEqual()
        {
            Assert.Equal(SnsPublishRequestExtensions.SpecVersionAttributeKey,
                SqsResponseExtensions.SpecVersionAttributeKey);
            Assert.Equal(SnsPublishRequestExtensions.ContentTypeAttributeKey,
                SqsResponseExtensions.ContentTypeAttributeKey);
            Assert.Equal(SnsPublishRequestExtensions.CloudEventAttributeKeyPrefix,
                SqsResponseExtensions.CloudEventAttributeKeyPrefix);
        }

        private static CloudEvent GetSampleCloudEvent(string contentType)
        {
            return new CloudEvent
            {
                Id = "Test Id",
                Source = new Uri("https://www.test_source"),
                Type = "Test cloud event",
                DataContentType = contentType,
                Data = "<much wow=\"xml\"/>"
            };
        }
    }
}