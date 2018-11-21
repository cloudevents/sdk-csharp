

using System.Collections.Generic;

namespace CloudNative.CloudEvents.Extensions
{
    using System;
    using System.Collections.Generic;

    public class DistributedTracingExtension : ICloudEventExtension
    {
        IDictionary<string, object> attributes = new Dictionary<string, object>();
        public const string TraceParentAttributeName = "traceparent";
        public const string TraceStateAttributeName = "tracestate";

        public DistributedTracingExtension(string traceParent)
        {
            this.TraceParent = traceParent;
        }

        public string TraceParent
        {
            get => attributes[TraceParentAttributeName] as string;
            set => attributes[TraceParentAttributeName] = value;
        }

        public string TraceState
        {
            get => attributes[TraceStateAttributeName] as string;
            set => attributes[TraceStateAttributeName] = value;
        }

        bool ICloudEventExtension.ValidateAndNormalize(string key, ref object value)
        {
            switch (key)
            {
                case TraceParentAttributeName:
                    if (value is string)
                    {
                        return true;
                    }
                    throw new InvalidOperationException(Strings.ErrorTraceParentValueIsaNotAString);
                case TraceStateAttributeName:
                    if (value == null || value is string)
                    {
                        return true;
                    }          
                    throw new InvalidOperationException(Strings.ErrorTraceParentValueIsaNotAString);
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
