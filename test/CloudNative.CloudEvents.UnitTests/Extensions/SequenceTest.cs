// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.NewtonsoftJson;
using System;
using System.Text;
using Xunit;

namespace CloudNative.CloudEvents.Extensions.UnitTests
{
    public class SequenceTest
    {
        private static readonly string sampleJson = @"
           {
               'specversion' : '1.0',
               'type' : 'com.github.pull.create',
               'id' : 'A234-1234-1234',
               'time' : '2018-04-05T17:31:00Z',
               'sequencetype' : 'Integer',
               'sequence' : '25'
           }".Replace('\'', '"');

        [Fact]
        public void Parse()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(sampleJson), Sequence.AllAttributes);

            Assert.Equal("Integer", cloudEvent[Sequence.SequenceTypeAttribute]);
            Assert.Equal("25", cloudEvent[Sequence.SequenceAttribute]);
        }

        [Fact]
        public void Transcode()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent1 = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(sampleJson));
            var jsonData = jsonFormatter.EncodeStructuredEvent(cloudEvent1, out var contentType);
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(jsonData, Sequence.AllAttributes);

            Assert.Equal("Integer", cloudEvent[Sequence.SequenceTypeAttribute]);
            Assert.Equal("25", cloudEvent[Sequence.SequenceAttribute]);
        }

        [Fact]
        public void GetSequenceExtensionMethods_Integer()
        {
            var cloudEvent = new CloudEvent
            {
                ["sequencetype"] = "Integer",
                ["sequence"] = "25"
            };

            Assert.Equal(25, cloudEvent.GetSequenceValue());
            Assert.Equal("25", cloudEvent.GetSequenceString());
            Assert.Equal("Integer", cloudEvent.GetSequenceType());
        }

        [Fact]
        public void GetSequenceExtensionMethods_Null()
        {
            var cloudEvent = new CloudEvent();

            Assert.Null(cloudEvent.GetSequenceValue());
            Assert.Null(cloudEvent.GetSequenceString());
            Assert.Null(cloudEvent.GetSequenceType());
        }

        [Fact]
        public void GetSequenceExtensionMethods_UnknownType()
        {
            var cloudEvent = new CloudEvent
            {
                ["sequencetype"] = "Mystery",
                ["sequence"] = "xyz"
            };

            Assert.Equal("Mystery", cloudEvent.GetSequenceType());
            Assert.Equal("xyz", cloudEvent.GetSequenceString());
            Assert.Throws<InvalidOperationException>(() => cloudEvent.GetSequenceValue());
        }

        [Fact]
        public void SetSequence_Null()
        {
            var cloudEvent = new CloudEvent
            {
                ["sequence"] = "xyz",
                ["sequencetype"] = "new sequence type"
            };
            cloudEvent.SetSequence(null);

            Assert.Null(cloudEvent["sequence"]);
            Assert.Null(cloudEvent["sequencetype"]);
        }


        [Fact]
        public void SetSequence_Integer()
        {
            var cloudEvent = new CloudEvent().SetSequence(15);

            Assert.Equal("15", cloudEvent["sequence"]);
            Assert.Equal("Integer", cloudEvent["sequencetype"]);
        }

        [Fact]
        public void SetSequence_UnknownType()
        {
            var cloudEvent = new CloudEvent();
            var uri = new Uri("https://oddsequencetype");
            Assert.Throws<ArgumentException>(() => cloudEvent.SetSequence(uri));
        }
    }
}
