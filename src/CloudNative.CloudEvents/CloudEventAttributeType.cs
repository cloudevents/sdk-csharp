// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

namespace CloudNative.CloudEvents
{
    // TODO: Expose the generic type? That might avoid boxing, and make various aspects more type-safe at compile time.

    /// <summary>
    /// The type of an event attribute, providing simple formatting and parsing functionality.
    /// </summary>
    public abstract class CloudEventAttributeType
    {
        /// <summary>
        /// A Boolean value of "true" or "false".
        /// </summary>
        public static CloudEventAttributeType Boolean { get; } = new BooleanType();

        /// <summary>
        /// A whole number in the range -2,147,483,648 to +2,147,483,647 inclusive.
        /// </summary>
        public static CloudEventAttributeType Integer { get; } = new IntegerType();

        /// <summary>
        /// A sequence of allowable Unicode characters.
        /// </summary>
        public static CloudEventAttributeType String { get; } = new StringType();

        /// <summary>
        /// A sequence of bytes.
        /// </summary>
        public static CloudEventAttributeType Binary { get; } = new BinaryType();

        /// <summary>
        /// An absolute uniform resource identifier.
        /// </summary>
        public static CloudEventAttributeType Uri { get; } = new UriType("URI", allowRelative: false);

        /// <summary>
        /// A uniform resource identifier reference.
        /// </summary>
        public static CloudEventAttributeType UriReference { get; } = new UriType("URI-Reference", allowRelative: true);

        /// <summary>
        /// A date and time expression using the Gregorian calendar.
        /// </summary>
        public static CloudEventAttributeType Timestamp { get; } = new TimestampType();

        /// <summary>
        /// The name of the type, as it is written in the CloudEvents specification.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// CLR type used to represent a value of this attribute type.
        /// </summary>
        public Type ClrType { get; }

        /// <summary>
        /// Returns the name of the type.
        /// </summary>
        /// <returns>The name of the type.</returns>
        public override string ToString() => Name;

        /// <summary>
        /// Converts the given value to its canonical string representation.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public abstract string Format(object value);

        /// <summary>
        /// Converts the given value from its canonical string representation
        /// into <see cref="ClrType"/>.
        /// </summary>
        public abstract object Parse(string text);

        /// <summary>
        /// Validates that the given value is valid for this type.
        /// </summary>
        /// <param name="value">The value to validate. Must be non-null, and suitable for this attribute type.</param>
        public abstract void Validate(object value);

        private CloudEventAttributeType(string name, Type clrType)
        {
            Name = name;
            ClrType = clrType;
        }

        private abstract class GenericCloudEventsAttributeType<T> : CloudEventAttributeType
        {
            protected GenericCloudEventsAttributeType(string name) : base(name, typeof(T))
            {
            }

            public override sealed object Parse(string value) => ParseImpl(Preconditions.CheckNotNull(value, nameof(value)));

            public override sealed string Format(object value)
            {
                Validate(value);
                // TODO: Avoid the double cast.
                return FormatImpl((T) value);
            }

            public override sealed void Validate(object value)
            {
                Preconditions.CheckNotNull(value, nameof(value));
                if (!ClrType.IsInstanceOfType(value))
                {
                    throw new ArgumentException($"Value of type {value.GetType()} is incompatible with expected type {ClrType}", nameof(value));
                }

                ValidateImpl((T)Preconditions.CheckNotNull(value, nameof(value)));
            }

            protected abstract T ParseImpl(string value);

            protected abstract string FormatImpl(T value);

            // Default is for all values to be valid.
            protected virtual void ValidateImpl(T value) { }
        }

        private class BooleanType : GenericCloudEventsAttributeType<bool>
        {
            public BooleanType() : base("Boolean")
            {
            }

            protected override string FormatImpl(bool value) => value ? "true" : "false";
            protected override bool ParseImpl(string value) =>
                value == "true" ? true
                : value == "false" ? false
                : throw new ArgumentException("Invalid Boolean attribute value");
        }

        private class StringType : GenericCloudEventsAttributeType<string>
        {
            public StringType() : base("String")
            {
            }

            // TODO: Validation
            protected override string FormatImpl(string value) => value;
            protected override string ParseImpl(string value) => value;
        }

        private class TimestampType : GenericCloudEventsAttributeType<DateTimeOffset>
        {
            public TimestampType() : base("Timestamp")
            {
            }

            protected override string FormatImpl(DateTimeOffset value) => Timestamps.Format(value);
            protected override DateTimeOffset ParseImpl(string value) => Timestamps.Parse(value);
        }

        // FIXME: Decide on escaping policies here. Uri will automatically perform escaping for us,
        // but it's not clear what the behavior should be. Should we assert that when parsing, the
        // string is already well-formed, and then use the original string when formatting?
        // That makes sense when we parse a CloudEvent and then reformat it, but if users provide
        // a Uri object to us, they may well want it to be escaped for them.
        // Side-note: Uri.IsWellFormedOriginalString() rejects "#fragment" for some reason, which makes
        // it very hard to really validate.
        // Note that it doesn't look like IsWellFormedOriginalString actually checks whether things 
        // need escaping anyway :(
        private class UriType : GenericCloudEventsAttributeType<Uri>
        {
            private readonly UriKind uriKind;

            public UriType(string name, bool allowRelative) : base(name)
            {
                uriKind = allowRelative ? UriKind.RelativeOrAbsolute : UriKind.Absolute;
            }

            protected override string FormatImpl(Uri value) => value.OriginalString;

            protected override Uri ParseImpl(string value) => new Uri(value, uriKind);

            protected override void ValidateImpl(Uri value)
            {
                if (uriKind == UriKind.Absolute && !value.IsAbsoluteUri)
                {
                    throw new ArgumentException("URI must be absolute.");
                }                
            }
        }

        private class BinaryType : GenericCloudEventsAttributeType<byte[]>
        {
            public BinaryType() : base("Binary")
            {
            }

            protected override string FormatImpl(byte[] value) => Convert.ToBase64String(value);
            protected override byte[] ParseImpl(string value) => Convert.FromBase64String(value);
        }

        private class IntegerType : GenericCloudEventsAttributeType<int>
        {
            public IntegerType() : base("Integer")
            {
            }

            protected override string FormatImpl(int value) => value.ToString(CultureInfo.InvariantCulture);
            protected override int ParseImpl(string value)
            {
                if (value.Length > 0 && value[0] == '+')
                {
                    throw new FormatException("Leading + sign is not permitted");
                }
                return int.Parse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            }
        }
    }
}
