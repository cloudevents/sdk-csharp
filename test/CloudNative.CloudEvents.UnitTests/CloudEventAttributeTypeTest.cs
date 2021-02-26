// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    public class CloudEventAttributeTypeTest
    {
        public static readonly TheoryData<CloudEventAttributeType> AllTypes = new TheoryData<CloudEventAttributeType>
        {
            CloudEventAttributeType.Binary,
            CloudEventAttributeType.Boolean,
            CloudEventAttributeType.Integer,
            CloudEventAttributeType.String,
            CloudEventAttributeType.Timestamp,
            CloudEventAttributeType.Uri,
            CloudEventAttributeType.UriReference
        };

        [Fact]
        public void Names()
        {
            Assert.Equal("Binary", CloudEventAttributeType.Binary.Name);
            Assert.Equal("Boolean", CloudEventAttributeType.Boolean.Name);
            Assert.Equal("Integer", CloudEventAttributeType.Integer.Name);
            Assert.Equal("String", CloudEventAttributeType.String.Name);
            Assert.Equal("Timestamp", CloudEventAttributeType.Timestamp.Name);
            Assert.Equal("URI", CloudEventAttributeType.Uri.Name);
            Assert.Equal("URI-Reference", CloudEventAttributeType.UriReference.Name);
        }

        [Fact]
        public void OrdinalTypeNameMatchesPropertyName()
        {
            var properties = typeof(CloudEventAttributeType)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(prop => prop.PropertyType == typeof(CloudEventAttributeType));
            foreach (var property in properties)
            {
                var type = (CloudEventAttributeType) property.GetValue(null);
                Assert.Equal(property.Name, type.Ordinal.ToString());
            }
        }

        [Theory]
        [MemberData(nameof(AllTypes))]
        public void ParseNull(CloudEventAttributeType type) =>
            Assert.Throws<ArgumentNullException>(() => type.Parse(null));

        [Theory]
        [MemberData(nameof(AllTypes))]
        public void FormatNull(CloudEventAttributeType type) =>
            Assert.Throws<ArgumentNullException>(() => type.Format(null));

        // None of our types can be constructed with a StringBuilder.
        [Theory]
        [MemberData(nameof(AllTypes))]
        public void FormatIncorrectType(CloudEventAttributeType type) =>
            Assert.Throws<ArgumentException>(() => type.Format(new StringBuilder()));

        [Theory]
        [MemberData(nameof(AllTypes))]
        public void ValidateIncorrectType(CloudEventAttributeType type) =>
            Assert.Throws<ArgumentException>(() => type.Validate(new StringBuilder()));

        public class BinaryTypeTest
        {
            [Theory]
            [InlineData("")]
            // Examples from https://en.wikipedia.org/wiki/Base64
            [InlineData("TWFu", (byte) 77, (byte)97, (byte) 110)]
            [InlineData("TWE=", (byte)77, (byte)97)]
            [InlineData("TQ==", (byte)77)]
            public void ParseAndFormat_Valid(string text, params byte[] bytes)
            {
                var parsedBytes = CloudEventAttributeType.Binary.Parse(text);
                // Convert both to hex to provide simpler comparisons.
                Assert.Equal(Convert.ToString(bytes), Convert.ToString(parsedBytes));

                var formattedBytes = CloudEventAttributeType.Binary.Format(bytes);
                Assert.Equal(text, formattedBytes);
            }

            [Theory]
            [InlineData("x")]
            [InlineData("TWFU=")]
            [InlineData("==TQ")]
            public void Parse_Invalid(string text)
            {
                Assert.Throws<FormatException>(() => CloudEventAttributeType.Binary.Parse(text));
            }
        }

        public class BooleanTypeTest
        {
            [Theory]
            [InlineData("false", false)]
            [InlineData("true", true)]
            public void ParseAndFormat_Valid(string text, bool value)
            {
                var parsedValue = (bool) CloudEventAttributeType.Boolean.Parse(text);
                // Convert both to hex to provide simpler comparisons.
                Assert.Equal(value, parsedValue);

                var formattedValue = CloudEventAttributeType.Boolean.Format(value);
                Assert.Equal(text, formattedValue);
            }

            [Theory]
            [InlineData("")]
            [InlineData("TRUE")]
            [InlineData("FALSE")]
            [InlineData("maybe")]
            public void Parse_Invalid(string text)
            {
                Assert.Throws<ArgumentException>(() => CloudEventAttributeType.Boolean.Parse(text));
            }
        }

        public class IntegerTypeTest
        {
            [Theory]
            [InlineData("-2147483648", -2147483648)]
            [InlineData("-1", -1)]
            [InlineData("0", 0)]
            [InlineData("1", 1)]
            [InlineData("2147483647", 2147483647)]
            public void ParseAndFormat_Valid(string text, int value)
            {
                var parsedValue = (int) CloudEventAttributeType.Integer.Parse(text);
                // Convert both to hex to provide simpler comparisons.
                Assert.Equal(value, parsedValue);

                var formattedValue = CloudEventAttributeType.Integer.Format(value);
                Assert.Equal(text, formattedValue);
            }

            [Theory]
            [InlineData("")]
            [InlineData("2147483648")] // Above int.MaxValue
            [InlineData("-2147483649")] // Below int.MinValue
            [InlineData("not an integer")]
            [InlineData("1,000")]
            [InlineData("1.5")]
            [InlineData(" 10")] // Leading space
            [InlineData("+10")] // Plus sign
            [InlineData("10 ")] // Trailing space
            public void Parse_Invalid(string text)
            {
                // Sometimes OverflowException, sometimes FormatException.
                Assert.ThrowsAny<Exception>(() => CloudEventAttributeType.Integer.Parse(text));
            }
        }

        public class StringTypeTest
        {
            [Theory]
            [InlineData("")]
            [InlineData("test")]
            [InlineData("TEST")]
            [InlineData("\U0001F600")]
            [InlineData("x\U0001F600y")]
            [InlineData("x\U0001F600y\U0001F600z")]
            public void ParseAndFormat_Valid(string text)
            {
                var parseResult = (string) CloudEventAttributeType.String.Parse(text);
                Assert.Equal(parseResult, text);
                var formatResult = CloudEventAttributeType.String.Format(text);
                Assert.Equal(text, formatResult);
                CloudEventAttributeType.String.Validate(text);
            }

            [Theory]
            [InlineData("\n")] // Control character (first range)
            [InlineData("\u007f")] // Control character (second range)
            [InlineData("\ufdd0")] // Non-character (first range)
            [InlineData("\ufffe")] // Non-character (second range)
            [InlineData("\U0001FFFE")] // Non-character (surrogate range)
            [InlineData("\U0010FFFE")] // Non-character (surrogate range)
            public void InvalidCharacters(string text)
            {
                Assert.Throws<ArgumentException>(() => CloudEventAttributeType.String.Validate(text));
            }

            // Note: these are specified separately as .NET string attributes are stored as UTF-8
            // internally, which means you can't express invalid strings in attributes (and get them
            // out again).
            [Theory]
            [InlineData(0xd800, 0x20)] // High surrogate followed by non-surrogate
            [InlineData(0xdc00, 0x20)] // Low surrogate at start of string
            [InlineData(0x20, 0xdc00)] // Non-surrogate followed by low surrogate
            [InlineData(0xd800, 0xd800)] // High surrogate followed by high surrogate
            [InlineData(0x20, 0xd800)] // High surrogate at end of string
            public void InvalidSurrogates(int first, int second)
            {
                string text = $"{(char)first}{(char)second}";
                Assert.Throws<ArgumentException>(() => CloudEventAttributeType.String.Validate(text));
            }
        }

        public class TimestampTest
        {
            // Note: this is not particularly exhaustive, as we have a more comprehensive set of tests
            // in TimestampsTest.

            [Theory]
            [InlineData("2021-01-18T14:52:01Z")]
            [InlineData("2000-02-29T01:23:45.678+01:30")]
            [InlineData("2000-02-29T01:23:45-01:30")]
            public void ParseAndFormat_Valid(string text)
            {
                var parsed = CloudEventAttributeType.Timestamp.Parse(text);
                var formatted = CloudEventAttributeType.Timestamp.Format(parsed);
                Assert.Equal(text, formatted);
            }

            [Theory]
            [InlineData("2021-01-18T14:52:01")] // No UTC offset indicator
            [InlineData("2021-01-18T14:52:01.1234567XYZ")] // Garbage after 7 significant digits of sub-second
            [InlineData("20garbage2T14:52:01Z01")] // Text after UTC offset indicator
            [InlineData("2021-01-18T14:52:01X")] // Garbage UTC offset indicator
            public void Parse_Invalid(string text)
            {
                Assert.Throws<FormatException>(() => CloudEventAttributeType.Timestamp.Parse(text));
            }
        }

        public class UriTest
        {
            [Theory]
            [InlineData("https://cloudevents.io/")]
            [InlineData("https://cloudevents.io/path?query=value")]
            [InlineData("ftp://cloudevents.io/path")]
            [InlineData("ftp://cloudevents.io/path#fragment")]
            // It's unclear why System.Uri thinks this doesn't need escaping, but apparently
            // it doesn't (so we now just return the original string in all cases)
            [InlineData("http://host/\u00a3")]
            // These would not round-trip if we just called ToString.
            [InlineData("https://cloudevents.io")]
            [InlineData("https://cloudevents.io?query=value")]
            public void ParseAndFormat_Roundtrip(string text)
            {
                var parsed = CloudEventAttributeType.Uri.Parse(text);
                CloudEventAttributeType.Uri.Validate(parsed);
                Assert.Equal(new Uri(text), parsed);
                var formatted = CloudEventAttributeType.Uri.Format(parsed);
                Assert.Equal(text, formatted);
            }

            [Theory]
            [InlineData("//cloudevents.io?query=value")]
            [InlineData("/path-absolute")]
            [InlineData("")]
            public void Parse_Invalid(string text)
            {
                Assert.Throws<UriFormatException>(() => CloudEventAttributeType.Uri.Parse(text));
            }

            [Fact]
            public void Validate_Invalid()
            {
                var uri = new Uri("//relative", UriKind.Relative);
                Assert.Throws<ArgumentException>(() => CloudEventAttributeType.Uri.Validate(uri));
            }
        }

        public class UriReferenceTest
        {
            [Theory]
            [InlineData("https://cloudevents.io/?query=value")]
            [InlineData("ftp://cloudevents.io/path")]
            [InlineData("")] // Empty strings are valid URI references
            [InlineData("//authority/path")]
            [InlineData("/path-absolute")]
            [InlineData("path-noscheme")]
            [InlineData("#fragment")]
            // These three really shouldn't round-trip. They're not valid URIs as they are,
            // but it's hard to prevent that without also rejecting "#fragment", due to Uri's
            // behavior. I'd expect the Uri constructor to automatically escape the leading
            // character, but apparently it doesn't.
            [InlineData(":colon-start")]
            [InlineData("[open-bracket-start")]
            [InlineData("]close-bracket-start")]
            public void ParseFormatValidate_Valid(string text)
            {
                var parsed = CloudEventAttributeType.UriReference.Parse(text);
                CloudEventAttributeType.UriReference.Validate(parsed);
                Assert.Equal(new Uri(text, UriKind.RelativeOrAbsolute), parsed);
                var formatted = CloudEventAttributeType.UriReference.Format(parsed);
                Assert.Equal(text, formatted);
            }
        }
    }
}
