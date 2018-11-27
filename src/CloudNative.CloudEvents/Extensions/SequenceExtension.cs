// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Extensions
{
    using System;
    using System.Collections.Generic;

    public class SequenceExtension : ICloudEventExtension
    {
        public const string SequenceAttributeName = "sequence";

        public const string SequenceTypeAttributeName = "sequencetype";

        IDictionary<string, object> attributes = new Dictionary<string, object>();

        public SequenceExtension()
        {
        }

        public string Sequence
        {
            get => attributes[SequenceAttributeName] as string;
            set => attributes[SequenceAttributeName] = value;
        }

        public string SequenceType
        {
            get => attributes[SequenceTypeAttributeName] as string;
            set => attributes[SequenceTypeAttributeName] = value;
        }

        public Type GetAttributeType(string name)
        {
            switch (name)
            {
                case SequenceAttributeName:
                case SequenceTypeAttributeName:
                    return typeof(string);
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
                case SequenceAttributeName:
                    if (value == null || value is string)
                    {
                        return true;
                    }

                    throw new InvalidOperationException(Strings.ErrorSequenceValueIsaNotAString);
                case SequenceTypeAttributeName:
                    if (value == null || value is string)
                    {
                        return true;
                    }

                    throw new InvalidOperationException(Strings.ErrorSequenceTypeValueIsaNotAString);
            }

            return false;
        }
    }
}