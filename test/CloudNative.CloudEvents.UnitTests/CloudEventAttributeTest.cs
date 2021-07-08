// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    public class CloudEventAttributeTest
    {
        [Theory]
        [InlineData("")]
        [InlineData("UPPER")]
        [InlineData("punct-uation")]
        [InlineData("under_scope")]
        public void CreateExtension_InvalidName(string name)
        {
            var exception = Assert.Throws<ArgumentException>(() => CloudEventAttribute.CreateExtension(name, CloudEventAttributeType.String));
            if (!string.IsNullOrEmpty(name))
            {
                Assert.Contains($"'{name}'", exception.Message);
            }
        }

        [Theory]
        [InlineData("simple")]
        [InlineData("longnamethatwouldnotberecommendedbutisstillvalidsoweshouldnotthrow")]
        public void CreateExtension_ValidName(string name)
        {
            var attr = CloudEventAttribute.CreateExtension(name, CloudEventAttributeType.Uri);
            Assert.Equal(name, attr.Name);
        }

        [Fact]
        public void Properties_ExtensionAttribute()
        {
            var attr = CloudEventAttribute.CreateExtension("test", CloudEventAttributeType.Uri);
            Assert.Equal("test", attr.Name);
            Assert.Equal("test", attr.ToString());
            Assert.Equal(CloudEventAttributeType.Uri, attr.Type);
            Assert.False(attr.IsRequired);
            Assert.True(attr.IsExtension);
        }

        [Fact]
        public void Properties_RequiredAttribute()
        {
            var attr = CloudEventsSpecVersion.V1_0.IdAttribute;
            Assert.Equal("id", attr.Name);
            Assert.Equal("id", attr.ToString());
            Assert.Equal(CloudEventAttributeType.String, attr.Type);
            Assert.True(attr.IsRequired);
            Assert.False(attr.IsExtension);
        }

        [Fact]
        public void Properties_OptionalAttribute()
        {
            var attr = CloudEventsSpecVersion.V1_0.TimeAttribute;
            Assert.Equal("time", attr.Name);
            Assert.Equal("time", attr.ToString()); 
            Assert.Equal(CloudEventAttributeType.Timestamp, attr.Type);
            Assert.False(attr.IsRequired);
            Assert.False(attr.IsExtension);
        }

        [Fact]
        public void CreateExtension_NullName() =>
            Assert.Throws<ArgumentNullException>(() => CloudEventAttribute.CreateExtension(null!, CloudEventAttributeType.String));

        [Fact]
        public void CreateExtension_NullType() =>
            Assert.Throws<ArgumentNullException>(() => CloudEventAttribute.CreateExtension("name", null!));

        [Fact]
        public void CreateExtension_SpecVersionName() =>
            Assert.Throws<ArgumentException>(() =>
                CloudEventAttribute.CreateExtension(CloudEventsSpecVersion.SpecVersionAttributeName, CloudEventAttributeType.String));

        [Fact]
        public void Validate_NoValidator_Valid()
        {
            var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer, validator: null);
            attr.Validate(10);
        }

        [Fact]
        public void Validate_NoValidator_InvalidType()
        {
            var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer, validator: null);
            Assert.Throws<ArgumentException>(() => attr.Validate(10L));
        }

        [Fact]
        public void Validate_WithValidator_Valid()
        {
            var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer, ValidateNonNegative);
            attr.Validate(10);

        }

        [Fact]
        public void Validate_WithValidator_InvalidType()
        {
            var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer, ValidateNonNegative);
            Assert.Throws<ArgumentException>(() => attr.Validate(10L));
        }

        [Fact]
        public void Validate_WithValidator_InvalidValue()
        {
            var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer, ValidateNonNegative);
            var exception = Assert.Throws<ArgumentException>(() => attr.Validate(-5));
            Assert.Contains("Custom validation message", exception.Message);
            Assert.IsType<Exception>(exception.InnerException);
            Assert.Equal("Custom validation message", exception.InnerException.Message);
        }

        [Fact]
        public void Parse_Valid()
        {
            var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer, ValidateNonNegative);
            Assert.Equal(10, attr.Parse("10"));
        }

        [Fact]
        public void Parse_Invalid()
        {
            var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer, ValidateNonNegative);
            Assert.Throws<ArgumentException>(() => attr.Parse("-5"));
        }

        [Fact]
        public void Format_Valid()
        {
            var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer, ValidateNonNegative);
            Assert.Equal("10", attr.Format(10));
        }

        [Fact]
        public void Format_Invalid()
        {
            var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer, ValidateNonNegative);
            Assert.Throws<ArgumentException>(() => attr.Format(-5));
        }

        private void ValidateNonNegative(object value)
        {
            if ((int)value < 0)
            {
                throw new Exception("Custom validation message");
            }
        }
    }
}
