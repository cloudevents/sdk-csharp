// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;

namespace CloudNative.CloudEvents
{
    /// <summary>
    /// An attribute that can be associated with a <see cref="CloudEvent"/>.
    /// This may be a context attribute or an extension attribute.
    /// This class represents the abstract concept of an attribute, rather than
    /// an attribute value.
    /// </summary>
    public class CloudEventAttribute
    {
        private static readonly IList<string> ReservedNames = new List<string>
        {
            CloudEventsSpecVersion.SpecVersionAttributeName,
            "data"
        };

        /// <summary>
        /// The type of the attribute. All values provided must be compatible with this.
        /// </summary>
        public CloudEventAttributeType Type { get; }

        /// <summary>
        /// The name of the attribute. Instances of this class associated with different
        /// versions of the specification may use different names for the same concept.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Indicates whether this attribute is a required attribute.
        /// Extension attributes are never required.
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// Indicates whether this attribute is an extension attribute.
        /// </summary>
        public bool IsExtension { get; }

        private readonly Action<object>? validator;

        // TODO: Have a "mode" of Required/Optional/Extension?

        private CloudEventAttribute(string name, CloudEventAttributeType type, bool required, bool extension, Action<object>? validator) =>
            (Name, Type, IsRequired, IsExtension, this.validator) = (ValidateName(name), Validation.CheckNotNull(type, nameof(type)), required, extension, validator);

        internal static CloudEventAttribute CreateRequired(string name, CloudEventAttributeType type, Action<object>? validator) =>
            new CloudEventAttribute(name, type, required: true, extension: false, validator: validator);

        internal static CloudEventAttribute CreateOptional(string name, CloudEventAttributeType type, Action<object>? validator) =>
            new CloudEventAttribute(name, type, required: false, extension: false, validator: validator);

        /// <summary>
        /// Creates an extension attribute with the given name and type.
        /// </summary>
        /// <param name="name">The extension attribute name. Must not be null, and must not be 'specversion'.</param>
        /// <param name="type">The extension attribute type. Must not be null.</param>
        /// <returns>The extension attribute represented as a <see cref="CloudEventAttribute"/>.</returns>
        public static CloudEventAttribute CreateExtension(string name, CloudEventAttributeType type)
        {
            Validation.CheckNotNull(name, nameof(name));
            Validation.CheckNotNull(type, nameof(type));
            if (ReservedNames.Contains(name))
            {
                throw new ArgumentException($"The attribute name '{name}' is reserved and cannot be used for an extension attribute.");
            }
            return new CloudEventAttribute(name, type, required: false, extension: true, validator: null);
        }

        /// <summary>
        /// Creates an extension attribute with a custom validator.
        /// </summary>
        /// <param name="name">The extension attribute name. Must not be null, and must not be 'specversion'.</param>
        /// <param name="type">The extension attribute type. Must not be null.</param>
        /// <param name="validator">Validator to use when parsing or formatting values. May be null.
        /// This delegate is only ever called with a non-null value which can be cast to the attribute type's corresponding
        /// CLR type. If the validator throws any exception, it is wrapped in an ArgumentException containing the
        /// attribute details.</param>
        /// <returns>The extension attribute represented as a <see cref="CloudEventAttribute"/>.</returns>
        public static CloudEventAttribute CreateExtension(string name, CloudEventAttributeType type, Action<object>? validator) =>
            new CloudEventAttribute(name, type, required: false, extension: true, validator: validator);

        /// <summary>
        /// Returns the name of the attribute.
        /// </summary>
        /// <returns>The name of the attribute.</returns>
        public override string ToString() => Name;

        /// <summary>
        /// Validates that the given name is valid for an attribute. It must be non-empty,
        /// and consist entirely of lower-case ASCII letters or digits. While the specification recommends
        /// that attribute names should be at most 20 characters long, this method does not validate that.
        /// </summary>
        /// <param name="name">The name to validate.</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid argument name.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
        /// <returns><paramref name="name"/>, for convenience.</returns>
        private static string ValidateName(string name)
        {
            Validation.CheckNotNull(name, nameof(name));
            if (name.Length == 0)
            {
                throw new ArgumentException("Attribute names must be non-empty", nameof(name));
            }
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool valid = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z');
                if (!valid)
                {
                    throw new ArgumentException($"Invalid character '{c}' in attribute name '{name}'", nameof(name));
                }
            }
            return name;
        }

        /// <summary>
        /// Parses the given string representation of an attribute value into a suitable CLR representation.
        /// </summary>
        /// <param name="text">The text representation to parse. Must not be null, and must be a valid value for this attribute.</param>
        /// <returns>The CLR representation of the given textual value for this attribute.</returns>
        public object Parse(string text)
        {
            Validation.CheckNotNull(text, nameof(text));
            object value;
            // By wrapping every exception here, we always get an
            // ArgumentException (other than the ArgumentNullException above) and have the name in the message.
            try
            {
                value = Type.Parse(text);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Text for attribute '{Name}' is invalid: {e.Message}", nameof(value), e);
            }
            return Validate(value);
        }

