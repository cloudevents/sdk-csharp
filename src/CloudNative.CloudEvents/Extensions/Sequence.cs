// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents.Extensions
{
    /// <summary>
    /// Support for the <see href="https://github.com/cloudevents/spec/tree/main/cloudevents/extensions/sequence.md">sequence</see>
    /// CloudEvent extension.
    /// </summary>
    public static class Sequence
    {
        // TODO: Potentially make it public. But public constants make me nervous.
        private const string IntegerType = "Integer";

        /// <summary>
        /// <see cref="CloudEventAttribute"/> representing the 'sequence' extension attribute.
        /// </summary>
        public static CloudEventAttribute SequenceAttribute { get; } =
            CloudEventAttribute.CreateExtension("sequence", CloudEventAttributeType.String);

        /// <summary>
        /// <see cref="CloudEventAttribute"/> representing the 'sequencetype' extension attribute.
        /// </summary>
        public static CloudEventAttribute SequenceTypeAttribute { get; } =
            CloudEventAttribute.CreateExtension("sequencetype", CloudEventAttributeType.String);

        private static CloudEventAttribute SurrogateIntegerAttribute { get; } =
            CloudEventAttribute.CreateExtension("int", CloudEventAttributeType.Integer);

        /// <summary>
        /// A read-only sequence of all attributes related to the sequence extension.
        /// </summary>
        public static IEnumerable<CloudEventAttribute> AllAttributes { get; } =
            new[] { SequenceAttribute, SequenceTypeAttribute }.ToList().AsReadOnly();

        /// <summary>
        /// Sets both <see cref="SequenceAttribute"/> and <see cref="SequenceTypeAttribute"/> attributes based on the specified value.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent on which to set the attributes. Must not be null.</param>
        /// <param name="value">The sequence value to set. May be null, in which case both attributes are removed from
        /// <paramref name="cloudEvent"/>.</param>
        /// <exception cref="ArgumentException"><paramref name="value"/> is a non-null value for an unsupported sequence type.</exception>
        /// <returns><paramref name="cloudEvent"/>, for convenient method chaining.</returns>
        public static CloudEvent SetSequence(this CloudEvent cloudEvent, object? value)
        {
            Validation.CheckNotNull(cloudEvent, nameof(cloudEvent));
            if (value is null)
            {
                cloudEvent[SequenceAttribute] = null;
                cloudEvent[SequenceTypeAttribute] = null;
            }
            else if (value is int)
            {
                // TODO: Validation? Would be nice to get a sort of "surrogate" attribute here...
                cloudEvent[SequenceAttribute] = SurrogateIntegerAttribute.Format(value);
                cloudEvent[SequenceTypeAttribute] = IntegerType;
            }
            else
            {
                throw new ArgumentException($"No sequence type known for type {value.GetType()}");
            }
            return cloudEvent;
        }

        // TODO: Naming of these extension methods

        /// <summary>
        /// Retrieves the <see cref="SequenceAttribute"/> value from the event, without any
        /// further transformation.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent from which to retrieve the attribute. Must not be null.</param>
        /// <returns>The <see cref="SequenceAttribute"/> value, as a string, or null if the attribute is not set.</returns>
        public static string? GetSequenceString(this CloudEvent cloudEvent) =>
            (string?) Validation.CheckNotNull(cloudEvent, nameof(cloudEvent))[SequenceAttribute];

        /// <summary>
        /// Retrieves the <see cref="SequenceTypeAttribute"/> value from the event, without any
        /// further transformation.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent from which to retrieve the attribute. Must not be null.</param>
        /// <returns>The <see cref="SequenceTypeAttribute"/> value, as a string, or null if the attribute is not set.</returns>
        public static string? GetSequenceType(this CloudEvent cloudEvent) =>
            (string?) Validation.CheckNotNull(cloudEvent, nameof(cloudEvent))[SequenceTypeAttribute];

        /// <summary>
        /// Retrieves the <see cref="SequenceAttribute"/> value from the event,
        /// parsing it according to the value of <see cref="SequenceTypeAttribute"/>.
        /// If no type is present in the event, the string value is
        /// returned without further transformation.
        /// </summary>
        /// <param name="cloudEvent"></param>
        /// <returns>The value of <see cref="SequenceAttribute"/> from <paramref name="cloudEvent"/>, transformed
        /// based on the value of <see cref="SequenceTypeAttribute"/>, or null if the attribute is not set.</returns>
        /// <exception cref="InvalidOperationException">The <see cref="SequenceTypeAttribute"/> is present, but unknown to this library.</exception>
        public static object? GetSequenceValue(this CloudEvent cloudEvent)
        {
            Validation.CheckNotNull(cloudEvent, nameof(cloudEvent));
            var sequence = GetSequenceString(cloudEvent);
            if (sequence is null)
            {
                return null;
            }
            var type = GetSequenceType(cloudEvent);
            if (type == null)
            {
                return sequence;
            }
            if (type == IntegerType)
            {
                return SurrogateIntegerAttribute.Parse(sequence);
            }
            throw new InvalidOperationException($"Unknown sequence type '{type}'");
        }
    }
}
