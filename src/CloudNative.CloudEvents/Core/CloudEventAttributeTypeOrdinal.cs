// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Core
{
    /// <summary>
    /// Enum of attribute types, to allow efficient switching over <see cref="CloudEventAttributeType"/>.
    /// Each attribute type has a unique value, returned by <see cref="CloudEventAttributeType.Ordinal"/>.
    /// </summary>
    /// <remarks>
    /// This type is in the "Core" namespace and exposed via CloudEventAttributeTypes as relatively few consumers will need to use it.
    /// </remarks>
    public enum CloudEventAttributeTypeOrdinal
    {
        // Note: changing the values here is a breaking change.

        /// <summary>
        /// Ordinal for <see cref="CloudEventAttributeType.Binary"/>
        /// </summary>
        Binary = 0,
        /// <summary>
        /// Ordinal for <see cref="CloudEventAttributeType.Boolean"/>
        /// </summary>
        Boolean = 1,
        /// <summary>
        /// Ordinal for <see cref="CloudEventAttributeType.Integer"/>
        /// </summary>
        Integer = 2,
        /// <summary>
        /// Ordinal for <see cref="CloudEventAttributeType.String"/>
        /// </summary>
        String = 3,
        /// <summary>
        /// Ordinal for <see cref="CloudEventAttributeType.Uri"/>
        /// </summary>
        Uri = 4,
        /// <summary>
        /// Ordinal for <see cref="CloudEventAttributeType.UriReference"/>
        /// </summary>
        UriReference = 5,
        /// <summary>
        /// Ordinal for <see cref="CloudEventAttributeType.Timestamp"/>
        /// </summary>
        Timestamp = 6
    }
}
