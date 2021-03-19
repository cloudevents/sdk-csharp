// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.UnitTests;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;
// JObject is a really handy way of creating JSON which we can then parse with System.Text.Json
using JObject = Newtonsoft.Json.Linq.JObject;

namespace CloudNative.CloudEvents.SystemTextJson.UnitTests
{
    public class JsonEventFormatterTest
    {
        private static readonly ContentType s_jsonCloudEventContentType = new ContentType("application/cloudevents+json; charset=utf-8");
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

            byte[] encoded = new JsonEventFormatter().EncodeStructuredModeMessage(cloudEvent, out var contentType);
            Assert.Equal("application/cloudevents+json; charset=utf-8", contentType.ToString());
            JsonElement obj = ParseJson(encoded);
            var asserter = new JsonElementAsserter
            {
                { "data", JsonValueKind.String, "text" },
                { "datacontenttype", JsonValueKind.String, "text/plain" },
                { "dataschema", JsonValueKind.String, "https://data-schema" },
                { "id", JsonValueKind.String, "event-id" },
                { "source", JsonValueKind.String, "https://event-source" },
                { "specversion", JsonValueKind.String, "1.0" },
                { "subject", JsonValueKind.String, "event-subject" },
                { "time", JsonValueKind.String, "2021-02-19T12:34:56.789+01:00" },
                { "type", JsonValueKind.String, "event-type" },
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

            JsonElement element = EncodeAndParseStructured(cloudEvent);
            var asserter = new JsonElementAsserter
            {
                { "binary", JsonValueKind.String, SampleBinaryDataBase64 },
                { "boolean", JsonValueKind.True, true },
                { "integer", JsonValueKind.Number, 10 },
                { "string", JsonValueKind.String, "text" },
                { "timestamp", JsonValueKind.String, SampleTimestampText },
                { "uri", JsonValueKind.String, SampleUriText },
                { "urireference", JsonValueKind.String, SampleUriReferenceText },
            };
            asserter.AssertProperties(element, assertCount: false);
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_ObjectSerialization()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new { Text = "simple text" };
            cloudEvent.DataContentType = "application/json";
            JsonElement element = EncodeAndParseStructured(cloudEvent);
            JsonElement dataProperty = element.GetProperty("data");
            var asserter = new JsonElementAsserter
            {
                { "Text", JsonValueKind.String, "simple text" }
            };
            asserter.AssertProperties(dataProperty, assertCount: true);
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_CustomSerializerOptions()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new { DateValue = new DateTime(2021, 2, 19, 12, 49, 34, DateTimeKind.Utc) };
            cloudEvent.DataContentType = "application/json";

            var serializerOptions = new JsonSerializerOptions
            {
                Converters = { new YearMonthDayConverter() },
            };
            var formatter = new JsonEventFormatter(serializerOptions, default);
            byte[] encoded = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
            JsonElement element = ParseJson(encoded);
            JsonElement dataProperty = element.GetProperty("data");
            var asserter = new JsonElementAsserter
            {
                { "DateValue", JsonValueKind.String, "2021-02-19" }
            };
            asserter.AssertProperties(dataProperty, assertCount: true);
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_AttributedModel()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new AttributedModel { AttributedProperty = "simple text" };
            cloudEvent.DataContentType = "application/json";
            JsonElement element = EncodeAndParseStructured(cloudEvent);
            JsonElement dataProperty = element.GetProperty("data");
            var asserter = new JsonElementAsserter
            {
                { AttributedModel.JsonPropertyName, JsonValueKind.String, "simple text" }
            };
            asserter.AssertProperties(dataProperty, assertCount: true);
        }
        
        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_JsonElement()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = ParseJson("{ \"value\": 100 }").GetProperty("value");
            cloudEvent.DataContentType = "application/json";
            JsonElement element = EncodeAndParseStructured(cloudEvent);
            JsonElement data = element.GetProperty("data");
            Assert.Equal(JsonValueKind.Number, data.ValueKind);
            Assert.Equal(100, data.GetInt32());
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_NullValue()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = null;
            cloudEvent.DataContentType = "application/json";
            JsonElement element = EncodeAndParseStructured(cloudEvent);
            Assert.False(element.TryGetProperty("data", out _));
        }

