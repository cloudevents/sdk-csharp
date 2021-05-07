// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents.Extensions
{
    /// <summary>
    /// Support for the <see href="https://github.com/cloudevents/spec/blob/master/extensions/distributed-tracing.md">distributed tracing</see>
    /// CloudEvent extension.
    /// </summary>
    public static class DistributedTracing
    {
        /// <summary>
        /// <see cref="CloudEventAttribute"/> representing the 'traceparent' extension attribute.
        /// </summary>
        public static CloudEventAttribute TraceParentAttribute { get; } =
            CloudEventAttribute.CreateExtension("traceparent", CloudEventAttributeType.String);

        /// <summary>
        /// <see cref="CloudEventAttribute"/> representing the 'tracestate' extension attribute.
        /// </summary>
        public static CloudEventAttribute TraceStateAttribute { get; } =
            CloudEventAttribute.CreateExtension("tracestate", CloudEventAttributeType.String);

        /// <summary>
        /// A read-only sequence of all attributes related to the distributed tracing extension.
        /// </summary>
        public static IEnumerable<CloudEventAttribute> AllAttributes { get; } =
            new[] { TraceParentAttribute, TraceStateAttribute }.ToList().AsReadOnly();

        /// <summary>
        /// Sets the <see cref="TraceParentAttribute"/> on the given <see cref="CloudEvent"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent on which to set the attribute. Must not be null.</param>
        /// <param name="traceParent">The trace parent to set. May be null, in which case the attribute is
        /// removed from <paramref name="cloudEvent"/>.</param>
        /// <returns><paramref name="cloudEvent"/>, for convenient method chaining.</returns>
        public static CloudEvent SetTraceParent(this CloudEvent cloudEvent, string traceParent)
        {
            Validation.CheckNotNull(cloudEvent, nameof(cloudEvent));
            cloudEvent[TraceParentAttribute] = traceParent;
            return cloudEvent;
        }

        /// <summary>
        /// Retrieves the <see cref="TraceParentAttribute"/> from the given <see cref="CloudEvent"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent from which to retrieve the attribute. Must not be null.</param>
        /// <returns>The partition key, or null if <paramref name="cloudEvent"/> does not have a trace parent set.</returns>
        public static string GetTraceParent(this CloudEvent cloudEvent) =>
            (string)Validation.CheckNotNull(cloudEvent, nameof(cloudEvent))[TraceParentAttribute];

        /// <summary>
        /// Sets the <see cref="TraceStateAttribute"/> on the given <see cref="CloudEvent"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent on which to set the attribute. Must not be null.</param>
        /// <param name="traceState">The trace state to set. May be null, in which case the attribute is
        /// removed from <paramref name="cloudEvent"/>.</param>
        /// <returns><paramref name="cloudEvent"/>, for convenient method chaining.</returns>
        public static CloudEvent SetTraceState(this CloudEvent cloudEvent, string traceState)
        {
            Validation.CheckNotNull(cloudEvent, nameof(cloudEvent));
            cloudEvent[TraceStateAttribute] = traceState;
            return cloudEvent;
        }

        /// <summary>
        /// Retrieves the <see cref="TraceStateAttribute"/> from the given <see cref="CloudEvent"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent from which to retrieve the attribute. Must not be null.</param>
        /// <returns>The partition key, or null if <paramref name="cloudEvent"/> does not have a trace state set.</returns>
        public static string GetTraceState(this CloudEvent cloudEvent) =>
            (string)Validation.CheckNotNull(cloudEvent, nameof(cloudEvent))[TraceStateAttribute];
    }
}
