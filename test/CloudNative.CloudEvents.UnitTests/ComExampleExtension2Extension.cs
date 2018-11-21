namespace CloudNative.CloudEvents.UnitTests
{
    using System;
    using System.Collections.Generic;

    public class ComExampleExtension2Extension : ICloudEventExtension
    {
        IDictionary<string, object> attributes = new Dictionary<string, object>();

        private const string extensionAttribute = "comexampleextension2";

        public ComExampleExtension2Extension()
        {
        }

        public ComExampleExtension2Data ComExampleExtension2
        {
            get => attributes[extensionAttribute] as ComExampleExtension2Data;
            set => attributes[extensionAttribute] = value;
        }

        bool ICloudEventExtension.ValidateAndNormalize(string key, ref object value)
        {
            switch (key)
            {
                case extensionAttribute:
                    if (value is ComExampleExtension2Data)
                    {
                        return true;
                    }

                    var ext = ((dynamic)value);
                    value = new ComExampleExtension2Data()
                    {
                        OtherValue = (int)ext.othervalue
                    };
                    return true;
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
