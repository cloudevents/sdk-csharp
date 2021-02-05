// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents.Extensions
{
    public static class Sequence
    {
        // TODO: Potentially make it public. But public constants make me nervous.
        private const string IntegerType = "Integer";

        public static CloudEventAttribute SequenceAttribute { get; } =
            CloudEventAttribute.CreateExtension("sequence", CloudEventAttributeType.String);

        public static CloudEventAttribute SequenceTypeAttribute { get; } =
            CloudEventAttribute.CreateExtension("sequencetype", CloudEventAttributeType.String);

        private static CloudEventAttribute SurrogateIntegerAttribute { get; } =
            CloudEventAttribute.CreateExtension("int", CloudEventAttributeType.Integer);

        public static IEnumerable<CloudEventAttribute> AllAttributes { get; } =
            new[] { SequenceAttribute, SequenceTypeAttribute }.ToList().AsReadOnly();

        /// <summary>
        /// Sets both the 'sequence' and 'sequencetype' attributes based on the specified value.
        /// </summary>
        public static CloudEvent SetSequence(this CloudEvent cloudEvent, object value)
        {
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
        /// <param name="cloudEvent"></param>
        /// <returns></returns>
        public static string GetSequenceString(this CloudEvent cloudEvent) =>
            (string) cloudEvent[SequenceAttribute];

        public static string GetSequenceType(this CloudEvent cloudEvent) =>
            (string) cloudEvent[SequenceTypeAttribute];

        /// <summary>
        /// Retrieves the <see cref="SequenceAttribute"/> value from the event,
        /// parsing it according to the value of <see cref="SequenceTypeAttribute"/>.
        /// If no type is present in the event, the string value is
        /// returned without further transformation.
        /// </summary>
        public static object GetSequenceValue(this CloudEvent cloudEvent)
        {
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
