// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents.Extensions
{
    /// <summary>
    /// Support for the <see href="https://github.com/cloudevents/spec/tree/main/cloudevents/extensions/sampledrate.md">sampling</see>
    /// CloudEvent extension.
    /// </summary>
    public static class Sampling
    {
        /// <summary>
        /// <see cref="CloudEventAttribute"/> representing the 'sampledrate' extension attribute.
        /// </summary>
        public static CloudEventAttribute SampledRateAttribute { get; } =
            CloudEventAttribute.CreateExtension("sampledrate", CloudEventAttributeType.Integer, PositiveInteger);

        /// <summary>
        /// A read-only sequence of all attributes related to the sampling extension.
        /// </summary>
        public static IEnumerable<CloudEventAttribute> AllAttributes { get; } =
            new[] { SampledRateAttribute }.ToList().AsReadOnly();

        /// <summary>
        /// Sets the <see cref="SampledRateAttribute"/> on the given <see cref="CloudEvent"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent on which to set the attribute. Must not be null.</param>
        /// <param name="sampledRate">The sampled rate to set. May be null, in which case the attribute is
        /// removed from <paramref name="cloudEvent"/>. If this value is non-null, it must be positive.</param>
        /// <returns><paramref name="cloudEvent"/>, for convenient method chaining.</returns>
        public static CloudEvent SetSampledRate(this CloudEvent cloudEvent, int? sampledRate)
        {
            Validation.CheckNotNull(cloudEvent, nameof(cloudEvent));
            cloudEvent[SampledRateAttribute] = sampledRate;
            return cloudEvent;
        }

        /// <summary>
        /// Retrieves the <see cref="SampledRateAttribute"/> from the given <see cref="CloudEvent"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent from which to retrieve the attribute. Must not be null.</param>
        /// <returns>The sampled rate, or null if <paramref name="cloudEvent"/> does not have a sampled rate set.</returns>
        public static int? GetSampledRate(this CloudEvent cloudEvent) =>
            (int?) Validation.CheckNotNull(cloudEvent, nameof(cloudEvent))[SampledRateAttribute];

        private static void PositiveInteger(object value)
        {
            if ((int) value <= 0)
            {
                throw new ArgumentOutOfRangeException("Sampled rate must be positive.");
            }
        }
    }
}
