// Copyright 2025 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents;

/// <summary>
/// Extension methods for <see cref="CloudEventFormatter"/>.
/// </summary>
public static class CloudEventFormatters
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="formatter"></param>
    /// <param name="extensionAttributes"></param>
    /// <returns></returns>
    public static CloudEventFormatter WithDefaultDecodingExtensions(this CloudEventFormatter formatter, IEnumerable<CloudEventAttribute> extensionAttributes) =>
        new DefaultExtensionsFormatter(formatter, extensionAttributes);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="formatter"></param>
    /// <param name="validationAction"></param>
    /// <returns></returns>
    public static CloudEventFormatter WithValidation(this CloudEventFormatter formatter, Action<CloudEvent> validationAction) =>
        WithValidation(formatter, validationAction, validationAction);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="formatter"></param>
    /// <param name="encodingValidationAction"></param>
    /// <param name="decodingValidationAction"></param>
    /// <returns></returns>
    public static CloudEventFormatter WithValidation(this CloudEventFormatter formatter, Action<CloudEvent>? encodingValidationAction, Action<CloudEvent>? decodingValidationAction) =>
        new ValidatingFormatter(formatter, encodingValidationAction, decodingValidationAction);

    /// <summary>
    /// Delegating formatter which applies additional validation before each encoding method, and/or after each decoding method.
    /// </summary>
    private class ValidatingFormatter : DelegatingFormatter
    {
        private readonly Action<CloudEvent>? encodingValidationAction;
        private readonly Action<CloudEvent>? decodingValidationAction;

        internal ValidatingFormatter(CloudEventFormatter formatter, Action<CloudEvent>? encodingValidationAction, Action<CloudEvent>? decodingValidationAction) : base(formatter)
        {
            this.encodingValidationAction = encodingValidationAction;
            this.decodingValidationAction = decodingValidationAction;
        }

        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            var result = base.DecodeBatchModeMessage(body, contentType, extensionAttributes);
            foreach (var evt in result)
            {
                decodingValidationAction?.Invoke(evt);
            }
            return result;
        }

        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            var result = base.DecodeBatchModeMessage(body, contentType, extensionAttributes);
            foreach (var evt in result)
            {
                decodingValidationAction?.Invoke(evt);
            }
            return result;
        }

        public override async Task<IReadOnlyList<CloudEvent>> DecodeBatchModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            var result = await base.DecodeBatchModeMessageAsync(body, contentType, extensionAttributes).ConfigureAwait(false);
            foreach (var evt in result)
            {
                decodingValidationAction?.Invoke(evt);
            }
            return result;
        }

        public override void DecodeBinaryModeEventData(ReadOnlyMemory<byte> body, CloudEvent cloudEvent)
        {
            base.DecodeBinaryModeEventData(body, cloudEvent);
            decodingValidationAction?.Invoke(cloudEvent);
        }

        public override CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            var evt = base.DecodeStructuredModeMessage(body, contentType, extensionAttributes);
            decodingValidationAction?.Invoke(evt);
            return evt;
        }

        public override CloudEvent DecodeStructuredModeMessage(Stream messageBody, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            var evt = base.DecodeStructuredModeMessage(messageBody, contentType, extensionAttributes);
            decodingValidationAction?.Invoke(evt);
            return evt;
        }

        public override async Task<CloudEvent> DecodeStructuredModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            var evt = await base.DecodeStructuredModeMessageAsync(body, contentType, extensionAttributes).ConfigureAwait(false);
            decodingValidationAction?.Invoke(evt);
            return evt;
        }

        public override ReadOnlyMemory<byte> EncodeBatchModeMessage(IEnumerable<CloudEvent> cloudEvents, out ContentType contentType)
        {
            // This approach ensures that we only evaluate the original sequence once, without having to materialize it.
            // It does mean we could end up encoding some before aborting later (and therefore wasting effort) though.
            var validating = cloudEvents.Select(evt => { encodingValidationAction?.Invoke(evt); return evt; });
            return base.EncodeBatchModeMessage(validating, out contentType);
        }

        public override ReadOnlyMemory<byte> EncodeBinaryModeEventData(CloudEvent cloudEvent)
        {
            encodingValidationAction?.Invoke(cloudEvent);
            return base.EncodeBinaryModeEventData(cloudEvent);
        }

        public override ReadOnlyMemory<byte> EncodeStructuredModeMessage(CloudEvent cloudEvent, out ContentType contentType)
        {
            encodingValidationAction?.Invoke(cloudEvent);
            return base.EncodeStructuredModeMessage(cloudEvent, out contentType);
        }
    }

    /// <summary>
    /// Delegating formatter which applies default extension attributes on all decoding method calls.
    /// </summary>
    private class DefaultExtensionsFormatter : DelegatingFormatter
    {
        private readonly IReadOnlyList<CloudEventAttribute> defaultExtensionAttributes;

        internal DefaultExtensionsFormatter(CloudEventFormatter formatter, IEnumerable<CloudEventAttribute> extensionAttributes) : base(formatter)
        {
            defaultExtensionAttributes = Validation.CheckNotNull(extensionAttributes, nameof(extensionAttributes)).ToList().AsReadOnly();
            foreach (var attribute in defaultExtensionAttributes)
            {
                if (!attribute.IsExtension)
                {
                    throw new ArgumentException($"The {nameof(CloudEventAttribute.IsExtension)} must be true for all default extension attributes", nameof(extensionAttributes));
                }
            }
        }

        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            base.DecodeBatchModeMessage(body, contentType, ComputeEffectiveExtensionAttributes(extensionAttributes));

        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            base.DecodeBatchModeMessage(body, contentType, ComputeEffectiveExtensionAttributes(extensionAttributes));

        public override Task<IReadOnlyList<CloudEvent>> DecodeBatchModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            base.DecodeBatchModeMessageAsync(body, contentType, ComputeEffectiveExtensionAttributes(extensionAttributes));

        public override CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            base.DecodeStructuredModeMessage(body, contentType, ComputeEffectiveExtensionAttributes(extensionAttributes));

        public override CloudEvent DecodeStructuredModeMessage(Stream messageBody, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            base.DecodeStructuredModeMessage(messageBody, contentType, ComputeEffectiveExtensionAttributes(extensionAttributes));

        public override Task<CloudEvent> DecodeStructuredModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            base.DecodeStructuredModeMessageAsync(body, contentType, ComputeEffectiveExtensionAttributes(extensionAttributes));

        private IEnumerable<CloudEventAttribute> ComputeEffectiveExtensionAttributes(IEnumerable<CloudEventAttribute>? additionalExtensions)
        {
            if (additionalExtensions is null)
            {
                return defaultExtensionAttributes;
            }
            var result = new List<CloudEventAttribute>();

            foreach (var additional in additionalExtensions)
            {
                var match = defaultExtensionAttributes.FirstOrDefault(def => def.Name == additional.Name);
                if (match is not null)
                {
                    if (match != additional)
                    {
                        // TODO: Improve this message
                        throw new ArgumentException($"The extension attribute {match.Name} is already provided as a default, using a different object");
                    }
                }
                else
                {
                    result.Add(additional);
                }
            }
            result.AddRange(defaultExtensionAttributes);
            return result;
        }
    }
}
