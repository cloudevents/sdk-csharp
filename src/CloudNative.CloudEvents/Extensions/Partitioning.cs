// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents.Extensions
{
    /// <summary>
    /// Support for the <see href="https://github.com/cloudevents/spec/blob/master/extensions/partitioning.md">partitioning</see>
    /// CloudEvent extension.
    /// </summary>
    public static class Partitioning
    {
        /// <summary>
        /// <see cref="CloudEventAttribute"/> representing the 'partitionkey' extension attribute.
        /// </summary>
        public static CloudEventAttribute PartitionKeyAttribute { get; } =
            CloudEventAttribute.CreateExtension("partitionkey", CloudEventAttributeType.String);

        /// <summary>
        /// A read-only sequence of all attributes related to the partitioning extension.
        /// </summary>
        public static IEnumerable<CloudEventAttribute> AllAttributes { get; } =
            new[] { PartitionKeyAttribute }.ToList().AsReadOnly();

        /// <summary>
        /// Sets the <see cref="PartitionKeyAttribute"/> on the given <see cref="CloudEvent"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent on which to set the attribute. Must not be null.</param>
        /// <param name="partitionKey">The partition key to set. May be null, in which case the attribute is
        /// removed from <paramref name="cloudEvent"/>.</param>
        /// <returns><paramref name="cloudEvent"/>, for convenient method chaining.</returns>
        public static CloudEvent SetPartitionKey(this CloudEvent cloudEvent, string? partitionKey)
        {
            Validation.CheckNotNull(cloudEvent, nameof(cloudEvent));
            cloudEvent[PartitionKeyAttribute] = partitionKey;
            return cloudEvent;
        }

        /// <summary>
        /// Retrieves the <see cref="PartitionKeyAttribute"/> from the given <see cref="CloudEvent"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent from which to retrieve the attribute. Must not be null.</param>
        /// <returns>The partition key, or null if <paramref name="cloudEvent"/> does not have a partition key set.</returns>
        public static string? GetPartitionKey(this CloudEvent cloudEvent) =>
            (string?) Validation.CheckNotNull(cloudEvent, nameof(cloudEvent))[PartitionKeyAttribute];
    }
}
