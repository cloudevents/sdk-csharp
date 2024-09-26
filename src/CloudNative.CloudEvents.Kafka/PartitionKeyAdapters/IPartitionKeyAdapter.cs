// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Kafka.PartitionKeyAdapters;

/// <summary>
/// Defines the methods of the adapters responsible for transforming from cloud event
/// PartitionKey Attribute to Kafka Message Key.
/// </summary>
/// <typeparam name="TKey">The type of Kafka Message Key.</typeparam>
public interface IPartitionKeyAdapter<TKey>
{
    /// <summary>
    /// Converts a Message Key to PartionKey Attribute Value.
    /// </summary>
    /// <param name="keyValue">The key value to transform.</param>
    /// <param name="attributeValue">The transformed attribute value (output).</param>
    /// <returns>Whether the attribute should be set.</returns>
    bool ConvertKeyToPartitionKeyAttributeValue(TKey keyValue, out string? attributeValue);

    /// <summary>
    /// Converts a PartitionKey Attribute value to a Message Key.
    /// </summary>
    /// <param name="attributeValue">The attribute value to transform.</param>
    /// <param name="keyValue">The transformed key value (output)</param>
    /// <returns>Whether the key should be set.</returns>
    bool ConvertPartitionKeyAttributeValueToKey(string? attributeValue, out TKey? keyValue);
}
