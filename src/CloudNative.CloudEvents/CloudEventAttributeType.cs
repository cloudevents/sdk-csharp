// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Globalization;

namespace CloudNative.CloudEvents
{
    // TODO: Expose the generic type? That might avoid boxing, and make various aspects more type-safe at compile time.
    // TODO: Clarify validation requirements. At the moment I suspect we're validating more often than we need to.
    // (This is just a little inefficient.)

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
        public static CloudEventAttributeType Uri { get; } = new UriType();

        /// <summary>
        /// A uniform resource identifier reference.
        /// </summary>
        public static CloudEventAttributeType UriReference { get; } = new UriReferenceType();

        /// <summary>
        /// A date and time expression using the Gregorian calendar.
        /// </summary>
        public static CloudEventAttributeType Timestamp { get; } = new TimestampType();

        /// <summary>
        /// The <see cref="Ordinal"/> value for this type.
        /// </summary>
        internal CloudEventAttributeTypeOrdinal Ordinal { get; }

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

        private CloudEventAttributeType(string name, CloudEventAttributeTypeOrdinal ordinal, Type clrType)
        {
            Name = name;
            Ordinal = ordinal;
            ClrType = clrType;
        }

        private abstract class GenericCloudEventsAttributeType<T> : CloudEventAttributeType
        {
            protected GenericCloudEventsAttributeType(string name, CloudEventAttributeTypeOrdinal ordinal) : base(name, ordinal, typeof(T))
            {
            }

            public override sealed object Parse(string value) => ParseImpl(Validation.CheckNotNull(value, nameof(value)));

            public override sealed string Format(object value)
            {
                Validate(value);
                // TODO: Avoid the double cast.
                return FormatImpl((T) value);
            }

            public override sealed void Validate(object value)
            {
                Validation.CheckNotNull(value, nameof(value));
                if (!ClrType.IsInstanceOfType(value))
                {
                    throw new ArgumentException($"Value of type {value.GetType()} is incompatible with expected type {ClrType}", nameof(value));
                }

                ValidateImpl((T)Validation.CheckNotNull(value, nameof(value)));
            }

            protected abstract T ParseImpl(string value);

            protected abstract string FormatImpl(T value);

            // Default is for all values to be valid.
            protected virtual void ValidateImpl(T value) { }
        }

        private class BooleanType : GenericCloudEventsAttributeType<bool>
        {
            public BooleanType() : base("Boolean", CloudEventAttributeTypeOrdinal.Boolean)
            {
            }

            protected override string FormatImpl(bool value) => value ? "true" : "false";
            protected override bool ParseImpl(string value) =>
#pragma warning disable IDE0075 // Simplify conditional expression (the suggestion really isn't simpler)
                value == "true" ? true
                : value == "false" ? false
                : throw new ArgumentException("Invalid Boolean attribute value");
#pragma warning restore IDE0075 // Simplify conditional expression
        }

        private class StringType : GenericCloudEventsAttributeType<string>
        {
            public StringType() : base("String", CloudEventAttributeTypeOrdinal.String)
            {
            }

            // Note: these methods deliberately don't validate, to avoid repeated validation.
            // The "owning attribute" already validates the value when parsing or formatting.
            protected override string FormatImpl(string value) => value;
            protected override string ParseImpl(string value) => value;

