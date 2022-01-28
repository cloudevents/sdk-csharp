// Copyright 2022 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using System;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;
using static CloudNative.CloudEvents.V1.CloudEvent.Types;

namespace CloudNative.CloudEvents.Protobuf.UnitTests
{
    /// <summary>
    /// Tests for ProtobufEventFormatter. Note that most tests for encoding/decoding of
    /// structured mode events (and batches) are performed via the public methods that
    /// perform proto/CloudEvent conversions - the regular event formatter methods are
    /// only wrappers around those, covered by minimal tests here.
    /// </summary>
    public class ProtobufEventFormatterTest
    {
        private static readonly ContentType s_protobufCloudEventContentType = new ContentType("application/cloudevents+protobuf");
        private static readonly ContentType s_protobufCloudEventBatchContentType = new ContentType("application/cloudevents-batch+protobuf");

        [Fact]
        public void EncodeStructuredModeMessage_Minimal()
        {
            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Id = "event-id",
                Source = new Uri("https://event-source"),
                Type = "event-type",
            };

            var encoded = new ProtobufEventFormatter().EncodeStructuredModeMessage(cloudEvent, out var contentType);
            Assert.Equal("application/cloudevents+protobuf; charset=utf-8", contentType.ToString());
            var actualProto = V1.CloudEvent.Parser.ParseFrom(encoded.ToArray());

            var expectedProto = new V1.CloudEvent
            {
                SpecVersion = "1.0",
                Id = "event-id",
                Source = "https://event-source",
                Type = "event-type"
            };
            Assert.Equal(expectedProto, actualProto);
        }

        /// <summary>
        /// A simple test that populates all known v1.0 attributes, so we don't need to test that
        /// aspect in the future.
        /// </summary>
        [Fact]
        public void ConvertToProto_V1Attributes()
        {
            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Data = "text",
                DataContentType = "text/plain",
                DataSchema = new Uri("https://data-schema"),
                Id = "event-id",
                Source = new Uri("https://event-source"),
                Subject = "event-subject",
                Time = new DateTimeOffset(2021, 2, 19, 12, 34, 56, 789, TimeSpan.FromHours(1)),
                Type = "event-type"
            };

