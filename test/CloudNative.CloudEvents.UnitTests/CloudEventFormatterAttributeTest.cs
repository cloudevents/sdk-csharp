// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Mime;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    public class CloudEventFormatterAttributeTest
    {
        [Fact]
        public void CreateFormatter_NoAttribute() =>
            Assert.Null(CloudEventFormatterAttribute.CreateFormatter(typeof(NoAttribute)));

        [Fact]
        public void CreateFormatter_Valid() =>
            Assert.IsType<SampleCloudEventFormatter>(CloudEventFormatterAttribute.CreateFormatter(typeof(ValidAttribute)));

        [Theory]
        [InlineData(typeof(NonInstantiableAttribute))]
        [InlineData(typeof(NonEventFormatterAttribute))]
        [InlineData(typeof(NullFormatterAttribute))]
        public void CreateFormatter_Invalid(Type targetType) =>
            Assert.Throws<ArgumentException>(() => CloudEventFormatterAttribute.CreateFormatter(targetType));

        public class NoAttribute
        {
        }

        [CloudEventFormatter(typeof(AbstractCloudEventFormatter))]
        public class NonInstantiableAttribute
        {
        }

        [CloudEventFormatter(null)]
        public class NullFormatterAttribute
        {
        }

        [CloudEventFormatter(typeof(object))]
        public class NonEventFormatterAttribute
        {
        }

        [CloudEventFormatter(typeof(SampleCloudEventFormatter))]
        public class ValidAttribute
        {
        }

        public abstract class AbstractCloudEventFormatter : CloudEventFormatter
        {
        }

        public class SampleCloudEventFormatter : CloudEventFormatter
        {
            public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> body, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes) =>
                throw new NotImplementedException();

            public override void DecodeBinaryModeEventData(ReadOnlyMemory<byte> body, CloudEvent cloudEvent) =>
                throw new NotImplementedException();

            public override CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> body, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes) =>
                throw new NotImplementedException();

            public override ReadOnlyMemory<byte> EncodeBatchModeMessage(IEnumerable<CloudEvent> cloudEvents, out ContentType contentType) =>
                throw new NotImplementedException();

            public override ReadOnlyMemory<byte> EncodeBinaryModeEventData(CloudEvent cloudEvent) =>
                throw new NotImplementedException();

            public override ReadOnlyMemory<byte> EncodeStructuredModeMessage(CloudEvent cloudEvent, out ContentType contentType) =>
                throw new NotImplementedException();
        }
    }
}
