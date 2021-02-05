// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents.Extensions
{
    public static class Partitioning
    {
        public static CloudEventAttribute PartitionKeyAttribute { get; } =
            CloudEventAttribute.CreateExtension("partitionkey", CloudEventAttributeType.String);

        public static IEnumerable<CloudEventAttribute> AllAttributes { get; } =
            new[] { PartitionKeyAttribute }.ToList().AsReadOnly();

        public static CloudEvent SetPartitionKey(this CloudEvent cloudEvent, string partitionKey)
        {
            cloudEvent[PartitionKeyAttribute] = partitionKey;
            return cloudEvent;
        }

        public static string GetPartitionKey(this CloudEvent cloudEvent) =>
            (string)cloudEvent[PartitionKeyAttribute];
    }
}