            protected override void ValidateImpl(string value)
            {
                bool lastCharWasHighSurrogate = false;
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    // Directly from the spec
                    if (c <= 0x1f || (c >= 0x7f && c <= 0x9f))
                    {
                        throw new ArgumentException($"Control character U+{(ushort)c:x4} is not permitted in string attributes");
                    }
                    // First two ranges in http://www.unicode.org/faq/private_use.html#noncharacters
                    if (c >= 0xfffe || (c >= 0xfdd0 && c <= 0xfdef))
                    {
                        throw new ArgumentException($"Noncharacter U+{(ushort)c:x4} is not permitted in string attributes");
                    }

                    // Handle surrogate pairs, based on this character and whether the last character was a high surrogate.
                    // Every high surrogate must be followed by a low surrogate, and every low surrogate must be preceded by a high surrogate.
                    // Confusingly, the "high surrogate" region [U+D800, U+DBFF] is lower in value than the "low surrogate" region [U+DC00, U+DFFF].
                    if (char.IsSurrogate(c))
                    {
                        if (char.IsHighSurrogate(c))
                        {
                            if (lastCharWasHighSurrogate)
                            {
                                throw new ArgumentException($"High surrogate character U+{(ushort)value[i - 1]:x4} must be followed by a low surrogate character");
                            }
                            lastCharWasHighSurrogate = true;
                        }
                        else
                        {
                            if (!lastCharWasHighSurrogate)
                            {
                                throw new ArgumentException($"Low surrogate character U+{(ushort)c:x4} must be preceded by a high surrogate character");
                            }
                            // Convert the surrogate pair to validate it's not a non-character.
                            // This is the third rule in http://www.unicode.org/faq/private_use.html#noncharacters
                            int utf32 = char.ConvertToUtf32(value[i - 1], c);
                            var last16Bits = utf32 & 0xffff;
                            if (last16Bits == 0xffff || last16Bits == 0xfffe)
                            {
                                throw new ArgumentException($"Noncharacter U+{utf32:x} is not permitted in string attributes");
                            }
                            lastCharWasHighSurrogate = false;
                        }
                    }
                    else if (lastCharWasHighSurrogate)
                    {
                        throw new ArgumentException($"High surrogate character U+{(ushort)value[i - 1]:x4} must be followed by a low surrogate character");
                    }
                }
                if (lastCharWasHighSurrogate)
                {
                    throw new ArgumentException($"String must not end with high surrogate character U+{(ushort)value[value.Length - 1]:x4}");
                }
            }
        }

        private class TimestampType : GenericCloudEventsAttributeType<DateTimeOffset>
        {
            public TimestampType() : base("Timestamp", CloudEventAttributeTypeOrdinal.Timestamp)
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
        // While URI and URI-Reference could be implemented in the same class, there were sufficient
        // differences to make it not worthwhile.
        private class UriType : GenericCloudEventsAttributeType<Uri>
        {
            public UriType() : base("URI", CloudEventAttributeTypeOrdinal.Uri)
            {
            }

            protected override string FormatImpl(Uri value) => value.OriginalString;

            protected override Uri ParseImpl(string value)
            {
                Uri uri = new Uri(value, UriKind.Absolute);
                // On Linux, URIs starting with '/' are implicitly absolute with a scheme of "file".
                // We don't want that...
                if (value.StartsWith("/"))
                {
                    throw new UriFormatException("Invalid URI: expected an absolute URI");
                }
                return uri;
            }

            protected override void ValidateImpl(Uri value)
            {
                if (!value.IsAbsoluteUri)
                {
                    throw new ArgumentException("URI must be absolute.");
                }
            }
        }

        private class UriReferenceType : GenericCloudEventsAttributeType<Uri>
        {
            public UriReferenceType() : base("URI-Reference", CloudEventAttributeTypeOrdinal.UriReference)
            {
            }

            protected override string FormatImpl(Uri value) => value.OriginalString;

            protected override Uri ParseImpl(string value) => new Uri(value, UriKind.RelativeOrAbsolute);
        }

        private class BinaryType : GenericCloudEventsAttributeType<byte[]>
        {
            public BinaryType() : base("Binary", CloudEventAttributeTypeOrdinal.Binary)
            {
            }

            protected override string FormatImpl(byte[] value) => Convert.ToBase64String(value);
            protected override byte[] ParseImpl(string value) => Convert.FromBase64String(value);
        }

        private class IntegerType : GenericCloudEventsAttributeType<int>
        {
            public IntegerType() : base("Integer", CloudEventAttributeTypeOrdinal.Integer)
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