        [Fact]
        public void EncodeStructuredModeMessage_TextType_String()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = "some text";
            cloudEvent.DataContentType = "text/anything";
            JsonElement element = EncodeAndParseStructured(cloudEvent);
            var dataProperty = element.GetProperty("data");
            Assert.Equal(JsonValueKind.String, dataProperty.ValueKind);
            Assert.Equal("some text", dataProperty.GetString());
        }

        // A text content type with bytes as data is serialized like any other bytes.
        [Fact]
        public void EncodeStructuredModeMessage_TextType_Bytes()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = SampleBinaryData;
            cloudEvent.DataContentType = "text/anything";
            JsonElement element = EncodeAndParseStructured(cloudEvent);
            Assert.False(element.TryGetProperty("data", out _));
            var dataBase64 = element.GetProperty("data_base64");
            Assert.Equal(JsonValueKind.String, dataBase64.ValueKind);
            Assert.Equal(SampleBinaryDataBase64, dataBase64.GetString());
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
            JsonElement element = EncodeAndParseStructured(cloudEvent);
            Assert.False(element.TryGetProperty("data", out _));
            var dataBase64 = element.GetProperty("data_base64");
            Assert.Equal(JsonValueKind.String, dataBase64.ValueKind);
            Assert.Equal(SampleBinaryDataBase64, dataBase64.GetString());
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
            byte[] bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            JsonElement data = ParseJson(bytes);
            var asserter = new JsonElementAsserter
            {
                { "Text", JsonValueKind.String, "simple text" }
            };
            asserter.AssertProperties(data, assertCount: true);
        }

        [Fact]
        public void EncodeBinaryModeEventData_JsonDataType_CustomSerializer()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new { DateValue = new DateTime(2021, 2, 19, 12, 49, 34, DateTimeKind.Utc) };
            cloudEvent.DataContentType = "application/json";

            var serializerOptions = new JsonSerializerOptions
            {
                Converters = { new YearMonthDayConverter() },
            };
            var formatter = new JsonEventFormatter(serializerOptions, default);
            byte[] bytes = formatter.EncodeBinaryModeEventData(cloudEvent);
            JsonElement data = ParseJson(bytes);
            var asserter = new JsonElementAsserter
            {
                { "DateValue", JsonValueKind.String, "2021-02-19" }
            };
            asserter.AssertProperties(data, assertCount: true);
        }

        [Fact]
        public void EncodeBinaryModeEventData_JsonDataType_AttributedModel()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new AttributedModel { AttributedProperty = "simple text" };
            cloudEvent.DataContentType = "application/json";
            byte[] bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            JsonElement data = ParseJson(bytes);
            var asserter = new JsonElementAsserter
            {
                { AttributedModel.JsonPropertyName, JsonValueKind.String, "simple text" }
            };
            asserter.AssertProperties(data, assertCount: true);
        }

        [Theory]
        [InlineData("utf-8")]
        [InlineData("utf-16")]
        public void EncodeBinaryModeEventData_JsonDataType_JsonElement(string charset)
        {
            // This would definitely be an odd thing to do, admittedly...
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = ParseJson($"{{ \"value\": \"some text\" }}").GetProperty("value");
            cloudEvent.DataContentType = $"application/json; charset={charset}";
            byte[] bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            Assert.Equal("\"some text\"", Encoding.GetEncoding(charset).GetString(bytes));
        }

        [Fact]
        public void EncodeBinaryModeEventData_JsonDataType_NullValue()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = null;
            cloudEvent.DataContentType = "application/json";
            byte[] bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            Assert.Empty(bytes);
        }

        [Theory]
        [InlineData("utf-8")]
        [InlineData("iso-8859-1")]
        public void EncodeBinaryModeEventData_TextType_String(string charset)
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = "some text";
            cloudEvent.DataContentType = $"text/anything; charset={charset}";
            byte[] bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            Assert.Equal("some text", Encoding.GetEncoding(charset).GetString(bytes));
        }

        // A text content type with bytes as data is serialized like any other bytes.
        [Fact]
        public void EncodeBinaryModeEventData_TextType_Bytes()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = SampleBinaryData;
            cloudEvent.DataContentType = "text/anything";
            byte[] bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
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
            byte[] bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
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

        [Fact]
        public void DecodeStructuredModeMessage_NotJson()
        {
            var formatter = new JsonEventFormatter();
            Assert.ThrowsAny<JsonException>(() => formatter.DecodeStructuredModeMessage(new byte[10], new ContentType("application/json"), null));
        }

        // Just a single test for the code that parses asynchronously... the guts are all the same.
        [Theory]
        [InlineData("utf-8")]
        [InlineData("iso-8859-1")]
        public async Task DecodeStructuredModeMessageAsync_Minimal(string charset)
        {
            // Note: just using Json.NET to get the JSON in a simple way...
            var obj = new JObject
            {
                ["specversion"] = "1.0",
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["source"] = SampleUriText,
                ["text"] = NonAsciiValue
            };
            byte[] bytes = Encoding.GetEncoding(charset).GetBytes(obj.ToString());
            var stream = new MemoryStream(bytes);
            var formatter = new JsonEventFormatter();
            var cloudEvent = await formatter.DecodeStructuredModeMessageAsync(stream, new ContentType($"application/cloudevents+json; charset={charset}"), null);
            Assert.Equal("test-type", cloudEvent.Type);
            Assert.Equal("test-id", cloudEvent.Id);
            Assert.Equal(SampleUri, cloudEvent.Source);
            Assert.Equal(NonAsciiValue, cloudEvent["text"]);
        }

        [Theory]
        [InlineData("utf-8")]
        [InlineData("iso-8859-1")]
        public void DecodeStructuredModeMessage_Minimal(string charset)
        {
            var obj = new JObject
            {
                ["specversion"] = "1.0",
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["source"] = SampleUriText,
                ["text"] = NonAsciiValue
            };
            byte[] bytes = Encoding.GetEncoding(charset).GetBytes(obj.ToString());
            var stream = new MemoryStream(bytes);
            var formatter = new JsonEventFormatter();
            var cloudEvent = formatter.DecodeStructuredModeMessage(stream, new ContentType($"application/cloudevents+json; charset={charset}"), null);
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
            Assert.True((bool)cloudEvent["boolean"]);
            Assert.Equal(10, cloudEvent["integer"]);
            Assert.Equal("text", cloudEvent["string"]);
            AssertTimestampsEqual(SampleTimestamp, (DateTimeOffset)cloudEvent["timestamp"]);
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
            obj["attr"] = new Newtonsoft.Json.Linq.JArray();
            Assert.Throws<ArgumentException>(() => DecodeStructuredModeMessage(obj));
        }

        [Fact]
        public void DecodeStructuredModeMessage_Null()
        {
            var obj = CreateMinimalValidJObject();
            obj["attr"] = Newtonsoft.Json.Linq.JValue.CreateNull();
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
            var element = (JsonElement) cloudEvent.Data;
            Assert.Equal(JsonValueKind.Number, element.ValueKind);
            Assert.Equal(10, element.GetInt32());
        }

        [Fact]
        public void DecodeStructuredModeMessage_NullDataBase64Ignored()
        {
            var obj = CreateMinimalValidJObject();
            obj["data_base64"] = Newtonsoft.Json.Linq.JValue.CreateNull();
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
            obj["data"] = Newtonsoft.Json.Linq.JValue.CreateNull();
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
        [InlineData("utf-16")]
        public void DecodeBinaryModeEventData_Json(string charset)
        {
            var encoding = Encoding.GetEncoding(charset);
            var bytes = encoding.GetBytes(new JObject { ["test"] = "some text" }.ToString());
            var data = DecodeBinaryModeEventData(bytes, $"application/json; charset={charset}");
            var element = Assert.IsType<JsonElement>(data);
            var asserter = new JsonElementAsserter
            {
                { "test", JsonValueKind.String, "some text"}
            };
            asserter.AssertProperties(element, assertCount: true);
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
            Assert.Same(bytes, data);
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
        /// Parses JSON as a JsonElement.
        /// </summary>
        internal static JsonElement ParseJson(string text)
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }

        /// <summary>
        /// Parses JSON as a JsonElement.
        /// </summary>
        internal static JsonElement ParseJson(byte[] data)
        {
            using var document = JsonDocument.Parse(data);
            return document.RootElement.Clone();
        }

        /// <summary>
        /// Convenience method to format a CloudEvent with the default JsonEventFormatter in
        /// structured mode, then parse the result as a JObject.
        /// </summary>
        private static JsonElement EncodeAndParseStructured(CloudEvent cloudEvent)
        {
            var formatter = new JsonEventFormatter();
            byte[] encoded = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
            return ParseJson(encoded);
        }

        /// <summary>
        /// Convenience method to serialize a JObject to bytes, then
        /// decode it as a structured event with the default (System.Text.Json) JsonEventFormatter and no extension attributes.
        /// </summary>
        private static CloudEvent DecodeStructuredModeMessage(Newtonsoft.Json.Linq.JObject obj)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(obj.ToString());
            var formatter = new JsonEventFormatter();
            return formatter.DecodeStructuredModeMessage(bytes, s_jsonCloudEventContentType, null);
        }

        private class JsonElementAsserter : IEnumerable
        {
            private readonly List<(string name, JsonValueKind type, object value)> expectations = new List<(string, JsonValueKind, object)>();

            // Just for collection initializers
            public IEnumerator GetEnumerator() => throw new NotImplementedException();

            public void Add<T>(string name, JsonValueKind type, T value) =>
                expectations.Add((name, type, value));

            public void AssertProperties(JsonElement obj, bool assertCount)
            {
                foreach (var expectation in expectations)
                {
                    Assert.True(
                        obj.TryGetProperty(expectation.name, out var property),
                        $"Expected property '{expectation.name}' to be present");
                    Assert.Equal(expectation.type, property.ValueKind);
                    // No need to check null values, as they'll have a null token type.
                    if (expectation.value is object)
                    {
                        var value = property.ValueKind switch
                        {
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.String => property.GetString(),
                            JsonValueKind.Number => property.GetInt32(),
                            JsonValueKind.Null => (object) null,
                            _ => throw new Exception($"Unhandled value kind: {property.ValueKind}")
                        };

                        Assert.Equal(expectation.value, value);
                    }
                }
                if (assertCount)
                {
                    Assert.Equal(expectations.Count, obj.EnumerateObject().Count());
                }
            }
        }

        private class AttributedModel
        {
            public const string JsonPropertyName = "customattribute";

            [JsonPropertyName(JsonPropertyName)]
            public string AttributedProperty { get; set; }
        }

        private class YearMonthDayConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                DateTime.ParseExact(reader.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
                writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

    }
}