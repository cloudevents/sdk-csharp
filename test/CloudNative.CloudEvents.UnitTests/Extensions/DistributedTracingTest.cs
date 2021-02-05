// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Text;
using Xunit;

namespace CloudNative.CloudEvents.Extensions.UnitTests
{
    public class DistributedTracingTest
    {
        private const string SampleParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        private const string SampleState = "rojo=00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01,congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4=";

        private static readonly string sampleJson = @"
           {
               'specversion' : '1.0',
               'type' : 'com.github.pull.create',
               'id' : 'A234-1234-1234',
               'time' : '2018-04-05T17:31:00Z',
               'traceparent' : '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01',
               'tracestate' : 'rojo=00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01,congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4=',
           }".Replace('\'', '"');

        [Fact]
        public void ParseJson()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(sampleJson), DistributedTracing.AllAttributes);

            Assert.Equal(SampleParent, cloudEvent[DistributedTracing.TraceParentAttribute]);
            Assert.Equal(SampleParent, cloudEvent.GetTraceParent());
            Assert.Equal(SampleState, cloudEvent[DistributedTracing.TraceStateAttribute]);
            Assert.Equal(SampleState, cloudEvent.GetTraceState());
        }

        [Fact]
        public void Transcode()
        {
            var jsonFormatter = new JsonEventFormatter();
            var cloudEvent1 = jsonFormatter.DecodeStructuredEvent(Encoding.UTF8.GetBytes(sampleJson));
            var jsonData = jsonFormatter.EncodeStructuredEvent(cloudEvent1, out _);
            var cloudEvent = jsonFormatter.DecodeStructuredEvent(jsonData, DistributedTracing.AllAttributes);

            Assert.Equal(SampleParent, cloudEvent[DistributedTracing.TraceParentAttribute]);
            Assert.Equal(SampleParent, cloudEvent.GetTraceParent());
            Assert.Equal(SampleState, cloudEvent[DistributedTracing.TraceStateAttribute]);
            Assert.Equal(SampleState, cloudEvent.GetTraceState());
        }

        [Fact]
        public void SetExtensionMethods()
        {
            var cloudEvent = new CloudEvent();
            cloudEvent.SetTraceParent("parent");
            cloudEvent.SetTraceState("state");
            Assert.Equal("parent", cloudEvent[DistributedTracing.TraceParentAttribute]);
            Assert.Equal("state", cloudEvent[DistributedTracing.TraceStateAttribute]);
        }

        [Fact]
        public void GetExtensionMethods()
        {
            var cloudEvent = new CloudEvent
            {
                ["traceparent"] = "parent",
                ["tracestate"] = "state"
            };
            Assert.Equal("parent", cloudEvent.GetTraceParent());
            Assert.Equal("state", cloudEvent.GetTraceState());
        }
    }
}
