// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.UnitTests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.NewtonsoftJson.UnitTests
{
    public class JsonEventFormatterTest
    {
        private static readonly ContentType s_jsonCloudEventContentType = new ContentType("application/cloudevents+json; charset=utf-8");
        private static readonly ContentType s_jsonCloudEventBatchContentType = new ContentType("application/cloudevents-batch+json; charset=utf-8");
        private const string NonAsciiValue = "GBP=\u00a3";

        /// <summary>
        /// A simple test that populates all known v1.0 attributes, so we don't need to test that
        /// aspect in the future.
        /// </summary>
        [Fact]
        public void EncodeStructuredModeMessage_V1Attributes()
        {
            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Data = "text", // Just so that it's reasonable to have a DataContentType
                DataContentType = "text/plain",
                DataSchema = new Uri("https://data-schema"),
                Id = "event-id",
                Source = new Uri("https://event-source"),
                Subject = "event-subject",
                Time = new DateTimeOffset(2021, 2, 19, 12, 34, 56, 789, TimeSpan.FromHours(1)),
                Type = "event-type"
            };

            var encoded = new JsonEventFormatter().EncodeStructuredModeMessage(cloudEvent, out var contentType);
            Assert.Equal("application/cloudevents+json; charset=utf-8", contentType.ToString());
            JObject obj = ParseJson(encoded);
            var asserter = new JTokenAsserter
            {
                { "data", JTokenType.String, "text" },
                { "datacontenttype", JTokenType.String, "text/plain" },
                { "dataschema", JTokenType.String, "https://data-schema" },
                { "id", JTokenType.String, "event-id" },
                { "source", JTokenType.String, "https://event-source" },
                { "specversion", JTokenType.String, "1.0" },
                { "subject", JTokenType.String, "event-subject" },
                { "time", JTokenType.String, "2021-02-19T12:34:56.789+01:00" },
                { "type", JTokenType.String, "event-type" },
            };
            asserter.AssertProperties(obj, assertCount: true);
        }

        [Fact]
        public void EncodeStructuredModeMessage_AllAttributeTypes()
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

            JObject obj = EncodeAndParseStructured(cloudEvent);
            var asserter = new JTokenAsserter
            {
                { "binary", JTokenType.String, SampleBinaryDataBase64 },
                { "boolean", JTokenType.Boolean, true },
                { "integer", JTokenType.Integer, 10 },
                { "string", JTokenType.String, "text" },
                { "timestamp", JTokenType.String, SampleTimestampText },
                { "uri", JTokenType.String, SampleUriText },
                { "urireference", JTokenType.String, SampleUriReferenceText },
            };
            asserter.AssertProperties(obj, assertCount: false);
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_ObjectSerialization()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new { Text = "simple text" };
            cloudEvent.DataContentType = "application/json";
            JObject obj = EncodeAndParseStructured(cloudEvent);
            JObject dataProperty = (JObject) obj["data"];
            var asserter = new JTokenAsserter
            {
                { "Text", JTokenType.String, "simple text" }
            };
            asserter.AssertProperties(dataProperty, assertCount: true);
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_CustomSerializer()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new { DateValue = new DateTime(2021, 2, 19, 12, 49, 34, DateTimeKind.Utc) };
            cloudEvent.DataContentType = "application/json";

            var serializer = new JsonSerializer
            {
                DateFormatString = "yyyy-MM-dd"
            };
            var formatter = new JsonEventFormatter(serializer);
            var encoded = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
            JObject obj = ParseJson(encoded);
            JObject dataProperty = (JObject) obj["data"];
            var asserter = new JTokenAsserter
            {
                { "DateValue", JTokenType.String, "2021-02-19" }
            };
            asserter.AssertProperties(dataProperty, assertCount: true);
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_AttributedModel()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new AttributedModel { AttributedProperty = "simple text" };
            cloudEvent.DataContentType = "application/json";
            JObject obj = EncodeAndParseStructured(cloudEvent);
            JObject dataProperty = (JObject) obj["data"];
            var asserter = new JTokenAsserter
            {
                { AttributedModel.JsonPropertyName, JTokenType.String, "simple text" }
            };
            asserter.AssertProperties(dataProperty, assertCount: true);
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_JToken()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new JValue(100);
            cloudEvent.DataContentType = "application/json";
            JObject obj = EncodeAndParseStructured(cloudEvent);
            JToken data = obj["data"];
            Assert.Equal(JTokenType.Integer, data.Type);
            Assert.Equal(100, (int) data);
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_NullValue()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = null;
            cloudEvent.DataContentType = "application/json";
            JObject obj = EncodeAndParseStructured(cloudEvent);
            Assert.False(obj.ContainsKey("data"));
        }

        [Fact]
        public void EncodeStructuredModeMessage_TextType_String()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = "some text";
            cloudEvent.DataContentType = "text/anything";
            JObject obj = EncodeAndParseStructured(cloudEvent);
            var dataProperty = obj["data"];
            Assert.Equal(JTokenType.String, dataProperty.Type);
            Assert.Equal("some text", (string) dataProperty);
        }

        // A text content type with bytes as data is serialized like any other bytes.
        [Fact]
        public void EncodeStructuredModeMessage_TextType_Bytes()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = SampleBinaryData;
            cloudEvent.DataContentType = "text/anything";
            JObject obj = EncodeAndParseStructured(cloudEvent);
            Assert.False(obj.ContainsKey("data"));
            var dataBase64 = obj["data_base64"];
            Assert.Equal(JTokenType.String, dataBase64.Type);
            Assert.Equal(SampleBinaryDataBase64, (string) dataBase64);
        }

        [Fact]
        public void EncodeStructuredModeMessage_TextType_NotStringOrBytes()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new object();
            cloudEvent.DataContentType = "text/anything";
            var formatter = new JsonEventFormatter();
            Assert.Throws<ArgumentException>(() => formatter.EncodeStructuredModeMessage(cloudEvent, out _));
        }

        [Fact]
        public void EncodeStructuredModeMessage_ArbitraryType_Bytes()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = SampleBinaryData;
            cloudEvent.DataContentType = "not_text/or_json";
            JObject obj = EncodeAndParseStructured(cloudEvent);
            Assert.False(obj.ContainsKey("data"));
            var dataBase64 = obj["data_base64"];
            Assert.Equal(JTokenType.String, dataBase64.Type);
            Assert.Equal(SampleBinaryDataBase64, (string) dataBase64);
        }

        [Fact]
        public void EncodeStructuredModeMessage_ArbitraryType_NotBytes()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new object();
            cloudEvent.DataContentType = "not_text/or_json";
            var formatter = new JsonEventFormatter();
            Assert.Throws<ArgumentException>(() => formatter.EncodeStructuredModeMessage(cloudEvent, out _));
        }

        [Fact]
        public void EncodeBinaryModeEventData_JsonDataType_ObjectSerialization()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new { Text = "simple text" };
            cloudEvent.DataContentType = "application/json";
            var bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            JObject data = ParseJson(bytes);
            var asserter = new JTokenAsserter
            {
                { "Text", JTokenType.String, "simple text" }
            };
            asserter.AssertProperties(data, assertCount: true);
        }

        [Fact]
        public void EncodeBinaryModeEventData_JsonDataType_CustomSerializer()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new { DateValue = new DateTime(2021, 2, 19, 12, 49, 34, DateTimeKind.Utc) };
            cloudEvent.DataContentType = "application/json";

            var serializer = new JsonSerializer
            {
                DateFormatString = "yyyy-MM-dd"
            };
            var formatter = new JsonEventFormatter(serializer);
            var bytes = formatter.EncodeBinaryModeEventData(cloudEvent);
            JObject data = ParseJson(bytes);
            var asserter = new JTokenAsserter
            {
                { "DateValue", JTokenType.String, "2021-02-19" }
            };
            asserter.AssertProperties(data, assertCount: true);
        }

        [Fact]
        public void EncodeBinaryModeEventData_JsonDataType_AttributedModel()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new AttributedModel { AttributedProperty = "simple text" };
            cloudEvent.DataContentType = "application/json";
            var bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            JObject data = ParseJson(bytes);
            var asserter = new JTokenAsserter
            {
                { AttributedModel.JsonPropertyName, JTokenType.String, "simple text" }
            };
            asserter.AssertProperties(data, assertCount: true);
        }

        [Fact]
        public void EncodeBinaryModeEventData_JsonDataType_JToken()
        {
            // This would definitely be an odd thing to do, admittedly...
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new JValue(100);
            cloudEvent.DataContentType = "application/json";
            var bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            Assert.Equal("100", BinaryDataUtilities.GetString(bytes, Encoding.UTF8));
        }

        [Fact]
        public void EncodeBinaryModeEventData_JsonDataType_NullValue()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = null;
            cloudEvent.DataContentType = "application/json";
            var bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            Assert.True(bytes.IsEmpty);
        }

        [Fact]
        public void EncodeBinaryModeEventData_TextType_String()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = "some text";
            cloudEvent.DataContentType = "text/anything";
            var bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            Assert.Equal("some text", BinaryDataUtilities.GetString(bytes, Encoding.UTF8));
        }

        // A text content type with bytes as data is serialized like any other bytes.
        [Fact]
        public void EncodeBinaryModeEventData_TextType_Bytes()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = SampleBinaryData;
            cloudEvent.DataContentType = "text/anything";
            var bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            Assert.Equal(SampleBinaryData, bytes);
        }

        [Fact]
        public void EncodeBinaryModeEventData_TextType_NotStringOrBytes()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new object();
            cloudEvent.DataContentType = "text/anything";
            var formatter = new JsonEventFormatter();
            Assert.Throws<ArgumentException>(() => formatter.EncodeBinaryModeEventData(cloudEvent));
        }

        [Fact]
        public void EncodeBinaryModeEventData_ArbitraryType_Bytes()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = SampleBinaryData;
            cloudEvent.DataContentType = "not_text/or_json";
            var bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            Assert.Equal(SampleBinaryData, bytes);
        }

        [Fact]
        public void EncodeBinaryModeEventData_ArbitraryType_NotBytes()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new object();
            cloudEvent.DataContentType = "not_text/or_json";
            var formatter = new JsonEventFormatter();
            Assert.Throws<ArgumentException>(() => formatter.EncodeBinaryModeEventData(cloudEvent));
        }

        // Note: batch mode testing is restricted to the batch aspects; we assume that the
        // per-CloudEvent implementation is shared with structured mode, so we rely on
        // structured mode testing for things like custom serialization.

        [Fact]
        public void EncodeBatchModeMessage_Empty()
        {
            var cloudEvents = new CloudEvent[0];
            var formatter = new JsonEventFormatter();
            var bytes = formatter.EncodeBatchModeMessage(cloudEvents, out var contentType);
            Assert.Equal("application/cloudevents-batch+json; charset=utf-8", contentType.ToString());
            var array = ParseJsonArray(bytes);
            Assert.Empty(array);
        }

        [Fact]
        public void EncodeBatchModeMessage_TwoEvents()
        {
            var event1 = new CloudEvent().PopulateRequiredAttributes();
            event1.Id = "event1";
            event1.Data = "simple text";
            event1.DataContentType = "text/plain";

            var event2 = new CloudEvent().PopulateRequiredAttributes();
            event2.Id = "event2";

            var cloudEvents = new[] { event1, event2 };
            var formatter = new JsonEventFormatter();
            var bytes = formatter.EncodeBatchModeMessage(cloudEvents, out var contentType);
            Assert.Equal("application/cloudevents-batch+json; charset=utf-8", contentType.ToString());
            var array = ParseJsonArray(bytes);
            Assert.Equal(2, array.Count);

            var asserter1 = new JTokenAsserter
            {
                { "specversion", JTokenType.String, "1.0" },
                { "id", JTokenType.String, event1.Id },
                { "type", JTokenType.String, event1.Type },
                { "source", JTokenType.String, "//test" },
                { "data", JTokenType.String, "simple text" },
                { "datacontenttype", JTokenType.String, event1.DataContentType }
            };
            asserter1.AssertProperties((JObject) array[0], assertCount: true);

            var asserter2 = new JTokenAsserter
            {
                { "specversion", JTokenType.String, "1.0" },
                { "id", JTokenType.String, event2.Id },
                { "type", JTokenType.String, event2.Type },
                { "source", JTokenType.String, "//test" },
            };
            asserter2.AssertProperties((JObject) array[1], assertCount: true);
        }

        [Fact]
        public void EncodeBatchModeMessage_Invalid()
        {
            var formatter = new JsonEventFormatter();
            // Invalid CloudEvent
            Assert.Throws<ArgumentException>(() => formatter.EncodeBatchModeMessage(new[] { new CloudEvent() }, out _));
            // Null argument
            Assert.Throws<ArgumentNullException>(() => formatter.EncodeBatchModeMessage(null, out _));
            // Null value within the argument. Arguably this should throw ArgumentException instead of
            // ArgumentNullException, but it's unlikely to cause confusion.
            Assert.Throws<ArgumentNullException>(() => formatter.EncodeBatchModeMessage(new CloudEvent[1], out _));
        }

        [Fact]
        public void DecodeStructuredModeMessage_NotJson()
        {
            var formatter = new JsonEventFormatter();
            Assert.Throws<JsonReaderException>(() => formatter.DecodeStructuredModeMessage(new byte[10], new ContentType("application/json"), null));
        }

        // Just a single test for the code that parses asynchronously... the guts are all the same.
        [Fact]
        public async Task DecodeStructuredModeMessageAsync_Minimal()
        {
            var obj = new JObject
            {
                ["specversion"] = "1.0",
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["source"] = SampleUriText,
            };
            byte[] bytes = Encoding.UTF8.GetBytes(obj.ToString());
            var stream = new MemoryStream(bytes);
            var formatter = new JsonEventFormatter();
            var cloudEvent = await formatter.DecodeStructuredModeMessageAsync(stream, s_jsonCloudEventContentType, null);
            Assert.Equal("test-type", cloudEvent.Type);
            Assert.Equal("test-id", cloudEvent.Id);
            Assert.Equal(SampleUri, cloudEvent.Source);
        }

        [Fact]
        public void DecodeStructuredModeMessage_Minimal()
        {
            var obj = new JObject
            {
                ["specversion"] = "1.0",
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["source"] = SampleUriText,
            };
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal("test-type", cloudEvent.Type);
            Assert.Equal("test-id", cloudEvent.Id);
            Assert.Equal(SampleUri, cloudEvent.Source);
        }

        [Fact]
        public void DecodeStructuredModeMessage_NoSpecVersion()
        {
            var obj = new JObject
            {
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["source"] = SampleUriText,
            };
            Assert.Throws<ArgumentException>(() => DecodeStructuredModeMessage(obj));
        }

        [Fact]
        public void DecodeStructuredModeMessage_UnknownSpecVersion()
        {
            var obj = new JObject
            {
                ["specversion"] = "0.5",
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["source"] = SampleUriText,
            };
            Assert.Throws<ArgumentException>(() => DecodeStructuredModeMessage(obj));
        }

        [Fact]
        public void DecodeStructuredModeMessage_MissingRequiredAttributes()
        {
            var obj = new JObject
            {
                ["specversion"] = "1.0",
                ["type"] = "test-type",
                ["id"] = "test-id"
                // Source is missing
            };
            Assert.Throws<ArgumentException>(() => DecodeStructuredModeMessage(obj));
        }

        [Fact]
        public void DecodeStructuredModeMessage_SpecVersionNotString()
        {
            var obj = new JObject
            {
                ["specversion"] = 1,
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["source"] = SampleUriText,
            };
            Assert.Throws<ArgumentException>(() => DecodeStructuredModeMessage(obj));
        }

        [Fact]
        public void DecodeStructuredModeMessage_TypeNotString()
        {
            var obj = new JObject
            {
                ["specversion"] = "1.0",
                ["type"] = 1,
                ["id"] = "test-id",
                ["source"] = SampleUriText,
            };
            Assert.Throws<ArgumentException>(() => DecodeStructuredModeMessage(obj));
        }

        [Fact]
        public void DecodeStructuredModeMessage_V1Attributes()
        {
            var obj = new JObject
            {
                ["specversion"] = "1.0",
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["data"] = "text", // Just so that it's reasonable to have a DataContentType,
                ["datacontenttype"] = "text/plain",
                ["dataschema"] = "https://data-schema",
                ["subject"] = "event-subject",
                ["source"] = "//event-source",
                ["time"] = SampleTimestampText
            };
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal(CloudEventsSpecVersion.V1_0, cloudEvent.SpecVersion);
            Assert.Equal("test-type", cloudEvent.Type);
            Assert.Equal("test-id", cloudEvent.Id);
            Assert.Equal("text/plain", cloudEvent.DataContentType);
            Assert.Equal(new Uri("https://data-schema"), cloudEvent.DataSchema);
            Assert.Equal("event-subject", cloudEvent.Subject);
            Assert.Equal(new Uri("//event-source", UriKind.RelativeOrAbsolute), cloudEvent.Source);
            AssertTimestampsEqual(SampleTimestamp, cloudEvent.Time);
        }

        [Fact]
        public void DecodeStructuredModeMessage_AllAttributeTypes()
        {
            var obj = new JObject
            {
                // Required attributes
                ["specversion"] = "1.0",
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["source"] = "//source",
                // Extension attributes
                ["binary"] = SampleBinaryDataBase64,
                ["boolean"] = true,
                ["integer"] = 10,
                ["string"] = "text",
                ["timestamp"] = SampleTimestampText,
                ["uri"] = SampleUriText,
                ["urireference"] = SampleUriReferenceText
            };

            byte[] bytes = Encoding.UTF8.GetBytes(obj.ToString());
            var formatter = new JsonEventFormatter();
            var cloudEvent = formatter.DecodeStructuredModeMessage(bytes, s_jsonCloudEventContentType, AllTypesExtensions);
            Assert.Equal(SampleBinaryData, cloudEvent["binary"]);
            Assert.True((bool) cloudEvent["boolean"]);
            Assert.Equal(10, cloudEvent["integer"]);
            Assert.Equal("text", cloudEvent["string"]);
            AssertTimestampsEqual(SampleTimestamp, (DateTimeOffset) cloudEvent["timestamp"]);
            Assert.Equal(SampleUri, cloudEvent["uri"]);
            Assert.Equal(SampleUriReference, cloudEvent["urireference"]);
        }

        [Fact]
        public void DecodeStructuredModeMessage_IncorrectExtensionTypeWithValidValue()
        {
            var obj = new JObject
            {
                ["specversion"] = "1.0",
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["source"] = "//source",
                // Incorrect type, but is a valid value for the extension
                ["integer"] = "10",
            };
            // Decode the event, providing the extension with the correct type.
            byte[] bytes = Encoding.UTF8.GetBytes(obj.ToString());
            var formatter = new JsonEventFormatter();
            var cloudEvent = formatter.DecodeStructuredModeMessage(bytes, s_jsonCloudEventContentType, AllTypesExtensions);

            // The value will have been decoded according to the extension.
            Assert.Equal(10, cloudEvent["integer"]);
        }

        // There are other invalid token types as well; this is just one of them.
        [Fact]
        public void DecodeStructuredModeMessage_AttributeValueAsArrayToken()
        {
            var obj = CreateMinimalValidJObject();
            obj["attr"] = new JArray();
            Assert.Throws<ArgumentException>(() => DecodeStructuredModeMessage(obj));
        }

        [Fact]
        public void DecodeStructuredModeMessage_Null()
        {
            var obj = CreateMinimalValidJObject();
            obj["attr"] = JValue.CreateNull();
            var cloudEvent = DecodeStructuredModeMessage(obj);
            // The JSON event format spec demands that we ignore null values, so we shouldn't
            // have created an extension attribute.
            Assert.Null(cloudEvent.GetAttribute("attr"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("application/json")]
        [InlineData("text/plain")]
        [InlineData("application/binary")]
        public void DecodeStructuredModeMessage_NoData(string contentType)
        {
            var obj = CreateMinimalValidJObject();
            if (contentType is object)
            {
                obj["datacontenttype"] = contentType;
            }
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Null(cloudEvent.Data);
        }

        [Fact]
        public void DecodeStructuredModeMessage_BothDataAndDataBase64()
        {
            var obj = CreateMinimalValidJObject();
            obj["data"] = "text";
            obj["data_base64"] = SampleBinaryDataBase64;
            Assert.Throws<ArgumentException>(() => DecodeStructuredModeMessage(obj));
        }

        [Fact]
        public void DecodeStructuredModeMessage_DataBase64NonString()
        {
            var obj = CreateMinimalValidJObject();
            obj["data_base64"] = 10;
            Assert.Throws<ArgumentException>(() => DecodeStructuredModeMessage(obj));
        }

        // data_base64 always ends up as bytes, regardless of content type.
        [Theory]
        [InlineData(null)]
        [InlineData("application/json")]
        [InlineData("text/plain")]
        [InlineData("application/binary")]
        public void DecodeStructuredModeMessage_Base64(string contentType)
        {
            var obj = CreateMinimalValidJObject();
            if (contentType is object)
            {
                obj["datacontenttype"] = contentType;
            }
            obj["data_base64"] = SampleBinaryDataBase64;
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal(SampleBinaryData, cloudEvent.Data);
        }

        [Fact]
        public void DecodeStructuredModeMessage_TextContentTypeStringToken()
        {
            var obj = CreateMinimalValidJObject();
            obj["datacontenttype"] = "text/plain";
            obj["data"] = "some text";
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal("some text", cloudEvent.Data);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("application/json")]
        [InlineData("text/plain")]
        [InlineData("application/not-quite-json")]
        public void DecodeStructuredModeMessage_JsonToken(string contentType)
        {
            var obj = CreateMinimalValidJObject();
            if (contentType is object)
            {
                obj["datacontenttype"] = contentType;
            }
            obj["data"] = 10;
            var cloudEvent = DecodeStructuredModeMessage(obj);
            var token = (JToken) cloudEvent.Data;
            Assert.Equal(JTokenType.Integer, token.Type);
            Assert.Equal(10, (int) token);
        }

        [Fact]
        public void DecodeStructuredModeMessage_NullDataBase64Ignored()
        {
            var obj = CreateMinimalValidJObject();
            obj["data_base64"] = JValue.CreateNull();
            obj["data"] = "some text";
            obj["datacontenttype"] = "text/plain";
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal("some text", cloudEvent.Data);
        }

        [Fact]
        public void DecodeStructuredModeMessage_NullDataIgnored()
        {
            var obj = CreateMinimalValidJObject();
            obj["data_base64"] = SampleBinaryDataBase64;
            obj["data"] = JValue.CreateNull();
            obj["datacontenttype"] = "application/binary";
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal(SampleBinaryData, cloudEvent.Data);
        }

        [Fact]
        public void DecodeBinaryModeEventData_EmptyData_JsonContentType()
        {
            var data = DecodeBinaryModeEventData(new byte[0], "application/json");
            Assert.Null(data);
        }

        [Fact]
        public void DecodeBinaryModeEventData_EmptyData_TextContentType()
        {
            var data = DecodeBinaryModeEventData(new byte[0], "text/plain");
            var text = Assert.IsType<string>(data);
            Assert.Equal("", text);
        }

        [Fact]
        public void DecodeBinaryModeEventData_EmptyData_OtherContentType()
        {
            var data = DecodeBinaryModeEventData(new byte[0], "application/binary");
            var byteArray = Assert.IsType<byte[]>(data);
            Assert.Empty(byteArray);
        }

        [Theory]
        [InlineData("utf-8")]
        [InlineData("iso-8859-1")]
        public void DecodeBinaryModeEventData_Json(string charset)
        {
            var encoding = Encoding.GetEncoding(charset);
            var bytes = encoding.GetBytes(new JObject { ["test"] = NonAsciiValue }.ToString());
            var data = DecodeBinaryModeEventData(bytes, $"application/json; charset={charset}");
            var obj = Assert.IsType<JObject>(data);
            var asserter = new JTokenAsserter
            {
                { "test", JTokenType.String, NonAsciiValue }
            };
            asserter.AssertProperties(obj, assertCount: true);
        }

        [Theory]
        [InlineData("utf-8")]
        [InlineData("iso-8859-1")]
        public void DecodeBinaryModeEventData_Text(string charset)
        {
            var encoding = Encoding.GetEncoding(charset);
            var bytes = encoding.GetBytes(NonAsciiValue);
            var data = DecodeBinaryModeEventData(bytes, $"text/plain; charset={charset}");
            var text = Assert.IsType<string>(data);
            Assert.Equal(NonAsciiValue, text);
        }

        [Fact]
        public void DecodeBinaryModeEventData_Binary()
        {
            byte[] bytes = { 0, 1, 2, 3 };
            var data = DecodeBinaryModeEventData(bytes, "application/binary");
            Assert.Equal(bytes, data);
        }

        [Fact]
        public void DecodeBatchMode_NotArray()
        {
            var formatter = new JsonEventFormatter();
            var data = Encoding.UTF8.GetBytes(CreateMinimalValidJObject().ToString());
            Assert.Throws<JsonReaderException>(() => formatter.DecodeBatchModeMessage(data, s_jsonCloudEventBatchContentType, extensionAttributes: null));
        }

        [Fact]
        public void DecodeBatchMode_ArrayContainingNonObject()
        {
            var formatter = new JsonEventFormatter();
            var array = new JArray { CreateMinimalValidJObject(), "text" };
            var data = Encoding.UTF8.GetBytes(array.ToString());
            Assert.Throws<ArgumentException>(() => formatter.DecodeBatchModeMessage(data, s_jsonCloudEventBatchContentType, extensionAttributes: null));
        }

        [Fact]
        public void DecodeBatchMode_Empty()
        {
            var cloudEvents = DecodeBatchModeMessage(new JArray());
            Assert.Empty(cloudEvents);
        }

        [Fact]
        public void DecodeBatchMode_Minimal()
        {
            var cloudEvents = DecodeBatchModeMessage(new JArray { CreateMinimalValidJObject() });
            var cloudEvent = Assert.Single(cloudEvents);
            Assert.Equal("event-type", cloudEvent.Type);
            Assert.Equal("event-id", cloudEvent.Id);
            Assert.Equal(new Uri("//event-source", UriKind.RelativeOrAbsolute), cloudEvent.Source);
        }

        // Just a single test for the code that parses asynchronously... the guts are all the same.
        [Fact]
        public async Task DecodeBatchModeMessageAsync_Minimal()
        {
            var obj = new JObject
            {
                ["specversion"] = "1.0",
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["source"] = SampleUriText,
            };
            byte[] bytes = Encoding.UTF8.GetBytes(new JArray { obj }.ToString());
            var stream = new MemoryStream(bytes);
            var formatter = new JsonEventFormatter();
            var cloudEvents = await formatter.DecodeBatchModeMessageAsync(stream, s_jsonCloudEventBatchContentType, null);
            var cloudEvent = Assert.Single(cloudEvents);
            Assert.Equal("test-type", cloudEvent.Type);
            Assert.Equal("test-id", cloudEvent.Id);
            Assert.Equal(SampleUri, cloudEvent.Source);
        }


        [Fact]
        public void DecodeBatchMode_Multiple()
        {
            var array = new JArray
            {
                new JObject
                {
                    ["specversion"] = "1.0",
                    ["type"] = "type1",
                    ["id"] = "event1",
                    ["source"] = "//event-source1",
                    ["data"] = "simple text",
                    ["datacontenttype"] = "text/plain"
                },
                new JObject
                {
                    ["specversion"] = "1.0",
                    ["type"] = "type2",
                    ["id"] = "event2",
                    ["source"] = "//event-source2"
                },
            };
            var cloudEvents = DecodeBatchModeMessage(array);
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

        private static object DecodeBinaryModeEventData(byte[] bytes, string contentType)
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.DataContentType = contentType;
            new JsonEventFormatter().DecodeBinaryModeEventData(bytes, cloudEvent);
            return cloudEvent.Data;
        }

        internal static JObject CreateMinimalValidJObject() =>
            new JObject
            {
                ["specversion"] = "1.0",
                ["type"] = "event-type",
                ["id"] = "event-id",
                ["source"] = "//event-source"
            };

        /// <summary>
        /// Parses JSON as a JObject with settings that prevent any additional conversions.
        /// </summary>
        internal static JObject ParseJson(byte[] data) => ParseJsonImpl<JObject>(data);

        internal static JObject ParseJson(ReadOnlyMemory<byte> data) => ParseJsonImpl<JObject>(data);

        /// <summary>
        /// Parses JSON as a JArray with settings that prevent any additional conversions.
        /// </summary>
        internal static JArray ParseJsonArray(ReadOnlyMemory<byte> data) => ParseJsonImpl<JArray>(data);

        private static T ParseJsonImpl<T>(ReadOnlyMemory<byte> data)
        {
            string text = BinaryDataUtilities.GetString(data, Encoding.UTF8);
            var serializer = new JsonSerializer
            {
                DateParseHandling = DateParseHandling.None                
            };
            return serializer.Deserialize<T>(new JsonTextReader(new StringReader(text)));                
        }


        /// <summary>
        /// Convenience method to format a CloudEvent with the default JsonEventFormatter in
        /// structured mode, then parse the result as a JObject.
        /// </summary>
        private static JObject EncodeAndParseStructured(CloudEvent cloudEvent)
        {
            var formatter = new JsonEventFormatter();
            var encoded = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
            return ParseJson(encoded);
        }

        /// <summary>
        /// Convenience method to serialize a JObject to bytes, then
        /// decode it as a structured event with the default JsonEventFormatter and no extension attributes.
        /// </summary>
        private static CloudEvent DecodeStructuredModeMessage(JObject obj)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(obj.ToString());
            var formatter = new JsonEventFormatter();
            return formatter.DecodeStructuredModeMessage(bytes, s_jsonCloudEventContentType, null);
        }

        /// <summary>
        /// Convenience method to serialize a JArray to bytes, then
        /// decode it as a batch mode message with the default JsonEventFormatter and no extension attributes.
        /// </summary>
        private static IReadOnlyList<CloudEvent> DecodeBatchModeMessage(JArray array)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(array.ToString());
            var formatter = new JsonEventFormatter();
            return formatter.DecodeBatchModeMessage(bytes, s_jsonCloudEventContentType, null);
        }
    }
}