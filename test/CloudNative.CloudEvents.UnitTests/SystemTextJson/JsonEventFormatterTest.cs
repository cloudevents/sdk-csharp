// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.UnitTests;
using System;
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
using JArray = Newtonsoft.Json.Linq.JArray;
using CloudNative.CloudEvents.Core;

namespace CloudNative.CloudEvents.SystemTextJson.UnitTests
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
        public void EncodeStructuredModeMessage_JsonDataType_NumberSerialization()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = 10;
            cloudEvent.DataContentType = "application/json";
            JsonElement element = EncodeAndParseStructured(cloudEvent);
            var asserter = new JsonElementAsserter
            {
                { "data", JsonValueKind.Number, 10 }
            };
            asserter.AssertProperties(element, assertCount: false);
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
            var encoded = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
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
        public void EncodeStructuredModeMessage_JsonDataType_JsonElementObject()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = ParseJson("{ \"value\": { \"Key\": \"value\" } }").GetProperty("value");
            cloudEvent.DataContentType = "application/json";
            JsonElement element = EncodeAndParseStructured(cloudEvent);
            JsonElement data = element.GetProperty("data");
            var asserter = new JsonElementAsserter
            {
                { "Key", JsonValueKind.String, "value" }
            };
            asserter.AssertProperties(data, assertCount: true);
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_JsonElementString()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = ParseJson("{ \"value\": \"text\" }").GetProperty("value");
            cloudEvent.DataContentType = "application/json";
            JsonElement element = EncodeAndParseStructured(cloudEvent);
            JsonElement data = element.GetProperty("data");
            Assert.Equal(JsonValueKind.String, data.ValueKind);
            Assert.Equal("text", data.GetString());
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_JsonElementNull()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = ParseJson("{ \"value\": null }").GetProperty("value");
            cloudEvent.DataContentType = "application/json";
            JsonElement element = EncodeAndParseStructured(cloudEvent);
            JsonElement data = element.GetProperty("data");
            Assert.Equal(JsonValueKind.Null, data.ValueKind);
        }

        [Fact]
        public void EncodeStructuredModeMessage_JsonDataType_JsonElementNumeric()
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
            var bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
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
            var bytes = formatter.EncodeBinaryModeEventData(cloudEvent);
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
            var bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
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
            var bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            Assert.Equal("\"some text\"", BinaryDataUtilities.GetString(bytes, Encoding.GetEncoding(charset)));
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

        [Theory]
        [InlineData("utf-8")]
        [InlineData("iso-8859-1")]
        public void EncodeBinaryModeEventData_TextType_String(string charset)
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = "some text";
            cloudEvent.DataContentType = $"text/anything; charset={charset}";
            var bytes = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            Assert.Equal("some text", BinaryDataUtilities.GetString(bytes, Encoding.GetEncoding(charset)));
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

        [Fact]
        public void EncodeBinaryModeEventData_NoContentType_ConvertsStringToJson()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "some text"
            }.PopulateRequiredAttributes();

            // EncodeBinaryModeEventData doesn't actually populate the content type of the CloudEvent,
            // but treat the data as if we'd explicitly specified application/json.
            var data = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            string text = BinaryDataUtilities.GetString(data, Encoding.UTF8);
            Assert.Equal("\"some text\"", text);
        }

        [Fact]
        public void EncodeBinaryModeEventData_NoContentType_LeavesBinaryData()
        {
            var cloudEvent = new CloudEvent
            {
                Data = SampleBinaryData
            }.PopulateRequiredAttributes();

            // EncodeBinaryModeEventData does *not* implicitly encode binary data as JSON.
            var data = new JsonEventFormatter().EncodeBinaryModeEventData(cloudEvent);
            var array = BinaryDataUtilities.AsArray(data);
            Assert.Equal(array, SampleBinaryData);
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
            var array = ParseJson(bytes);
            Assert.Equal(JsonValueKind.Array, array.ValueKind);
            Assert.Equal(0, array.GetArrayLength());
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
            var array = ParseJson(bytes).EnumerateArray().ToList();
            Assert.Equal(2, array.Count);
            
            var asserter1 = new JsonElementAsserter
            {
                { "specversion", JsonValueKind.String, "1.0" },
                { "id", JsonValueKind.String, event1.Id },
                { "type", JsonValueKind.String, event1.Type },
                { "source", JsonValueKind.String, "//test" },
                { "data", JsonValueKind.String, "simple text" },
                { "datacontenttype", JsonValueKind.String, event1.DataContentType }
            };
            asserter1.AssertProperties(array[0], assertCount: true);

            var asserter2 = new JsonElementAsserter
            {
                { "specversion", JsonValueKind.String, "1.0" },
                { "id", JsonValueKind.String, event2.Id },
                { "type", JsonValueKind.String, event2.Type },
                { "source", JsonValueKind.String, "//test" },
            };
            asserter2.AssertProperties(array[1], assertCount: true);
        }

        [Fact]
        public void EncodeBatchModeMessage_Invalid()
        {
            var formatter = new JsonEventFormatter();
            // Invalid CloudEvent
            Assert.Throws<ArgumentException>(() => formatter.EncodeBatchModeMessage(new[] { new CloudEvent() }, out _));
            // Null argument
            Assert.Throws<ArgumentNullException>(() => formatter.EncodeBatchModeMessage(null!, out _));
            // Null value within the argument. Arguably this should throw ArgumentException instead of
            // ArgumentNullException, but it's unlikely to cause confusion.
            Assert.Throws<ArgumentNullException>(() => formatter.EncodeBatchModeMessage(new CloudEvent[1], out _));
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
            var bytes = Encoding.GetEncoding(charset).GetBytes(obj.ToString());
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
            var bytes = Encoding.GetEncoding(charset).GetBytes(obj.ToString());
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

            var bytes = Encoding.UTF8.GetBytes(obj.ToString());
            var formatter = new JsonEventFormatter();
            var cloudEvent = formatter.DecodeStructuredModeMessage(bytes, s_jsonCloudEventContentType, AllTypesExtensions);
            Assert.Equal(SampleBinaryData, cloudEvent["binary"]);
            Assert.True((bool)cloudEvent["boolean"]!);
            Assert.Equal(10, cloudEvent["integer"]);
            Assert.Equal("text", cloudEvent["string"]);
            AssertTimestampsEqual(SampleTimestamp, (DateTimeOffset)cloudEvent["timestamp"]!);
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
            var bytes = Encoding.UTF8.GetBytes(obj.ToString());
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

        [Theory]
        [InlineData("text/plain")]
        [InlineData("image/png")]
        public void DecodeStructuredModeMessage_NonJsonContentType_JsonStringToken(string contentType)
        {
            var obj = CreateMinimalValidJObject();
            obj["datacontenttype"] = contentType;
            obj["data"] = "some text";
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal("some text", cloudEvent.Data);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("application/json")]
        [InlineData("application/json; charset=utf-8")]
        public void DecodeStructuredModeMessage_JsonContentType_JsonStringToken(string contentType)
        {
            var obj = CreateMinimalValidJObject();
            if (contentType is object)
            {
                obj["datacontenttype"] = contentType;
            }
            obj["data"] = "text";
            var cloudEvent = DecodeStructuredModeMessage(obj);
            var element = (JsonElement) cloudEvent.Data!;
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.Equal("text", element.GetString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("application/json")]
        [InlineData("application/xyz+json")]
        [InlineData("application/xyz+json; charset=utf-8")]
        public void DecodeStructuredModeMessage_JsonContentType_NonStringValue(string contentType)
        {
            var obj = CreateMinimalValidJObject();
            if (contentType is object)
            {
                obj["datacontenttype"] = contentType;
            }
            obj["data"] = 10;
            var cloudEvent = DecodeStructuredModeMessage(obj);
            var element = (JsonElement) cloudEvent.Data!;
            Assert.Equal(JsonValueKind.Number, element.ValueKind);
            Assert.Equal(10, element.GetInt32());
        }

        [Fact]
        public void DecodeStructuredModeMessage_NonJsonContentType_NonStringValue()
        {
            var obj = CreateMinimalValidJObject();
            obj["datacontenttype"] = "text/plain";
            obj["data"] = 10;
            Assert.Throws<ArgumentException>(() => DecodeStructuredModeMessage(obj));
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
            Assert.Equal(bytes, data);
        }

        [Fact]
        public void DecodeBatchMode_NotArray()
        {
            var formatter = new JsonEventFormatter();
            var data = Encoding.UTF8.GetBytes(CreateMinimalValidJObject().ToString());
            Assert.Throws<ArgumentException>(() => formatter.DecodeBatchModeMessage(data, s_jsonCloudEventBatchContentType, extensionAttributes: null));
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

        [Fact]
        public void DecodeBatchMode_Minimal_WithStream()
        {
            var array = new JArray { CreateMinimalValidJObject() };
            var bytes = Encoding.UTF8.GetBytes(array.ToString());
            var formatter = new JsonEventFormatter();
            var cloudEvents = formatter.DecodeBatchModeMessage(new MemoryStream(bytes), s_jsonCloudEventBatchContentType, null);
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
            var bytes = Encoding.UTF8.GetBytes(new JArray { obj }.ToString());
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

        // Additional tests for the changes/clarifications in https://github.com/cloudevents/spec/pull/861
        [Fact]
        public void EncodeStructured_DefaultContentTypeToApplicationJson()
        {
            var cloudEvent = new CloudEvent
            {
                Data = new { Key = "value" }
            }.PopulateRequiredAttributes();

            var encoded = new JsonEventFormatter().EncodeStructuredModeMessage(cloudEvent, out var contentType);
            Assert.Equal("application/cloudevents+json; charset=utf-8", contentType.ToString());
            JsonElement obj = ParseJson(encoded);
            var asserter = new JsonElementAsserter
            {
                { "data", JsonValueKind.Object, cloudEvent.Data },
                { "datacontenttype", JsonValueKind.String, "application/json" },
                { "id", JsonValueKind.String, "test-id" },
                { "source", JsonValueKind.String, "//test" },
                { "specversion", JsonValueKind.String, "1.0" },
                { "type", JsonValueKind.String, "test-type" },
            };
            asserter.AssertProperties(obj, assertCount: true);
        }

        [Fact]
        public void EncodeStructured_BinaryData_DefaultContentTypeIsNotImplied()
        {
            var cloudEvent = new CloudEvent
            {
                Data = SampleBinaryData
            }.PopulateRequiredAttributes();

            // If a CloudEvent to have binary data but no data content type,
            // the spec says the data should be placed in data_base64, but the content type
            // should *not* be defaulted to application/json, as clarified in https://github.com/cloudevents/spec/issues/933
            var encoded = new JsonEventFormatter().EncodeStructuredModeMessage(cloudEvent, out var contentType);
            Assert.Equal("application/cloudevents+json; charset=utf-8", contentType.ToString());
            JsonElement obj = ParseJson(encoded);
            var asserter = new JsonElementAsserter
            {
                { "data_base64", JsonValueKind.String, SampleBinaryDataBase64 },
                { "id", JsonValueKind.String, "test-id" },
                { "source", JsonValueKind.String, "//test" },
                { "specversion", JsonValueKind.String, "1.0" },
                { "type", JsonValueKind.String, "test-type" },
            };
            asserter.AssertProperties(obj, assertCount: true);
        }

        [Fact]
        public void DecodeStructured_DefaultContentTypeToApplicationJson()
        {
            var obj = new JObject
            {
                ["specversion"] = "1.0",
                ["type"] = "test-type",
                ["id"] = "test-id",
                ["source"] = SampleUriText,
                ["data"] = "some text"
            };
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal("application/json", cloudEvent.DataContentType);
            var jsonData = Assert.IsType<JsonElement>(cloudEvent.Data);
            Assert.Equal(JsonValueKind.String, jsonData.ValueKind);
            Assert.Equal("some text", jsonData.GetString());
        }

        // Utility methods
        private static object DecodeBinaryModeEventData(byte[] bytes, string contentType)
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.DataContentType = contentType;
            new JsonEventFormatter().DecodeBinaryModeEventData(bytes, cloudEvent);
            return cloudEvent.Data!;
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
        internal static JsonElement ParseJson(ReadOnlyMemory<byte> data)
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
            var encoded = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
            return ParseJson(encoded);
        }

        /// <summary>
        /// Convenience method to serialize a JObject to bytes, then
        /// decode it as a structured event with the default (System.Text.Json) JsonEventFormatter and no extension attributes.
        /// </summary>
        private static CloudEvent DecodeStructuredModeMessage(Newtonsoft.Json.Linq.JObject obj)
        {
            var bytes = Encoding.UTF8.GetBytes(obj.ToString());
            var formatter = new JsonEventFormatter();
            return formatter.DecodeStructuredModeMessage(bytes, s_jsonCloudEventContentType, null);
        }

        /// <summary>
        /// Convenience method to serialize a JArray to bytes, then
        /// decode it as a structured event with the default (System.Text.Json) JsonEventFormatter and no extension attributes.
        /// </summary>
        private static IReadOnlyList<CloudEvent> DecodeBatchModeMessage(Newtonsoft.Json.Linq.JArray array)
        {
            var bytes = Encoding.UTF8.GetBytes(array.ToString());
            var formatter = new JsonEventFormatter();
            return formatter.DecodeBatchModeMessage(bytes, s_jsonCloudEventBatchContentType, null);
        }

        private class YearMonthDayConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                DateTime.ParseExact(reader.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
                writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }
}