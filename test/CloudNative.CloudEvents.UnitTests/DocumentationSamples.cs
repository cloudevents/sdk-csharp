// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.AspNetCore;
using CloudNative.CloudEvents.Http;
using CloudNative.CloudEvents.NewtonsoftJson;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace CloudNative.CloudEvents.UnitTests
{
    /// <summary>
    /// Tests for the code in the docs/ directory.
    /// The code itself is currently copy/pasted from this file, but at least we have some confidence
    /// that it's working code. In the future we can write tooling to extract the code automatically.
    /// </summary>
    public class DocumentationSamples
    {
        [Fact]
        public async Task HttpRequestMessageRoundtrip()
        {
            var requestMessage = CreateHttpRequestMessage();
            var request = await ConvertHttpRequestMessage(requestMessage);

            var cloudEvent = await ParseHttpRequestAsync(request);
            Assert.Equal("event-id", cloudEvent.Id);
            Assert.Equal("This is CloudEvent data", cloudEvent.Data);
        }

        private static HttpRequestMessage CreateHttpRequestMessage()
        {
            // Sample: guide.md#PopulateHttpRequestMessage
            CloudEvent cloudEvent = new CloudEvent
            {
                Id = "event-id",
                Type = "event-type",
                Source = new Uri("https://cloudevents.io/"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "text/plain",
                Data = "This is CloudEvent data"
            };

            CloudEventFormatter formatter = new JsonEventFormatter();
            HttpRequestMessage request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                Content = cloudEvent.ToHttpContent(ContentMode.Structured, formatter)
            };
            // End sample
            return request;
        }

        private static async Task<CloudEvent> ParseHttpRequestAsync(HttpRequest request)
        {
            // Sample: guide.md#ParseHttpRequestMessage
            CloudEventFormatter formatter = new JsonEventFormatter();
            CloudEvent cloudEvent = await request.ToCloudEventAsync(formatter);
            // End sample
            return cloudEvent;
        }

        [Fact]
        public async Task GameResultRoundtrip()
        {
            var requestMessage = SerializeGameResult();
            var request = await ConvertHttpRequestMessage(requestMessage);
            var result = await DeserializeGameResult(request);
            Assert.Equal("player1", result.PlayerId);
            Assert.Equal("game1", result.GameId);
            Assert.Equal(200, result.Score);
        }

        // Sample: guide.md#GameResult
        public class GameResult
        {
            [JsonProperty("playerId")]
            public string PlayerId { get; set; }

            [JsonProperty("gameId")]
            public string GameId { get; set; }

            [JsonProperty("score")]
            public int Score { get; set; }
        }
        // End sample

        private static HttpRequestMessage SerializeGameResult()
        {
            // Sample: guide.md#SerializeGameResult
            var result = new GameResult
            {
                PlayerId = "player1",
                GameId = "game1",
                Score = 200
            };
            var cloudEvent = new CloudEvent
            {
                Id = "result-1",
                Type = "game.played.v1",
                Source = new Uri("https://cloudevents.io/"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data = result
            };
            var formatter = new JsonEventFormatter();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                Content = cloudEvent.ToHttpContent(ContentMode.Binary, formatter)
            };
            // End sample
            return request;
        }

        private static async Task<GameResult> DeserializeGameResult(HttpRequest request)
        {
            // Sample: guide.md#DeserializeGameResult
            CloudEventFormatter formatter = new JsonEventFormatter();
            CloudEvent cloudEvent = await request.ToCloudEventAsync(formatter);
            JObject dataAsJObject = (JObject) cloudEvent.Data;
            GameResult result = dataAsJObject.ToObject<GameResult>();
            // End sample
            return result;
        }

        private static async Task<HttpRequest> ConvertHttpRequestMessage(HttpRequestMessage message)
        {
            var request = new DefaultHttpRequest(new DefaultHttpContext());
            foreach (var header in message.Headers)
            {
                request.Headers[header.Key] = header.Value.Single();
            }
            foreach (var header in message.Content.Headers)
            {
                request.Headers[header.Key] = header.Value.Single();
            }

            var contentBytes = await message.Content.ReadAsByteArrayAsync();
            request.Body = new MemoryStream(contentBytes);
            return request;
        }
    }
}