            var actualProto = new ProtobufEventFormatter().ConvertToProto(cloudEvent);
            var expectedProto = new V1.CloudEvent
            {
                SpecVersion = "1.0",
                Id = "event-id",
                Source = "https://event-source",
                Type = "event-type",
                Attributes =
                {
                    { "datacontenttype", StringAttribute("text/plain") },
                    { "dataschema", UriAttribute("https://data-schema") },
                    { "subject", StringAttribute("event-subject") },
                    // Deliberately not reusing cloudEvent.Time: this demonstrates that only the instant in time
                    // is relevant, not the UTC offset.
                    { "time", TimestampAttribute(new DateTimeOffset(2021, 2, 19, 11, 34, 56, 789, TimeSpan.Zero)) }
                },
                TextData = "text"
            };
            Assert.Equal(expectedProto, actualProto);
        }

        [Fact]
        public void ConvertToProto_AllAttributeTypes()
        {
            var cloudEvent = new CloudEvent(AllTypesExtensions)
            {
                ["binary"] = SampleBinaryData,
                ["boolean"] = true,
                ["integer"] = 10,
                ["string"] = "text",
                ["timestamp"] = SampleTimestamp,
                ["uri"] = SampleUri,
                ["urireference"] = SampleUriReference
            };
            // We're not going to check these.
            cloudEvent.PopulateRequiredAttributes();

            var proto = new ProtobufEventFormatter().ConvertToProto(cloudEvent);

            var expectedAttributes = new MapField<string, CloudEventAttributeValue>
            {
                { "binary", BinaryAttribute(SampleBinaryData) },
                { "boolean", BooleanAttribute(true) },
                { "integer", IntegerAttribute(10) },
                { "string", StringAttribute("text") },
                { "timestamp", TimestampAttribute(SampleTimestamp) },
                { "uri", UriAttribute(SampleUriText) },
                { "urireference", UriRefAttribute(SampleUriReferenceText) }
            };
            Assert.Equal(proto.Attributes, expectedAttributes);
        }

        [Fact]
        public void ConvertToProto_NoData()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var proto = new ProtobufEventFormatter().ConvertToProto(cloudEvent);
            Assert.Equal(V1.CloudEvent.DataOneofCase.None, proto.DataCase);
        }

        [Fact]
        public void ConvertToProto_TextData()
        {
            var cloudEvent = new CloudEvent { Data = "text" }.PopulateRequiredAttributes();
            var proto = new ProtobufEventFormatter().ConvertToProto(cloudEvent);
            Assert.Equal("text", proto.TextData);
        }

        [Fact]
        public void ConvertToProto_BinaryData()
        {
            var cloudEvent = new CloudEvent { Data = SampleBinaryData }.PopulateRequiredAttributes();
            var proto = new ProtobufEventFormatter().ConvertToProto(cloudEvent);
            Assert.Equal(SampleBinaryData, proto.BinaryData.ToByteArray());
        }

        [Fact]
        public void ConvertToProto_MessageData()
        {
            var data = new PayloadData1 { Name = "test" };
            var cloudEvent = new CloudEvent { Data = data }.PopulateRequiredAttributes();
            var proto = new ProtobufEventFormatter().ConvertToProto(cloudEvent);
            Assert.Equal(Any.Pack(data), proto.ProtoData);
        }

        [Fact]
        public void ConvertToProto_MessageData_AlreadyPacked()
        {
            var data = new PayloadData1 { Name = "test" };
            var packedData = Any.Pack(data);
            var cloudEvent = new CloudEvent { Data = packedData }.PopulateRequiredAttributes();
            var proto = new ProtobufEventFormatter().ConvertToProto(cloudEvent);
            // This verifies that the formatter doesn't "double-encode".
            Assert.Equal(packedData, proto.ProtoData);
        }

        [Fact]
        public void ConvertToProto_MessageData_CustomTypeUrlPrefix()
        {
            string typeUrlPrefix = "cloudevents.io/xyz";
            var data = new PayloadData1 { Name = "test" };
            var cloudEvent = new CloudEvent { Data = data }.PopulateRequiredAttributes();
            var proto = new ProtobufEventFormatter(typeUrlPrefix).ConvertToProto(cloudEvent);
            Assert.Equal(Any.Pack(data, typeUrlPrefix), proto.ProtoData);
        }

        [Fact]
        public void ConvertToProto_InvalidData()
        {
            var cloudEvent = new CloudEvent { Data = new object() }.PopulateRequiredAttributes();
            var formatter = new ProtobufEventFormatter();
            Assert.Throws<ArgumentException>(() => formatter.ConvertToProto(cloudEvent));
        }

        [Fact]
        public void EncodeBinaryModeData_Bytes()
        {
            var cloudEvent = new CloudEvent
            {
                Data = SampleBinaryData
            }.PopulateRequiredAttributes();
            var formatter = new ProtobufEventFormatter();

            var result = formatter.EncodeBinaryModeEventData(cloudEvent);
            Assert.Equal(SampleBinaryData, result.ToArray());
        }

        [Theory]
        [InlineData("utf-8")]
        [InlineData("iso-8859-1")]
        [InlineData(null)]
        public void EncodeBinaryModeData_String_TextContentType(string charset)
        {
            string text = "caf\u00e9"; // Valid in both UTF-8 and ISO-8859-1, but with different representations
            var encoding = charset is null ? Encoding.UTF8 : Encoding.GetEncoding(charset);
            string contentType = charset is null ? "text/plain" : $"text/plain; charset={charset}";
            var cloudEvent = new CloudEvent
            {
                Data = text,
                DataContentType = contentType
            }.PopulateRequiredAttributes();

            var formatter = new ProtobufEventFormatter();
            var result = formatter.EncodeBinaryModeEventData(cloudEvent);
            Assert.Equal(encoding.GetBytes(text), result.ToArray());
        }

        [Fact]
        public void EncodeBinaryModeData_String_NonTextContentType()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "text",
                DataContentType = "application/json"
            }.PopulateRequiredAttributes();
            var formatter = new ProtobufEventFormatter();
            Assert.Throws<ArgumentException>(() => formatter.EncodeBinaryModeEventData(cloudEvent));
        }

        [Fact]
        public void EncodeBinaryModeData_ProtoMessage()
        {
            var cloudEvent = new CloudEvent
            {
                Data = new PayloadData1 { Name = "fail" },
                DataContentType = "application/protobuf"
            }.PopulateRequiredAttributes();
            var formatter = new ProtobufEventFormatter();
            // See summary documentation for ProtobufEventFormatter for the reasoning for this
            Assert.Throws<ArgumentException>(() => formatter.EncodeBinaryModeEventData(cloudEvent));
        }

        [Fact]
        public void EncodeBinaryModeData_ArbitraryObject()
        {
            var cloudEvent = new CloudEvent
            {
                Data = new object(),
                DataContentType = "application/octet-stream"
            }.PopulateRequiredAttributes();
            var formatter = new ProtobufEventFormatter();
            // See summary documentation for ProtobufEventFormatter for the reasoning for this
            Assert.Throws<ArgumentException>(() => formatter.EncodeBinaryModeEventData(cloudEvent));
        }

        [Fact]
        public void EncodeBinaryModeData_NoData()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var formatter = new ProtobufEventFormatter();
            Assert.Empty(formatter.EncodeBinaryModeEventData(cloudEvent).ToArray());
        }

        // Just a single test for the code that parses asynchronously... the guts are all the same.
        [Fact]
        public async Task DecodeStructuredModeMessageAsync_Minimal()
        {
            var proto = CreateMinimalCloudEventProto();
            byte[] bytes = proto.ToByteArray();
            var stream = new MemoryStream(bytes);
            var formatter = new ProtobufEventFormatter();
            var cloudEvent = await formatter.DecodeStructuredModeMessageAsync(stream, s_protobufCloudEventContentType, null);
            Assert.Equal("test-type", cloudEvent.Type);
            Assert.Equal("test-id", cloudEvent.Id);
            Assert.Equal(SampleUri, cloudEvent.Source);
        }

        [Fact]
        public void DecodeStructuredModeMessage_Minimal()
        {
            var proto = CreateMinimalCloudEventProto();
            byte[] bytes = proto.ToByteArray();
            var stream = new MemoryStream(bytes);
            var formatter = new ProtobufEventFormatter();
            var cloudEvent = formatter.DecodeStructuredModeMessage(stream, s_protobufCloudEventContentType, null);
            Assert.Equal("test-type", cloudEvent.Type);
            Assert.Equal("test-id", cloudEvent.Id);
            Assert.Equal(SampleUri, cloudEvent.Source);
        }

        [Fact]
        public void EncodeBatchModeMessage_Empty()
        {
            var formatter = new ProtobufEventFormatter();
            var bytes = formatter.EncodeBatchModeMessage(new CloudEvent[0], out var contentType);
            Assert.Equal("application/cloudevents-batch+protobuf; charset=utf-8", contentType.ToString());
            var batch = V1.CloudEventBatch.Parser.ParseFrom(bytes.ToArray());
            Assert.Empty(batch.Events);
        }

        [Fact]
        public void EncodeBatchModeMessage_TwoEvents()
        {
            var event1 = new CloudEvent
            {
                Id = "event1",
                Type = "type1",
                Source = new Uri("//event-source1", UriKind.RelativeOrAbsolute),
                Data = "simple text",
                DataContentType = "text/plain"
            };
            var event2 = new CloudEvent
            {
                Id = "event2",
                Type = "type2",
                Source = new Uri("//event-source2", UriKind.RelativeOrAbsolute),
            };

            var cloudEvents = new[] { event1, event2 };
            var formatter = new ProtobufEventFormatter();
            var bytes = formatter.EncodeBatchModeMessage(cloudEvents, out var contentType);
            Assert.Equal("application/cloudevents-batch+protobuf; charset=utf-8", contentType.ToString());
            var actualBatch = V1.CloudEventBatch.Parser.ParseFrom(bytes.ToArray());
            var expectedBatch = new V1.CloudEventBatch
            {
                Events =
                {
                    new V1.CloudEvent
                    {
                        SpecVersion = "1.0",
                        Type = "type1",
                        Id = "event1",
                        Source = "//event-source1",
                        TextData = "simple text",
                        Attributes = { { "datacontenttype", StringAttribute("text/plain") }  }
                    },
                    new V1.CloudEvent
                    {
                        SpecVersion = "1.0",
                        Type = "type2",
                        Id = "event2",
                        Source = "//event-source2"
                    }
                }
            };
            Assert.Equal(expectedBatch, actualBatch);
        }

        [Fact]
        public void EncodeBatchModeMessage_Invalid()
        {
            var formatter = new ProtobufEventFormatter();
            // Invalid CloudEvent
            Assert.Throws<ArgumentException>(() => formatter.EncodeBatchModeMessage(new[] { new CloudEvent() }, out _));
            // Null argument
            Assert.Throws<ArgumentNullException>(() => formatter.EncodeBatchModeMessage(null!, out _));
            // Null value within the argument. Arguably this should throw ArgumentException instead of
            // ArgumentNullException, but it's unlikely to cause confusion.
            Assert.Throws<ArgumentNullException>(() => formatter.EncodeBatchModeMessage(new CloudEvent[1], out _));
        }

        [Fact]
        public void ConvertFromProto_V1Attributes()
        {
            var proto = new V1.CloudEvent
            {
                SpecVersion = "1.0",
                Type = "test-type",
                Id = "test-id",
                TextData = "text",
                Source = "//event-source",
                Attributes =
                {
                    { "datacontenttype", StringAttribute("text/plain") },
                    { "dataschema", UriAttribute("https://data-schema") },
                    { "subject", StringAttribute("event-subject") },
                    { "time", TimestampAttribute(SampleTimestamp) }
                }
            };
            var cloudEvent = new ProtobufEventFormatter().ConvertFromProto(proto, null);
            Assert.Equal(CloudEventsSpecVersion.V1_0, cloudEvent.SpecVersion);
            Assert.Equal("test-type", cloudEvent.Type);
            Assert.Equal("test-id", cloudEvent.Id);
            Assert.Equal("text/plain", cloudEvent.DataContentType);
            Assert.Equal(new Uri("https://data-schema"), cloudEvent.DataSchema);
            Assert.Equal("event-subject", cloudEvent.Subject);
            Assert.Equal(new Uri("//event-source", UriKind.RelativeOrAbsolute), cloudEvent.Source);
            // The protobuf timestamp loses the offset information, but is still the correct instant.
            AssertTimestampsEqual(SampleTimestamp.ToUniversalTime(), cloudEvent.Time);
        }

        [Theory]
        // These are required, so have to be specified in the dedicated protobuf field
        [InlineData("id")]
        [InlineData("type")]
        [InlineData("source")]
        // These are generally invalid attribute names
        [InlineData("specversion")]
        [InlineData("a b c")]
        [InlineData("ABC")]
        public void ConvertFromProto_InvalidAttributeNames(string attributeName)
        {
            var proto = CreateMinimalCloudEventProto();
            proto.Attributes.Add(attributeName, StringAttribute("value"));
            var formatter = new ProtobufEventFormatter();
            Assert.Throws<ArgumentException>(() => formatter.ConvertFromProto(proto, null));
        }

        [Fact]
        public void ConvertFromProto_AllAttributeTypes()
        {
            var proto = CreateMinimalCloudEventProto();
            proto.Attributes.Add("binary", BinaryAttribute(SampleBinaryData));
            proto.Attributes.Add("boolean", BooleanAttribute(true));
            proto.Attributes.Add("integer", IntegerAttribute(10));
            proto.Attributes.Add("string", StringAttribute("text"));
            proto.Attributes.Add("timestamp", TimestampAttribute(SampleTimestamp));
            proto.Attributes.Add("uri", UriAttribute(SampleUriText));
            proto.Attributes.Add("urireference", UriRefAttribute(SampleUriReferenceText));

            var cloudEvent = new ProtobufEventFormatter().ConvertFromProto(proto, null);
            Assert.Equal(SampleBinaryData, cloudEvent["binary"]);
            Assert.True((bool) cloudEvent["boolean"]!);
            Assert.Equal(10, cloudEvent["integer"]);
            Assert.Equal("text", cloudEvent["string"]);
            // The protobuf timestamp loses the offset information, but is still the correct instant.
            AssertTimestampsEqual(SampleTimestamp.ToUniversalTime(), (DateTimeOffset) cloudEvent["timestamp"]!);
            Assert.Equal(SampleUri, cloudEvent["uri"]);
            Assert.Equal(SampleUriReference, cloudEvent["urireference"]);
        }

        [Fact]
        public void ConvertFromProto_NoData()
        {
            var proto = CreateMinimalCloudEventProto();
            var cloudEvent = new ProtobufEventFormatter().ConvertFromProto(proto, null);
            Assert.Null(cloudEvent.Data);
        }

        [Fact]
        public void ConvertFromProto_TextData()
        {
            var proto = CreateMinimalCloudEventProto();
            proto.TextData = "text";
            var cloudEvent = new ProtobufEventFormatter().ConvertFromProto(proto, null);
            Assert.Equal("text", cloudEvent.Data);
        }

        [Fact]
        public void ConvertFromProto_BinaryData()
        {
            var proto = CreateMinimalCloudEventProto();
            proto.BinaryData = ByteString.CopyFrom(SampleBinaryData);
            var cloudEvent = new ProtobufEventFormatter().ConvertFromProto(proto, null);
            Assert.Equal(SampleBinaryData, cloudEvent.Data);
        }

        [Fact]
        public void ConvertFromProto_MessageData()
        {
            var message = new PayloadData1 { Name = "testing" };
            var proto = CreateMinimalCloudEventProto();
            proto.ProtoData = Any.Pack(message);
            var cloudEvent = new ProtobufEventFormatter().ConvertFromProto(proto, null);
            // Note: this isn't unpacked automatically.
            Assert.Equal(Any.Pack(message), cloudEvent.Data);
        }

        [Fact]
        public void ConvertFromProto_UnspecifiedExtensionAttributes()
        {
            var proto = CreateMinimalCloudEventProto();
            proto.Attributes.Add("xyz", StringAttribute("abc"));
            var cloudEvent = new ProtobufEventFormatter().ConvertFromProto(proto, null);
            Assert.Equal("abc", cloudEvent["xyz"]);
        }

        [Fact]
        public void ConvertFromProto_SpecifiedExtensionAttributes_Valid()
        {
            var attribute = CloudEventAttribute.CreateExtension("xyz", CloudEventAttributeType.String);
            var proto = CreateMinimalCloudEventProto();
            proto.Attributes.Add(attribute.Name, StringAttribute("abc"));
            var cloudEvent = new ProtobufEventFormatter().ConvertFromProto(proto, new[] { attribute });
            Assert.Equal("abc", cloudEvent[attribute]);
        }

        [Fact]
        public void ConvertFromProto_SpecifiedExtensionAttributes_UnexpectedType()
        {
            var attribute = CloudEventAttribute.CreateExtension("xyz", CloudEventAttributeType.UriReference);
            var proto = CreateMinimalCloudEventProto();
            proto.Attributes.Add(attribute.Name, UriAttribute("https://xyz"));
            // Even though the value would be valid as a URI reference, we fail because
            // the type in the proto message is not the same as the type we've specified in the method argument.
            Assert.Throws<ArgumentException>(() => new ProtobufEventFormatter().ConvertFromProto(proto, new[] { attribute }));
        }

        [Fact]
        public void ConvertFromProto_SpecifiedExtensionAttributes_InvalidValue()
        {
            var attribute = CloudEventAttribute.CreateExtension("xyz", CloudEventAttributeType.Integer, ValidateValue);
            var proto = CreateMinimalCloudEventProto();
            proto.Attributes.Add(attribute.Name, IntegerAttribute(1000));
            var exception = Assert.Throws<ArgumentException>(() => new ProtobufEventFormatter().ConvertFromProto(proto, new[] { attribute }));
            Assert.Equal("Boom!", exception!.InnerException!.Message);

            void ValidateValue(object value)
            {
                if ((int) value > 100)
                {
                    throw new Exception("Boom!");
                }
            }
        }

        [Fact]
        public void ConvertFromProto_Invalid_NoSpecVersion()
        {
            var proto = CreateMinimalCloudEventProto();
            proto.SpecVersion = "";
            var exception = Assert.Throws<ArgumentException>(() => new ProtobufEventFormatter().ConvertFromProto(proto, null));
        }

        [Fact]
        public void ConvertFromProto_Invalid_NoType()
        {
            var proto = CreateMinimalCloudEventProto();
            proto.SpecVersion = "";
            var exception = Assert.Throws<ArgumentException>(() => new ProtobufEventFormatter().ConvertFromProto(proto, null));
        }

        [Fact]
        public void ConvertFromProto_Invalid_NoId()
        {
            var proto = CreateMinimalCloudEventProto();
            proto.Id = "";
            var exception = Assert.Throws<ArgumentException>(() => new ProtobufEventFormatter().ConvertFromProto(proto, null));
        }

        [Fact]
        public void ConvertFromProto_Invalid_NoSource()
        {
            var proto = CreateMinimalCloudEventProto();
            proto.Source = "";
            var exception = Assert.Throws<ArgumentException>(() => new ProtobufEventFormatter().ConvertFromProto(proto, null));
        }

        [Theory]
        [InlineData("utf-8")]
        [InlineData("iso-8859-1")]
        [InlineData(null)]
        public void DecodeBinaryModeEventData_Text(string charset)
        {
            string text = "caf\u00e9"; // Valid in both UTF-8 and ISO-8859-1, but with different representations
            var encoding = charset is null ? Encoding.UTF8 : Encoding.GetEncoding(charset);
            var bytes = encoding.GetBytes(text);
            string contentType = charset is null ? "text/plain" : $"text/plain; charset={charset}";
            var data = DecodeBinaryModeEventData(bytes, contentType);
            string actualText = Assert.IsType<string>(data);
            Assert.Equal(text, actualText);
        }

        [Theory]
        [InlineData("application/json")]
        [InlineData(null)]
        public void DecodeBinaryModeData_NonTextContentType(string contentType)
        {
            var bytes = Encoding.UTF8.GetBytes("{}");
            var data = DecodeBinaryModeEventData(bytes, contentType);
            byte[] actualBytes = Assert.IsType<byte[]>(data);
            Assert.Equal(bytes, actualBytes);
        }

        [Fact]
        public void DecodeBatchMode_Minimal()
        {
            var batchProto = new V1.CloudEventBatch
            {
                Events = { CreateMinimalCloudEventProto() }
            };
            byte[] bytes = batchProto.ToByteArray();
            var stream = new MemoryStream(bytes);
            var formatter = new ProtobufEventFormatter();
            var cloudEvents = formatter.DecodeBatchModeMessage(stream, s_protobufCloudEventBatchContentType, null);
            var cloudEvent = Assert.Single(cloudEvents);
            Assert.Equal("test-type", cloudEvent.Type);
            Assert.Equal("test-id", cloudEvent.Id);
            Assert.Equal(SampleUri, cloudEvent.Source);
        }

        // Just a single test for the code that parses asynchronously... the guts are all the same.
        [Fact]
        public async Task DecodeBatchModeMessageAsync_Minimal()
        {
            var batchProto = new V1.CloudEventBatch
            {
                Events = { CreateMinimalCloudEventProto() }
            };
            byte[] bytes = batchProto.ToByteArray();
            var stream = new MemoryStream(bytes);
            var formatter = new ProtobufEventFormatter();
            var cloudEvents = await formatter.DecodeBatchModeMessageAsync(stream, s_protobufCloudEventBatchContentType, null);
            var cloudEvent = Assert.Single(cloudEvents);
            Assert.Equal("test-type", cloudEvent.Type);
            Assert.Equal("test-id", cloudEvent.Id);
            Assert.Equal(SampleUri, cloudEvent.Source);
        }

        [Fact]
        public void DecodeBatchMode_Empty()
        {
            var batchProto = new V1.CloudEventBatch();
            byte[] bytes = batchProto.ToByteArray();
            var stream = new MemoryStream(bytes);
            var formatter = new ProtobufEventFormatter();
            var cloudEvents = formatter.DecodeBatchModeMessage(stream, s_protobufCloudEventBatchContentType, null);
            Assert.Empty(cloudEvents);
        }

        [Fact]
        public void DecodeBatchMode_Multiple()
        {
            var batchProto = new V1.CloudEventBatch
            {
                Events =
                {
                    new V1.CloudEvent
                    {
                        SpecVersion = "1.0",
                        Type = "type1",
                        Id = "event1",
                        Source = "//event-source1",
                        TextData = "simple text",
                        Attributes = { { "datacontenttype", StringAttribute("text/plain") }  }
                    },
                    new V1.CloudEvent
                    {
                        SpecVersion = "1.0",
                        Type = "type2",
                        Id = "event2",
                        Source = "//event-source2"
                    }
                }
            };


            byte[] bytes = batchProto.ToByteArray();
            var stream = new MemoryStream(bytes);
            var formatter = new ProtobufEventFormatter();
            var cloudEvents = formatter.DecodeBatchModeMessage(stream, s_protobufCloudEventBatchContentType, null);
            Assert.Equal(2, cloudEvents.Count);

            var event1 = cloudEvents[0];
            Assert.Equal("type1", event1.Type);
            Assert.Equal("event1", event1.Id);
            Assert.Equal(new Uri("//event-source1", UriKind.RelativeOrAbsolute), event1.Source);
            Assert.Equal("simple text", event1.Data);
            Assert.Equal("text/plain", event1.DataContentType);

            var event2 = cloudEvents[1];
            Assert.Equal("type2", event2.Type);
            Assert.Equal("event2", event2.Id);
            Assert.Equal(new Uri("//event-source2", UriKind.RelativeOrAbsolute), event2.Source);
            Assert.Null(event2.Data);
            Assert.Null(event2.DataContentType);
        }

        // Utility methods

        private static object? DecodeBinaryModeEventData(byte[] bytes, string contentType)
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.DataContentType = contentType;
            new ProtobufEventFormatter().DecodeBinaryModeEventData(bytes, cloudEvent);
            return cloudEvent.Data;
        }

        private static CloudEventAttributeValue StringAttribute(string value) =>
            new CloudEventAttributeValue { CeString = value };

        private static CloudEventAttributeValue BinaryAttribute(byte[] value) =>
            new CloudEventAttributeValue { CeBytes = ByteString.CopyFrom(value) };

        private static CloudEventAttributeValue BooleanAttribute(bool value) =>
            new CloudEventAttributeValue { CeBoolean = value };

        private static CloudEventAttributeValue IntegerAttribute(int value) =>
            new CloudEventAttributeValue { CeInteger = value };

        private static CloudEventAttributeValue UriAttribute(string value) =>
            new CloudEventAttributeValue { CeUri = value };

        private static CloudEventAttributeValue UriRefAttribute(string value) =>
            new CloudEventAttributeValue { CeUriRef = value };

        private static CloudEventAttributeValue TimestampAttribute(Timestamp value) =>
            new CloudEventAttributeValue { CeTimestamp = value };

        private static CloudEventAttributeValue TimestampAttribute(DateTimeOffset value) =>
            TimestampAttribute(Timestamp.FromDateTimeOffset(value));

        private static V1.CloudEvent CreateMinimalCloudEventProto() => new V1.CloudEvent
        {
            SpecVersion = "1.0",
            Type = "test-type",
            Id = "test-id",
            Source = SampleUriText
        };
    }
}
