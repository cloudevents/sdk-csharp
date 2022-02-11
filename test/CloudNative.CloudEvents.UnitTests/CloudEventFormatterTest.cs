// Copyright 2022 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    public class CloudEventFormatterTest
    {
        [Fact]
        public void GetOrInferDataContentType_NullCloudEvent()
        {
            var formatter = new ContentTypeInferringFormatter();
            Assert.Throws<ArgumentNullException>(() => formatter.GetOrInferDataContentType(null!));
        }

        [Fact]
        public void GetOrInferDataContentType_NoDataOrDataContentType()
        {
            var formatter = new ContentTypeInferringFormatter();
            var cloudEvent = new CloudEvent();
            Assert.Null(formatter.GetOrInferDataContentType(cloudEvent));
        }

        [Fact]
        public void GetOrInferDataContentType_HasDataContentType()
        {
            var formatter = new ContentTypeInferringFormatter();
            var cloudEvent = new CloudEvent { DataContentType = "test/pass" };
            Assert.Equal(cloudEvent.DataContentType, formatter.GetOrInferDataContentType(cloudEvent));
        }

        [Fact]
        public void GetOrInferDataContentType_HasDataButNoContentType_OverriddenInferDataContentType()
        {
            var formatter = new ContentTypeInferringFormatter();
            var cloudEvent = new CloudEvent { Data = "some-data" };
            Assert.Equal("test/some-data", formatter.GetOrInferDataContentType(cloudEvent));
        }

        [Fact]
        public void GetOrInferDataContentType_DataButNoContentType_DefaultInferDataContentType()
        {
            var formatter = new ThrowingEventFormatter();
            var cloudEvent = new CloudEvent { Data = "some-data" };
            Assert.Null(formatter.GetOrInferDataContentType(cloudEvent));
        }

        private class ContentTypeInferringFormatter : ThrowingEventFormatter
        {
            protected override string? InferDataContentType(object data) => $"test/{data}";
        }

        /// <summary>
        /// Event formatter that overrides every abstract method to throw NotImplementedException.
        /// This can be derived from (and further overridden) to easily test concrete methods
        /// in CloudEventFormatter itself.
        /// </summary>
        private class ThrowingEventFormatter : CloudEventFormatter
        {
            public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
                throw new NotImplementedException();

            public override void DecodeBinaryModeEventData(ReadOnlyMemory<byte> body, CloudEvent cloudEvent) =>
                throw new NotImplementedException();

            public override CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
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
