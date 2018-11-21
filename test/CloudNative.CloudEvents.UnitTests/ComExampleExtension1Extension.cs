namespace CloudNative.CloudEvents.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public class ComExampleExtension1Extension : ICloudEventExtension
    {
        private const string extensionAttribute = "comexampleextension1";
        IDictionary<string, object> attributes = new Dictionary<string, object>();

        public ComExampleExtension1Extension()
        {
        }

        public string ComExampleExtension1
        {
            get => attributes[extensionAttribute] as string;
            set => attributes[extensionAttribute] = value;
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
    }
}