// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Extensions
{
    using System;
    using System.Collections.Generic;

    public class SamplingExtension : ICloudEventExtension
    {
        public const string SampledRateAttributeName = "sampledrate";

        IDictionary<string, object> attributes = new Dictionary<string, object>();

        public SamplingExtension(int sampledRate = 0)
        {
            this.SampledRate = sampledRate;
        }

        public int? SampledRate
        {
            get => (int?)this.attributes[SampledRateAttributeName];
            set => attributes[SampledRateAttributeName] = value;
        }

        public Type GetAttributeType(string name)
        {
            switch (name)
            {
                case SampledRateAttributeName:
                    return typeof(int?);
            }

            return null;
        }

        void ICloudEventExtension.Attach(CloudEvent cloudEvent)
        {
            var eventAttributes = cloudEvent.GetAttributes();
            if (attributes == eventAttributes)
            {
                // already done
                return;
            }

            foreach (var attr in attributes)
            {
                eventAttributes[attr.Key] = attr.Value;
            }

            attributes = eventAttributes;
        }

        bool ICloudEventExtension.ValidateAndNormalize(string key, ref object value)
        {
            switch (key)
            {
                case SampledRateAttributeName:
                    if (value == null)
                    {
                        return true;
                    }
                    else if (value is string)
                    {
                        if (int.TryParse((string)value, out var i))
                        {
                            value = (int?)i;
                            return true;
                        }
                    }
                    else if (value is int)
                    {
                        value = (int?)value;
                        return true;
                    }

                    throw new InvalidOperationException(Strings.ErrorSampledRateValueIsaNotAnInteger);
            }

            return false;
        }
    }
}