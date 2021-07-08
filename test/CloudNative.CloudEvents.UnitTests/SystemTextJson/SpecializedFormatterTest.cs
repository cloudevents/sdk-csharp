// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.UnitTests;
using System;
using System.Text;
using System.Text.Json;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;
// JObject is a really handy way of creating JSON which we can then parse with System.Text.Json
using JObject = Newtonsoft.Json.Linq.JObject;

namespace CloudNative.CloudEvents.SystemTextJson.UnitTests
{
    /// <summary>
    /// Tests for encoding/decoding using a subclass of <see cref="JsonEventFormatter"/>.
    /// This is effectively testing when the virtual methods are invoked.
    /// </summary>
    public class SpecializedFormatterTest
    {
        private const string GuidPrefix = "guid:";
        private const string TextBinaryContentType = "text/binary";
        private const string GuidContentType = "application/guid";

        // Just to validate delegation to base methods
        [Fact]
        public void EncodePlainText()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "some text",
                DataContentType = "text/plain"
            }.PopulateRequiredAttributes();
            var obj = EncodeAndParseStructured(cloudEvent);
            AssertStringElement("some text", obj.GetProperty("data"));
        }

        // Just to validate delegation to base methods
        [Fact]
        public void EncodeByteArray()
        {
            var cloudEvent = new CloudEvent
            {
                Data = SampleBinaryData,
                DataContentType = "application/binary"
            }.PopulateRequiredAttributes();
            var obj = EncodeAndParseStructured(cloudEvent);
            AssertStringElement(SampleBinaryDataBase64, obj.GetProperty("data_base64"));
        }

        [Fact]
        public void EncodeGuid()
        {
            Guid guid = Guid.NewGuid();
            var cloudEvent = new CloudEvent
            {
                Data = guid,
                DataContentType = GuidContentType
            }.PopulateRequiredAttributes();
            var obj = EncodeAndParseStructured(cloudEvent);
            string expectedText = GuidPrefix + Convert.ToBase64String(guid.ToByteArray());
            AssertStringElement(expectedText, obj.GetProperty("data"));
        }

        [Fact]
        public void EncodeTextBinary()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "some text",
                DataContentType = TextBinaryContentType
            }.PopulateRequiredAttributes();
            var obj = EncodeAndParseStructured(cloudEvent);
            string expectedText = Convert.ToBase64String(Encoding.UTF8.GetBytes("some text"));
            AssertStringElement(expectedText, obj.GetProperty("data_base64"));
        }

        // Just to validate delegation to base methods
        [Fact]
        public void DecodePlainText()
        {
            var obj = JsonEventFormatterTest.CreateMinimalValidJObject();
            obj["data"] = "some text";
            obj["datacontenttype"] = "text/plain";
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal("some text", cloudEvent.Data);
        }

        // Just to validate delegation to base methods
        [Fact]
        public void DecodeByteArray()
        {
            var obj = JsonEventFormatterTest.CreateMinimalValidJObject();
            obj["data_base64"] = SampleBinaryDataBase64;
            obj["datacontenttype"] = "application/binary";
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal(SampleBinaryData, cloudEvent.Data);
        }

        [Fact]
        public void DecodeGuid()
        {
            Guid guid = Guid.NewGuid();
            var obj = JsonEventFormatterTest.CreateMinimalValidJObject();
            obj["data"] = GuidPrefix + Convert.ToBase64String(guid.ToByteArray());
            obj["datacontenttype"] = GuidContentType;
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal(guid, cloudEvent.Data);
        }

        [Fact]
        public void DecodeTextBinary()
        {
            var obj = JsonEventFormatterTest.CreateMinimalValidJObject();
            obj["data_base64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("some text"));
            obj["datacontenttype"] = TextBinaryContentType;
            var cloudEvent = DecodeStructuredModeMessage(obj);
            Assert.Equal("some text", cloudEvent.Data);
        }

        private static void AssertStringElement(string expectedValue, JsonElement element)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.Equal(expectedValue, element.GetString());
        }

        /// <summary>
        /// Convenience method to format a CloudEvent with a specialized formatter in
        /// structured mode, then parse the result as a JObject.
        /// </summary>
        private static JsonElement EncodeAndParseStructured(CloudEvent cloudEvent)
        {
            var formatter = new SpecializedFormatter();
            var encoded = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
            return JsonEventFormatterTest.ParseJson(encoded);
        }

        /// <summary>
        /// Convenience method to serialize a JObject to bytes, then
        /// decode it as a structured event with a specialized formatter and no extension attributes.
        /// </summary>
        private static CloudEvent DecodeStructuredModeMessage(JObject obj)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(obj.ToString());
            var formatter = new SpecializedFormatter();
            return formatter.DecodeStructuredModeMessage(bytes, null, null);
        }

        /// <summary>
        /// Specialized formatter:
        /// - Content type of "text/binary" is encoded in base64
        /// - Guid with a content type of "application/guid" is encoded as a string with content "guid:base64-data"
        /// </summary>
        private class SpecializedFormatter : JsonEventFormatter
        {
            protected override void DecodeStructuredModeDataBase64Property(JsonElement dataBase64Element, CloudEvent cloudEvent)
            {
                if (cloudEvent.DataContentType == TextBinaryContentType && dataBase64Element.ValueKind == JsonValueKind.String)
                {
                    cloudEvent.Data = Encoding.UTF8.GetString(dataBase64Element.GetBytesFromBase64());
                }
                else
                {
                    base.DecodeStructuredModeDataBase64Property(dataBase64Element, cloudEvent);
                }
            }

            protected override void DecodeStructuredModeDataProperty(JsonElement dataElement, CloudEvent cloudEvent)
            {
                if (cloudEvent.DataContentType == GuidContentType && dataElement.ValueKind == JsonValueKind.String)
                {
                    string text = dataElement.GetString()!;
                    if (!text.StartsWith(GuidPrefix))
                    {
                        throw new ArgumentException("Invalid GUID text data");
                    }
                    cloudEvent.Data = new Guid(Convert.FromBase64String(text.Substring(GuidPrefix.Length)));
                }
                else
                {
                    base.DecodeStructuredModeDataProperty(dataElement, cloudEvent);
                }
            }

            protected override void EncodeStructuredModeData(CloudEvent cloudEvent, Utf8JsonWriter writer)
            {
                var data = cloudEvent.Data;
                if (data is Guid guid && cloudEvent.DataContentType == GuidContentType)
                {
                    writer.WritePropertyName(DataPropertyName);
                    writer.WriteStringValue(GuidPrefix + Convert.ToBase64String(guid.ToByteArray()));
                }
                else if (data is string text && cloudEvent.DataContentType == TextBinaryContentType)
                {
                    writer.WritePropertyName(DataBase64PropertyName);
                    writer.WriteBase64StringValue(Encoding.UTF8.GetBytes(text));
                }
                else
                {
                    base.EncodeStructuredModeData(cloudEvent, writer);
                }
            }
        }
    }
}
