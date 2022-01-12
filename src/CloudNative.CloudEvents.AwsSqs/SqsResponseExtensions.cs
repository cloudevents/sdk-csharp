// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using Amazon.SQS.Model;
using CloudNative.CloudEvents.Core;

namespace CloudNative.CloudEvents.AwsSqs
{
    /// <summary>
    ///     Extension methods to convert between CloudEvents and AWS.SQS response.
    /// </summary>
    public static class SqsResponseExtensions
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
        /// </summary>
        /// <param name="sqsReceiveMessageResponse"></param>
        /// <param name="formatter"></param>
        /// <param name="extensionAttributes"></param>
        /// <returns></returns>
        public static ICollection<CloudEvent> ToCloudEvents(
            this ReceiveMessageResponse sqsReceiveMessageResponse,
            CloudEventFormatter formatter,
            IEnumerable<CloudEventAttribute>? extensionAttributes = null)
        {
            Validation.CheckNotNull(sqsReceiveMessageResponse, nameof(sqsReceiveMessageResponse));
            Validation.CheckNotNull(formatter, nameof(formatter));

            return sqsReceiveMessageResponse
                .Messages
                .Select(message => ToCloudEventInternal(message, formatter, extensionAttributes))
                .ToList();
        }

        private static CloudEvent ToCloudEventInternal(
            Message message,
            CloudEventFormatter formatter,
            IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            if (!message.MessageAttributes.Any())
            {
                throw new ArgumentException(@"Received SQS message has no attributes. 
                    If you are using SNS to SQS publication check if RawMessageDelivery mode is ENABLED, otherwise attributes are dropped in AWS.");
            }

            var contentType = ReadContentTypeAttribute(message);

            if (MimeUtilities.IsCloudEventsContentType(contentType.ToString()))
            {
                var encoding = MimeUtilities.GetEncoding(contentType);
                var bytes = encoding.GetBytes(message.Body);
                var cloudEvent =
                    formatter.DecodeStructuredModeMessage(new MemoryStream(bytes), contentType, extensionAttributes);
                return Validation.CheckCloudEventArgument(cloudEvent, nameof(message));
            }
            else
            {
                var specVersion = ReadSpecVersion(message);

                var cloudEvent = new CloudEvent(specVersion, extensionAttributes)
                {
                    DataContentType = contentType.ToString()
                };

                foreach (var messageAttribute in message.MessageAttributes)
                {
                    if (messageAttribute.Key.StartsWith(CloudEventAttributeKeyPrefix))
                    {
                        var attributeName = messageAttribute.Key.Substring(CloudEventAttributeKeyPrefix.Length)
                            .ToLowerInvariant();
                        cloudEvent.SetAttributeFromString(attributeName, messageAttribute.Value.StringValue);
                    }
                }

                var messageBytes = Convert.FromBase64String(message.Body);
                formatter.DecodeBinaryModeEventData(messageBytes, cloudEvent);
                return Validation.CheckCloudEventArgument(cloudEvent, nameof(message));
            }
        }

        private static ContentType ReadContentTypeAttribute(Message message)
        {
            message.MessageAttributes.TryGetValue(ContentTypeAttributeKey, out var contentType);

            if (contentType?.StringValue is null)
            {
                throw new ArgumentException("Missing CloudEvent content type attribute.", nameof(message));
            }

            return new ContentType(contentType.StringValue);
        }

        private static CloudEventsSpecVersion ReadSpecVersion(Message message)
        {
            message.MessageAttributes.TryGetValue(SpecVersionAttributeKey, out var specVersionAttribute);

            if (specVersionAttribute?.StringValue is null)
            {
                throw new ArgumentException("Missing CloudEvent spec version attribute.", nameof(message));
            }

            var specVersion = CloudEventsSpecVersion.FromVersionId(specVersionAttribute.StringValue);

            if (specVersion is null)
            {
                throw new ArgumentException(
                    $"Unknown CloudEvent spec version attribute '{specVersionAttribute.StringValue}'.",
                    nameof(message));
            }

            return specVersion;
        }
    }
}