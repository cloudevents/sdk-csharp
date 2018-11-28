// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.UnitTests
{
    using System;
    using System.Net.Mime;
    using System.Text;
    using CloudNative.CloudEvents.Extensions;
    using Xunit;

    public class ExtensionsTest
    {
        const string jsonDistTrace =
           "{\n" +
           "    \"specversion\" : \"0.2\",\n" +
           "    \"type\" : \"com.github.pull.create\",\n" +
           "    \"source\" : \"https://github.com/cloudevents/spec/pull/123\",\n" +
           "    \"id\" : \"A234-1234-1234\",\n" +
           "    \"time\" : \"2018-04-05T17:31:00Z\",\n" +
           "    \"traceparent\" : \"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01\",\n" +
           "    \"tracestate\" : \"rojo=00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01,congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4=\",\n" +
           "    \"contenttype\" : \"text/plain\",\n" +
           "    \"data\" : \"test\"\n" +
           "}";

        const string jsonSequence =
            "{\n" +
            "    \"specversion\" : \"0.2\",\n" +
            "    \"type\" : \"com.github.pull.create\",\n" +
            "    \"source\" : \"https://github.com/cloudevents/spec/pull/123\",\n" +
            "    \"id\" : \"A234-1234-1234\",\n" +
            "    \"time\" : \"2018-04-05T17:31:00Z\",\n" +
            "    \"sequencetype\" : \"Integer\",\n" +
            "    \"sequence\" : \"25\",\n" +
            "    \"contenttype\" : \"text/plain\",\n" +
            "    \"data\" : \"test\"\n" +
            "}";

        const string jsonSampledRate =
            "{\n" +
            "    \"specversion\" : \"0.2\",\n" +
            "    \"type\" : \"com.github.pull.create\",\n" +
            "    \"source\" : \"https://github.com/cloudevents/spec/pull/123\",\n" +
            "    \"id\" : \"A234-1234-1234\",\n" +
            "    \"time\" : \"2018-04-05T17:31:00Z\",\n" +
            "    \"sampledrate\" : \"1\",\n" +
            "    \"contenttype\" : \"text/plain\",\n" +
            "    \"data\" : \"test\"\n" +
            "}";

        [Fact]
        public void DistTraceParse()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonDistTrace), new DistributedTracingExtension());
            Assert.Equal(CloudEventsSpecVersion.V0_2, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());
            
            Assert.Equal("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", cloudEvent.Extension<DistributedTracingExtension>().TraceParent);
            Assert.Equal("rojo=00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01,congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4=", cloudEvent.Extension<DistributedTracingExtension>().TraceState);
        }

        [Fact]
        public void DistTraceJsonTranscode()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent1 = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonDistTrace));
            var jsonData = jsonFormatter.EncodeStructuredEvent(cloudEvent1, out var contentType);
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(jsonData, new DistributedTracingExtension());

            Assert.Equal(CloudEventsSpecVersion.V0_2, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());

            Assert.Equal("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", cloudEvent.Extension<DistributedTracingExtension>().TraceParent);
            Assert.Equal("rojo=00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01,congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4=", cloudEvent.Extension<DistributedTracingExtension>().TraceState);
        }

        [Fact]
        public void SequenceParse()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonSequence), new SequenceExtension());
            Assert.Equal(CloudEventsSpecVersion.V0_2, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());

            Assert.Equal("Integer", cloudEvent.Extension<SequenceExtension>().SequenceType);
            Assert.Equal("25", cloudEvent.Extension<SequenceExtension>().Sequence);
        }

        [Fact]
        public void SequenceJsonTranscode()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent1 = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonSequence));
            var jsonData = jsonFormatter.EncodeStructuredEvent(cloudEvent1, out var contentType);
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(jsonData, new SequenceExtension());

            Assert.Equal(CloudEventsSpecVersion.V0_2, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());

            Assert.Equal("Integer", cloudEvent.Extension<SequenceExtension>().SequenceType);
            Assert.Equal("25", cloudEvent.Extension<SequenceExtension>().Sequence);
        }

        [Fact]
        public void IntegerSequenceParse()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonSequence), new IntegerSequenceExtension());
            Assert.Equal(CloudEventsSpecVersion.V0_2, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());

            Assert.Equal(25, cloudEvent.Extension<IntegerSequenceExtension>().Sequence);
        }

        [Fact]
        public void IntegerSequenceJsonTranscode()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent1 = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonSequence));
            var jsonData = jsonFormatter.EncodeStructuredEvent(cloudEvent1, out var contentType);
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(jsonData, new IntegerSequenceExtension());

            Assert.Equal(CloudEventsSpecVersion.V0_2, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());

            Assert.Equal(25, cloudEvent.Extension<IntegerSequenceExtension>().Sequence);
        }

        [Fact]
        public void SamplingParse()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonSampledRate), new SamplingExtension());
            Assert.Equal(CloudEventsSpecVersion.V0_2, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());

            Assert.Equal(1, cloudEvent.Extension<SamplingExtension>().SampledRate.Value);
        }

        [Fact]
        public void SamplingJsonTranscode()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent1 = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(jsonSampledRate));
            var jsonData = jsonFormatter.EncodeStructuredEvent(cloudEvent1, out var contentType);
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(jsonData, new SamplingExtension());

            Assert.Equal(CloudEventsSpecVersion.V0_2, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            Assert.Equal(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime(),
                cloudEvent.Time.Value.ToUniversalTime());

            Assert.Equal(1, cloudEvent.Extension<SamplingExtension>().SampledRate.Value);
        }
    }
}