// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Kafka.PartitionKeyAdapters;

/// <summary>
/// Partion Key Adapter that skips handling the key.
/// </summary>
/// <typeparam name="TKey">The type of Kafka Message Key.</typeparam>
public class NullPartitionKeyAdapter<TKey> : IPartitionKeyAdapter<TKey>
{
    /// <inheritdoc/>
    public bool ConvertKeyToPartitionKeyAttributeValue(TKey keyValue, out string? attributeValue)
    {
        attributeValue = null;
        return false;
    }

    /// <inheritdoc/>
    public bool ConvertPartitionKeyAttributeValueToKey(string? attributeValue, out TKey? keyValue)
    {
        keyValue = default;
        return false;
    }
}
