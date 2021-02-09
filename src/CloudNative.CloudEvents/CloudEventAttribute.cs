// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;

namespace CloudNative.CloudEvents
{
    // TODO: Allow additional validation, e.g. for extensions and content type?
    // TODO: Add Format/Parse methods that pass through to the type (and do validation when parsing)?

    /// <summary>
    /// An attribute that can be associated with a <see cref="CloudEvent"/>.
    /// This may be a context attribute or an extension attribute.
    /// This class represents the abstract concept of an attribute, rather than
    /// an attribute value.
    /// </summary>
    public class CloudEventAttribute
    {
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

        private Action<object> validator;

        // TODO: Have a "mode" of Required/Optional/Extension?

        private CloudEventAttribute(string name, CloudEventAttributeType type, bool required, bool extension, Action<object> validator) =>
            (Name, Type, IsRequired, IsExtension, this.validator) = (ValidateName(name), Preconditions.CheckNotNull(type, nameof(type)), required, extension, validator);

        internal static CloudEventAttribute CreateRequired(string name, CloudEventAttributeType type, Action<object> validator) =>
            new CloudEventAttribute(name, type, required: true, extension: false, validator: validator);

        internal static CloudEventAttribute CreateOptional(string name, CloudEventAttributeType type, Action<object> validator) =>
            new CloudEventAttribute(name, type, required: false, extension: false, validator: validator);

        public static CloudEventAttribute CreateExtension(string name, CloudEventAttributeType type)
        {
            if (name == CloudEventsSpecVersion.SpecVersionAttributeName)
            {
                throw new ArgumentException($"The attribute name '{name}' is reserved and cannot be used for an extension attribute.");
            }
            return new CloudEventAttribute(name, type, required: false, extension: true, validator: null);
        }

        /// <summary>
        /// Creates an extension attribute with a custom validator.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="validator">Validator to use when parsing or formatting values.
        /// This is only ever called with a non-null value which can be cast to the attribute type's corresponding
        /// CLR type. If the validator throws any exception, it is wrapped in an ArgumentException containing the
        /// attribute details.</param>
        /// <returns></returns>
        public static CloudEventAttribute CreateExtension(string name, CloudEventAttributeType type, Action<object> validator) =>
            new CloudEventAttribute(name, type, required: false, extension: true, validator: validator);

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
        internal static string ValidateName(string name)
        {
            Preconditions.CheckNotNull(name, nameof(name));
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
                    throw new ArgumentException($"Invalid character in attribute name: $'{c}'", nameof(name));
                }
            }
            return name;
        }

        public object Parse(string text)
        {
            Preconditions.CheckNotNull(text, nameof(text));
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
            return Validate(Type.Parse(text));
        }

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
            Preconditions.CheckNotNull(value, nameof(value));
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
    }
}
