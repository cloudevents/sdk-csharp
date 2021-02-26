// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Core
{
    /// <summary>
    /// Utility methods for working with <see cref="CloudEventAttributeType"/>, in contexts
    /// where the functionality is required by formatter/protocol binding implementations,
    /// but we want to obscure it from other users.
    /// </summary>
    public static class CloudEventAttributeTypes
    {
        /// <summary>
        /// Returns the <see cref="CloudEventAttributeTypeOrdinal"/> associated with <paramref name="type"/>,
        /// for convenient switching over attribute types.
        /// </summary>
        /// <param name="type">The attribute type. Must not be null.</param>
        /// <returns>The ordinal enum value associated with the attribute type.</returns>
        public static CloudEventAttributeTypeOrdinal GetOrdinal(CloudEventAttributeType type)
        {
            Validation.CheckNotNull(type, nameof(type));
            return type.Ordinal;
        }
    }
}
