// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Extensions
{
    using System;
    using System.Collections.Generic;

    public class PartitioningExtension : ICloudEventExtension
    {
        public const string PartitioningKeyAttributeName = "partitionkey";

        IDictionary<string, object> _attributes = new Dictionary<string, object>();

        public string PartitioningKeyValue
        {
            get => _attributes[PartitioningKeyAttributeName] as string;
            set => _attributes[PartitioningKeyAttributeName] = value;
        }

        public PartitioningExtension(string partitioningKeyValue = null)
        {
            PartitioningKeyValue = partitioningKeyValue;
        }

        void ICloudEventExtension.Attach(CloudEvent cloudEvent)
        {
            var eventAttributes = cloudEvent.GetAttributes();
            if (_attributes == eventAttributes)
            {
                // already done
                return;
            }

            foreach (var attr in _attributes)
            {
                if (attr.Value != null)
                {
                    eventAttributes[attr.Key] = attr.Value;
                }
            }
            _attributes = eventAttributes;
        }

        bool ICloudEventExtension.ValidateAndNormalize(string key, ref dynamic value)
        {
            if (string.Equals(key, PartitioningKeyAttributeName))
            {
                if (value is string)
                {
                    return true;
                }

                throw new InvalidOperationException(Strings.ErrorPartitioningKeyValueIsaNotAString);
            }

            return false;
        }

        public Type GetAttributeType(string name)
        {
            return string.Equals(name, PartitioningKeyAttributeName) ? typeof(string) : null;
        }
    }
}