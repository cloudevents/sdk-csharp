// Copyright 2025 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents;

// TODO:
// - Naming:
//   - Is the namespace appropriate? We have very few types in this root namespace
//   - Delegating or Decorating?
//   - Fill in the "CloudEvent" part, e.g. DelegatingCloudEventFormatter?
// - Expose the original formatter, perhaps in a protected way?
// - All documentation
// - We can't call original.InferContentType, because it's protected. Is that an issue? (Users can always override GetOrInferContentType instead.)
// - Are we happy with this being public? With the extension methods being public, this class doesn't have to be.
//   - Are we happy with the extension methods being extension methods?

/// <summary>
/// 
/// </summary>
public abstract class DelegatingFormatter : CloudEventFormatter
{
    /// <summary>
    /// The formatter to delegate to.
    /// </summary>
    private readonly CloudEventFormatter original;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="original"></param>
    public DelegatingFormatter(CloudEventFormatter original)
    {
        this.original = Validation.CheckNotNull(original, nameof(original));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="body"></param>
    /// <param name="contentType"></param>
    /// <param name="extensionAttributes"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
        original.DecodeStructuredModeMessage(body, contentType, extensionAttributes);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="messageBody"></param>
    /// <param name="contentType"></param>
    /// <param name="extensionAttributes"></param>
    /// <returns></returns>
    public override CloudEvent DecodeStructuredModeMessage(Stream messageBody, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
        original.DecodeStructuredModeMessage(messageBody, contentType, extensionAttributes);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="body"></param>
    /// <param name="contentType"></param>
    /// <param name="extensionAttributes"></param>
    /// <returns></returns>
    public override Task<CloudEvent> DecodeStructuredModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
        original.DecodeStructuredModeMessageAsync(body, contentType, extensionAttributes);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cloudEvent"></param>
    /// <param name="contentType"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override ReadOnlyMemory<byte> EncodeStructuredModeMessage(CloudEvent cloudEvent, out ContentType contentType) =>
        original.EncodeStructuredModeMessage(cloudEvent, out contentType);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="body"></param>
    /// <param name="cloudEvent"></param>
    /// <exception cref="NotImplementedException"></exception>
    public override void DecodeBinaryModeEventData(ReadOnlyMemory<byte> body, CloudEvent cloudEvent) =>
        original.DecodeBinaryModeEventData(body, cloudEvent);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cloudEvent"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override ReadOnlyMemory<byte> EncodeBinaryModeEventData(CloudEvent cloudEvent) =>
        original.EncodeBinaryModeEventData(cloudEvent);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="body"></param>
    /// <param name="contentType"></param>
    /// <param name="extensionAttributes"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
        original.DecodeBatchModeMessage(body, contentType, extensionAttributes);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="body"></param>
    /// <param name="contentType"></param>
    /// <param name="extensionAttributes"></param>
    /// <returns></returns>
    public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
        original.DecodeBatchModeMessage(body, contentType, extensionAttributes);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="body"></param>
    /// <param name="contentType"></param>
    /// <param name="extensionAttributes"></param>
    /// <returns></returns>
    public override Task<IReadOnlyList<CloudEvent>> DecodeBatchModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
        original.DecodeBatchModeMessageAsync(body, contentType, extensionAttributes);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cloudEvents"></param>
    /// <param name="contentType"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override ReadOnlyMemory<byte> EncodeBatchModeMessage(IEnumerable<CloudEvent> cloudEvents, out ContentType contentType) =>
        original.EncodeBatchModeMessage(cloudEvents, out contentType);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cloudEvent"></param>
    /// <returns></returns>
    public override string? GetOrInferDataContentType(CloudEvent cloudEvent) =>
        original.GetOrInferDataContentType(cloudEvent);
}
