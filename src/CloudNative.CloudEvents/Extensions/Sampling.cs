// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents.Extensions
{
    public static class Sampling
    {
        public static CloudEventAttribute SampledRateAttribute { get; } =
            CloudEventAttribute.CreateExtension("sampledrate", CloudEventAttributeType.Integer, PositiveInteger);

        public static IEnumerable<CloudEventAttribute> AllAttributes { get; } =
            new[] { SampledRateAttribute }.ToList().AsReadOnly();

        public static CloudEvent SetSampledRate(this CloudEvent cloudEvent, int? sampledRate)
        {
            cloudEvent[SampledRateAttribute] = sampledRate;
            return cloudEvent;
        }

        public static int? GetSampledRate(this CloudEvent cloudEvent) =>
            (int?)cloudEvent[SampledRateAttribute];

        private static void PositiveInteger(object value)
        {
            if ((int)value <= 0)
            {
                throw new ArgumentOutOfRangeException("Sampled rate must be positive.");
            }
        }
    }
}
