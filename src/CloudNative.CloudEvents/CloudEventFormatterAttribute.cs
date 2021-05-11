// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Reflection;

namespace CloudNative.CloudEvents
{
    /// <summary>
    /// Indicates the <see cref="CloudEventFormatter"/> type for the "target" type on which this attribute is placed.
    /// The formatter type is expected to be a concrete type derived from <see cref="CloudEventFormatter"/>,
    /// and must have a public parameterless constructor. It should ensure that any decoded CloudEvents
    /// populate the <see cref="CloudEvent.Data"/> property with an instance of the target type (or leave it
    /// as null if the CloudEvent has no data).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
    public sealed class CloudEventFormatterAttribute : Attribute
    {
        /// <summary>
        /// The type to use for CloudEvent formatting. Must not be null.
        /// </summary>
        public Type FormatterType { get; }

        /// <summary>
        /// Constructs an instance of the attribute for the specified formatter type.
        /// </summary>
        /// <param name="formatterType">The type performing the data conversions.</param>
        public CloudEventFormatterAttribute(Type formatterType) =>
            FormatterType = formatterType;

        /// <summary>
        /// Creates a <see cref="CloudEventFormatter"/> based on <see cref="FormatterType"/> if
        /// the specified target type (or an ancestor) has the attribute applied to it. This method does not
        /// perform any caching; callers may wish to cache the results themselves.
        /// </summary>
        /// <param name="targetType">The type for which to create a formatter if possible.</param>
        /// <exception cref="InvalidOperationException">The target type is decorated with this attribute, but the
        /// type cannot be instantiated or does not derive from <see cref="CloudEventFormatter"/>.</exception>
        /// <returns>A new instance of the specified formatter, or null if the type is not decorated with this attribute.</returns>
        public static CloudEventFormatter CreateFormatter(Type targetType)
        {
            var attribute = targetType.GetCustomAttribute<CloudEventFormatterAttribute>(inherit: true);
            if (attribute is null)
            {
                return null;
            }

            // It's fine for the converter creation to fail: we'll try it on every attempt,
            // and always end up with an exception.
            var formatterType = attribute.FormatterType;
            if (formatterType is null)
            {
                throw new ArgumentException($"The {nameof(CloudEventFormatterAttribute)} on type {targetType} has no converter type specified.", nameof(targetType));
            }

            object instance;
            try
            {
                instance = Activator.CreateInstance(formatterType);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Unable to create CloudEvent formatter for target type {targetType}", nameof(targetType), e);
            }

            var formatter = instance as CloudEventFormatter;
            if (formatter is null)
            {
                throw new ArgumentException($"CloudEventFormatter type {formatterType} does not derive from {nameof(CloudEventFormatter)}.", nameof(targetType));
            }

            return formatter;
    }
    }
}