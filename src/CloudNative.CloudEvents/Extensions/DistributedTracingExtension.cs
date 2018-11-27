// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.Extensions
{
    using System;
    using System.Collections.Generic;

    public class DistributedTracingExtension : ICloudEventExtension
    {
        public const string TraceParentAttributeName = "traceparent";

        public const string TraceStateAttributeName = "tracestate";

        IDictionary<string, object> attributes = new Dictionary<string, object>();

        public DistributedTracingExtension(string traceParent = null)
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
                if (attr.Value != null)
                {
                    eventAttributes[attr.Key] = attr.Value;
                }
            }    
            attributes = eventAttributes;
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

        public Type GetAttributeType(string name)
        {
            switch (name)
            {
                case TraceParentAttributeName:
                case TraceStateAttributeName:
                    return typeof(string);
            }           
            return null;
        }
    }
}