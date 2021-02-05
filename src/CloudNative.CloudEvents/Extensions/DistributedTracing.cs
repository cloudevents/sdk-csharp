// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents.Extensions
{
    public static class DistributedTracing
    {
        public static CloudEventAttribute TraceParentAttribute { get; } =
            CloudEventAttribute.CreateExtension("traceparent", CloudEventAttributeType.String);

        public static CloudEventAttribute TraceStateAttribute { get; } =
            CloudEventAttribute.CreateExtension("tracestate", CloudEventAttributeType.String);

        public static IEnumerable<CloudEventAttribute> AllAttributes { get; } =
            new[] { TraceParentAttribute, TraceStateAttribute }.ToList().AsReadOnly();

        public static CloudEvent SetTraceParent(this CloudEvent cloudEvent, string traceParent)
        {
            cloudEvent[TraceParentAttribute] = traceParent;
            return cloudEvent;
        }

        public static string GetTraceParent(this CloudEvent cloudEvent) =>
            (string)cloudEvent[TraceParentAttribute];

        public static CloudEvent SetTraceState(this CloudEvent cloudEvent, string traceState)
        {
            cloudEvent[TraceStateAttribute] = traceState;
            return cloudEvent;
        }

        public static string GetTraceState(this CloudEvent cloudEvent) =>
            (string)cloudEvent[TraceStateAttribute];
    }
}
