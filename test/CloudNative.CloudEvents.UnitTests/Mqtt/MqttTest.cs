// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.NewtonsoftJson;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Server;
using System;
using System.Net.Mime;
using System.Threading.Tasks;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.Mqtt.UnitTests
{
    public class MqttTest : IDisposable
    {
        private readonly IMqttServer mqttServer;

        public MqttTest()
        {
            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithConnectionBacklog(100)
                .WithDefaultEndpointPort(52355);

            this.mqttServer = new MqttFactory().CreateMqttServer();
            mqttServer.StartAsync(optionsBuilder.Build()).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            mqttServer.StopAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task MqttSendTest()
        {

            var jsonEventFormatter = new JsonEventFormatter();
            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull/123"),
                Id = "A234-1234-1234",
                Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero),
                DataContentType = MediaTypeNames.Text.Xml,
                Data = "<much wow=\"xml\"/>",
                ["comexampleextension1"] = "value"
            };

            var client = new MqttFactory().CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithClientId("Client1")
                .WithTcpServer("localhost", 52355)
                .WithCleanSession()
                .Build();

            TaskCompletionSource<CloudEvent> tcs = new TaskCompletionSource<CloudEvent>();
            await client.ConnectAsync(options);
            client.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(
                args => tcs.SetResult(args.ApplicationMessage.ToCloudEvent(jsonEventFormatter)));

            var result = await client.SubscribeAsync("abc");
            await client.PublishAsync(cloudEvent.ToMqttApplicationMessage(ContentMode.Structured, new JsonEventFormatter(), topic: "abc"));
            var receivedCloudEvent = await tcs.Task;

            Assert.Equal(CloudEventsSpecVersion.Default, receivedCloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", receivedCloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), receivedCloudEvent.Source);
            Assert.Equal("A234-1234-1234", receivedCloudEvent.Id);
            AssertTimestampsEqual("2018-04-05T17:31:00Z", receivedCloudEvent.Time.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, receivedCloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", receivedCloudEvent.Data);

            Assert.Equal("value", (string)receivedCloudEvent["comexampleextension1"]);
        }
    }
}