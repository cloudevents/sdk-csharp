// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.UnitTests;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Mime;
using System.Text;
using Xunit;

namespace CloudNative.CloudEvents.NewtonsoftJson.UnitTests
{
    /// <summary>
    /// Tests for <see cref="JsonEventFormatter{T}"/>
    /// </summary>
    public class GenericJsonEventFormatterTest
    {
        [Fact]
        public void DecodeStructuredMode()
        {
            var obj = JsonEventFormatterTest.CreateMinimalValidJObject();
            obj["data"] = new JObject { [AttributedModel.JsonPropertyName] = "test" };
            byte[] bytes = Encoding.UTF8.GetBytes(obj.ToString());

            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            var cloudEvent = formatter.DecodeStructuredModeMessage(bytes, null, null);

            var model = (AttributedModel) cloudEvent.Data;
            Assert.Equal("test", model.AttributedProperty);
        }

        [Fact]
        public void DecodeStructuredMode_ContentTypeIgnored()
        {
            var obj = JsonEventFormatterTest.CreateMinimalValidJObject();
            obj["data"] = new JObject { [AttributedModel.JsonPropertyName] = "test" };
            byte[] bytes = Encoding.UTF8.GetBytes(obj.ToString());

            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            var cloudEvent = formatter.DecodeStructuredModeMessage(bytes, new ContentType("text/plain"), null);

            var model = (AttributedModel)cloudEvent.Data;
            Assert.Equal("test", model.AttributedProperty);
        }

        [Fact]
        public void DecodeStructuredMode_NoData()
        {
            var obj = JsonEventFormatterTest.CreateMinimalValidJObject();
            byte[] bytes = Encoding.UTF8.GetBytes(obj.ToString());

            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            var cloudEvent = formatter.DecodeStructuredModeMessage(bytes, null, null);
            Assert.Null(cloudEvent.Data);
        }

        [Fact]
        public void DecodeStructuredMode_Base64Data()
        {
            var obj = JsonEventFormatterTest.CreateMinimalValidJObject();
            obj["data_base64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"));
            byte[] bytes = Encoding.UTF8.GetBytes(obj.ToString());

            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            Assert.Throws<ArgumentException>(() => formatter.DecodeStructuredModeMessage(bytes, null, null));
        }

        [Fact]
        public void DecodeBinaryEventModeData()
        {
            var obj = new JObject { [AttributedModel.JsonPropertyName] = "test" };
            byte[] bytes = Encoding.UTF8.GetBytes(obj.ToString());

            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            var cloudEvent = new CloudEvent();
            formatter.DecodeBinaryModeEventData(bytes, cloudEvent);

            var model = (AttributedModel)cloudEvent.Data;
            Assert.Equal("test", model.AttributedProperty);
        }

        [Fact]
        public void DecodeBinaryEventModeData_NoData()
        {
            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            var cloudEvent = new CloudEvent { Data = "original" };
            formatter.DecodeBinaryModeEventData(new byte[0], cloudEvent);
            Assert.Null(cloudEvent.Data);
        }

        [Fact]
        public void EncodeStructuredMode()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new AttributedModel { AttributedProperty = "test" };
            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            var body = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
            var jobject = JsonEventFormatterTest.ParseJson(body);
            Assert.False(jobject.ContainsKey("data_base64"));
            var data = (JObject)jobject["data"];

            new JTokenAsserter
            {
                { AttributedModel.JsonPropertyName, JTokenType.String, "test" }
            }.AssertProperties(data, assertCount: true);
        }

        [Fact]
        public void EncodeStructuredMode_NoData()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();

            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            var body = formatter.EncodeStructuredModeMessage(cloudEvent, out _);
            var jobject = JsonEventFormatterTest.ParseJson(body);
            Assert.False(jobject.ContainsKey("data"));
            Assert.False(jobject.ContainsKey("data_base64"));
        }

        [Fact]
        public void EncodeStructuredMode_WrongType()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new OtherModelClass { Text = "Wrong type" };
            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            Assert.Throws<InvalidCastException>(() => formatter.EncodeStructuredModeMessage(cloudEvent, out _));
        }

        [Fact]
        public void EncodeBinaryModeEventData()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new AttributedModel { AttributedProperty = "test" };
            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            var body = formatter.EncodeBinaryModeEventData(cloudEvent);
            var jobject = JsonEventFormatterTest.ParseJson(body);

            new JTokenAsserter
            {
                { AttributedModel.JsonPropertyName, JTokenType.String, "test" }
            }.AssertProperties(jobject, assertCount: true);
        }

        [Fact]
        public void EncodeBinaryModeEventData_NoData()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            Assert.Empty(formatter.EncodeBinaryModeEventData(cloudEvent));
        }

        [Fact]
        public void EncodeBinaryModeEventData_WrongType()
        {
            var cloudEvent = new CloudEvent().PopulateRequiredAttributes();
            cloudEvent.Data = new OtherModelClass { Text = "Wrong type" };
            var formatter = CloudEventFormatterAttribute.CreateFormatter(typeof(AttributedModel));
            Assert.Throws<InvalidCastException>(() => formatter.EncodeBinaryModeEventData(cloudEvent));
        }

        private class OtherModelClass
        {
            public string Text { get; set; }
        }
    }
}