        /// <summary>
        /// Formats the given value for this attribute as a string.
        /// </summary>
        /// <param name="value">The value to format. Must not be null, and must be a suitable value for this attribute.</param>
        /// <returns>The string representation of this attribute.</returns>
        public string Format(object value) => Type.Format(Validate(value));

        /// <summary>
        /// Validates that the given value is appropriate for this attribute.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="value"/> is invalid for this attribute.</exception>
        /// <returns>The value, for simple method chaining.</returns>
        public object Validate(object value)
        {
            Validation.CheckNotNull(value, nameof(value));
            // By wrapping every exception, whether from the type or the custom validator, we always get an
            // ArgumentException (other than the ArgumentNullException above) and have the name in the message.
            try
            {
                Type.Validate(value);
                if (validator is object)
                {
                    validator(value);
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Value for attribute '{Name}' is invalid: {e.Message}", nameof(value), e);
            }
            return value;
        }

        /// <summary>
        /// Returns an equality comparer for <see cref="CloudEventAttribute"/> values that just uses the attribute name
        /// for equality comparisons. Validators, types and kinds (optional, required, extension) are not included in the comparison.
        /// </summary>
        public static IEqualityComparer<CloudEventAttribute> NameComparer { get; } = new NameComparerImpl();

        /// <summary>
        /// Returns an equality comparer for <see cref="CloudEventAttribute"/> values that just uses the attribute name
        /// and type for equality comparisons. Validators and kinds (optional, required, extension) are not included in the comparison.
        /// </summary>
        public static IEqualityComparer<CloudEventAttribute> NameTypeComparer { get; } = new NameTypeComparerImpl();

        /// <summary>
        /// Returns an equality comparer for <see cref="CloudEventAttribute"/> values that uses the attribute name,
        /// type and kind (optional, required, extension) for equality comparisons. Validators are not included in the comparison.
        /// </summary>
        public static IEqualityComparer<CloudEventAttribute> NameTypeKindComparer { get; } = new NameTypeKindComparerImpl();

        /// <summary>
        /// Base class for all comparers, just to avoid having to worry about nullity in every implementation.
        /// </summary>
        private abstract class ComparerBase : IEqualityComparer<CloudEventAttribute>
        {
            public bool Equals(CloudEventAttribute x, CloudEventAttribute y) =>
                (x is null && y is null) ||
                (x is not null && y is not null && EqualsImpl(x, y));

            public int GetHashCode(CloudEventAttribute obj)
            {
                Validation.CheckNotNull(obj, nameof(obj));
                return GetHashCodeImpl(obj);
            }

            protected abstract bool EqualsImpl(CloudEventAttribute x, CloudEventAttribute y);
            protected abstract int GetHashCodeImpl(CloudEventAttribute obj);
        }

        private sealed class NameComparerImpl : ComparerBase
        {
            protected override bool EqualsImpl(CloudEventAttribute x, CloudEventAttribute y) => x.Name == y.Name;

            protected override int GetHashCodeImpl(CloudEventAttribute obj) => obj.Name.GetHashCode();
        }

        private sealed class NameTypeComparerImpl : ComparerBase
        {
            protected override bool EqualsImpl(CloudEventAttribute x, CloudEventAttribute y) =>
                x.Name == y.Name &&
                x.Type == y.Type;

            protected override int GetHashCodeImpl(CloudEventAttribute obj)
            {
#if NETSTANDARD2_1_OR_GREATER
                return HashCode.Combine(obj.Name, obj.Type);
#else
                unchecked
                {
                    int hash = 19;
                    hash = hash * 31 + obj.Name.GetHashCode();
                    hash = hash * 31 + obj.Type.GetHashCode();
                    return hash;
                }
#endif
            }
        }

        private sealed class NameTypeKindComparerImpl : ComparerBase
        {
            protected override bool EqualsImpl(CloudEventAttribute x, CloudEventAttribute y) =>
                x.Name == y.Name &&
                x.Type == y.Type &&
                x.IsExtension == y.IsExtension &&
                x.IsRequired == y.IsRequired;

            protected override int GetHashCodeImpl(CloudEventAttribute obj)
            {
#if NETSTANDARD2_1_OR_GREATER
                return HashCode.Combine(obj.Name, obj.Type, obj.IsExtension, obj.IsRequired);
#else
                unchecked
                {
                    int hash = 19;
                    hash = hash * 31 + obj.Name.GetHashCode();
                    hash = hash * 31 + obj.Type.GetHashCode();
                    hash = hash * 31 + obj.IsExtension.GetHashCode();
                    hash = hash * 31 + obj.IsRequired.GetHashCode();
                    return hash;
                }
#endif
            }
        }
    }
}
