// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;

namespace CloudNative.CloudEvents.Kafka.PartitionKeyAdapters;

/// <summary>
/// Partion Key Adapter that converts to and from Guids in binary representation.
/// </summary>
public class BinaryGuidPartitionKeyAdapter : IPartitionKeyAdapter<byte[]?>
{
    /// <inheritdoc/>
    public bool ConvertKeyToPartitionKeyAttributeValue(byte[]? keyValue, out string? attributeValue)
    {
        if (keyValue == null)
        {
            attributeValue = null;
            return false;
        }

        attributeValue = new Guid(keyValue).ToString();
        return true;
    }

    /// <inheritdoc/>
    public bool ConvertPartitionKeyAttributeValueToKey(string? attributeValue, out byte[]? keyValue)
    {
        if (string.IsNullOrEmpty(attributeValue))
        {
            keyValue = default;
            return false;
        }

        keyValue = Guid.Parse(attributeValue).ToByteArray();
        return true;
    }
}
