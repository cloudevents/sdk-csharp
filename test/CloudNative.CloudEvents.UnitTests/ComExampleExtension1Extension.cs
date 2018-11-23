// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.UnitTests
{
    using System;
    using System.Collections.Generic;

    public class ComExampleExtension1Extension : ICloudEventExtension
    {
        const string extensionAttribute = "comexampleextension1";

        IDictionary<string, object> attributes = new Dictionary<string, object>();

        public ComExampleExtension1Extension()
        {
        }

        public string ComExampleExtension1
        {
            get => attributes[extensionAttribute] as string;
            set => attributes[extensionAttribute] = value;
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
                case extensionAttribute:
                    if (value is string)
                    {
                        return true;
                    }

                    throw new InvalidOperationException("value is missing or not a string");
            }

            return false;
        }

        public Type GetAttributeType(string name)
        {
            switch (name)
            {
                case extensionAttribute:
                    return typeof(string);
            }
            return null;
        }
    }
}