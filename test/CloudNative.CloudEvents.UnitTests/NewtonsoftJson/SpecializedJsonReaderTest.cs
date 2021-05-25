// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.UnitTests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace CloudNative.CloudEvents.NewtonsoftJson.UnitTests
{
    /// <summary>
    /// Tests for the specialization of <see cref="JsonEventFormatter.CreateJsonReader(System.IO.Stream, Encoding)"/>
    /// </summary>
    public class SpecializedJsonReaderTest
    {
        [Fact]
        public void DefaultImplementation_ReturnsJsonTextReader()
        {
            var formatter = new CreateJsonReaderExposingFormatter();
            var reader = formatter.CreateJsonReaderPublic(CreateJsonStream(), null);
            Assert.IsType<JsonTextReader>(reader);
        }

        [Fact]
        public void DefaultImplementation_NoPropertyNameTable()
        {
            var formatter = new JsonEventFormatter();
            var event1 = formatter.DecodeStructuredModeMessage(CreateJsonStream(), null, null);
            var event2 = formatter.DecodeStructuredModeMessage(CreateJsonStream(), null, null);

            JObject data1 = (JObject)event1.Data;
            JObject data2 = (JObject)event2.Data;

            var property1 = data1.Properties().Single();
            var property2 = data2.Properties().Single();
            Assert.Equal(property1.Name, property2.Name);
            Assert.NotSame(property1.Name, property2.Name);
        }

        [Fact]
        public void Specialization_WithPropertyNameTable()
        {
            var formatter = new PropertyNameTableFormatter();
            var event1 = formatter.DecodeStructuredModeMessage(CreateJsonStream(), null, null);
            var event2 = formatter.DecodeStructuredModeMessage(CreateJsonStream(), null, null);

            JObject data1 = (JObject)event1.Data;
            JObject data2 = (JObject)event2.Data;

            var property1 = data1.Properties().Single();
            var property2 = data2.Properties().Single();
            Assert.Equal(property1.Name, property2.Name);
            Assert.Same(property1.Name, property2.Name);
        }

        private Stream CreateJsonStream()
        {
            var cloudEvent = new CloudEvent
            {
                Data = new { DataName = "DataValue" }
            }.PopulateRequiredAttributes();
            var bytes = new JsonEventFormatter().EncodeStructuredModeMessage(cloudEvent, out _);
            return new MemoryStream(bytes);
        }

        private class CreateJsonReaderExposingFormatter : JsonEventFormatter
        {
            public JsonReader CreateJsonReaderPublic(Stream stream, Encoding encoding) =>
                base.CreateJsonReader(stream, encoding);
        }

        private class PropertyNameTableFormatter : JsonEventFormatter
        {
            private readonly DefaultJsonNameTable table;

            public PropertyNameTableFormatter()
            {
                // Names aren't automatically cached by JsonTextReader, so we need to prepopulate the table.
                table = new DefaultJsonNameTable();
                table.Add("DataName");
            }

            protected override JsonReader CreateJsonReader(Stream stream, Encoding encoding)
            {
                var reader = (JsonTextReader) base.CreateJsonReader(stream, encoding);
                reader.PropertyNameTable = table;
                return reader;
            }
        }
    }
}
