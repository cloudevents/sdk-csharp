// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Mime;
using Amazon.SimpleNotificationService.Model;
using CloudNative.CloudEvents.Core;

namespace CloudNative.CloudEvents.AwsSns
{
    /// <summary>
    ///     Extension methods to convert between CloudEvents and AWS.SNS request.
    /// </summary>
    public static class SnsPublishRequestExtensions
    {
        /// <summary>
        ///     Message attribute key for ContentType
        /// </summary>
        public const string ContentTypeAttributeKey = "content-type";

        /// <summary>
        ///     Message attribute key for SpecVersion
        /// </summary>
        public const string SpecVersionAttributeKey = "spec-version";

        /// <summary>
        ///     A prefix for cloud event attributes keys to distinguish them from other.
        /// </summary>
        public const string CloudEventAttributeKeyPrefix = "cloud_event_";

        /// <summary>
        ///     Converts <see cref="CloudEvent" /> into new instance of <see cref="PublishRequest" />.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <returns>New instance of <see cref="PublishRequest" /></returns>
        public static PublishRequest ToSnsPublishRequest(
            this CloudEvent cloudEvent,
            ContentMode contentMode,
            CloudEventFormatter formatter)
        {
            return cloudEvent.CopyToSnsPublishRequest(new PublishRequest(), contentMode, formatter);
        }

        /// <summary>
        ///     Assign <see cref="CloudEvent" /> to existing instance of <see cref="PublishRequest" />.
        /// </summary>
        /// <param name="publishRqRequest">The instance of PublishRequest to which serialized cloud event data will be assigned.</param>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <returns>
        ///     <see cref="PublishRequest" />
        /// </returns>
        public static PublishRequest CopyToSnsPublishRequest(
            this CloudEvent cloudEvent,
            PublishRequest publishRqRequest,
            ContentMode contentMode,
            CloudEventFormatter formatter)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
            Validation.CheckNotNull(publishRqRequest, nameof(publishRqRequest));
            Validation.CheckNotNull(formatter, nameof(formatter));

            var message = Serialize(cloudEvent, contentMode, formatter);

            publishRqRequest.Message = message.Message;
            publishRqRequest.MessageStructure = "String";

            foreach (var messageAttribute in message.MessageAttributes)
            {
                publishRqRequest.MessageAttributes.Add(messageAttribute.Key, messageAttribute.Value);
            }

            return publishRqRequest;
        }

        /// <summary>
        ///     Serialize CloudEvent
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <returns></returns>
        public static (string Message, Dictionary<string, MessageAttributeValue> MessageAttributes) Serialize(
            CloudEvent cloudEvent,
            ContentMode contentMode,
            CloudEventFormatter formatter)
        {
            if (contentMode == ContentMode.Structured)
            {
                return SerializeInStructuredMode(cloudEvent, formatter);
            }

            return SerializeInBinaryMode(cloudEvent, formatter);
        }

        private static (string Message, Dictionary<string, MessageAttributeValue> MessageAttributes)
            SerializeInStructuredMode(
                CloudEvent cloudEvent,
                CloudEventFormatter formatter)
        {
            var bytesRepresentation =
                BinaryDataUtilities.AsArray(formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType));

            var encoding = MimeUtilities.GetEncoding(contentType);

            var message = encoding.GetString(bytesRepresentation);

            var messageAttributes = new Dictionary<string, MessageAttributeValue>()
                .AddContentTypeAttribute(contentType);

            return (Message: message, MessageAttributes: messageAttributes);
        }

        private static (string Message, Dictionary<string, MessageAttributeValue> MessageAttributes)
            SerializeInBinaryMode(
                CloudEvent cloudEvent,
                CloudEventFormatter formatter)
        {
            var bytesRepresentation =
                BinaryDataUtilities.AsArray(formatter.EncodeBinaryModeEventData(cloudEvent, out var contentType));

            var message = Convert.ToBase64String(bytesRepresentation);

            var messageAttributes = new Dictionary<string, MessageAttributeValue>()
                .AddContentTypeAttribute(contentType)
                .AddSpecVersionAttribute(cloudEvent)
                .AddCloudEventAttributes(cloudEvent);

            return (Message: message, MessageAttributes: messageAttributes);
        }

        private static Dictionary<string, MessageAttributeValue> AddContentTypeAttribute(
            this Dictionary<string, MessageAttributeValue> attributes,
            ContentType? contentType)
        {
            if (contentType != null)
            {
                attributes.Add(
                    ContentTypeAttributeKey,
                    new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = contentType.ToString()
                    });
            }

            return attributes;
        }

        private static Dictionary<string, MessageAttributeValue> AddSpecVersionAttribute(
            this Dictionary<string, MessageAttributeValue> attributes,
            CloudEvent cloudEvent)
        {
            attributes.Add(SpecVersionAttributeKey,
                new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = cloudEvent.SpecVersion.VersionId
                });
            return attributes;
        }

        private static Dictionary<string, MessageAttributeValue> AddCloudEventAttributes(
            this Dictionary<string, MessageAttributeValue> attributes,
            CloudEvent cloudEvent)
        {
            foreach (var cloudEventAttributePair in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = cloudEventAttributePair.Key;
                var attributeValue = attribute.Format(cloudEventAttributePair.Value);

                attributes.Add($"{CloudEventAttributeKeyPrefix}{attribute.Name}",
                    new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = attributeValue
                    });
            }

            return attributes;
        }
    }
}