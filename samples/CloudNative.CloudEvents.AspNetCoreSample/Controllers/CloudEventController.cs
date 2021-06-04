// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.NewtonsoftJson;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace CloudNative.CloudEvents.AspNetCoreSample.Controllers
{
    [Route("api/events")]
    [ApiController]
    public class CloudEventController : ControllerBase
    {
        private static readonly CloudEventFormatter formatter = new JsonEventFormatter();

        [HttpPost("receive")]
        public ActionResult<IEnumerable<string>> ReceiveCloudEvent([FromBody] CloudEvent cloudEvent)
        {
            var attributeMap = new JObject();
            foreach (var (attribute, value) in cloudEvent.GetPopulatedAttributes())
            {
                attributeMap[attribute.Name] = attribute.Format(value);
            }
            return Ok($"Received event with ID {cloudEvent.Id}, attributes: {attributeMap}");
        }

        /// <summary>
        /// Generates a CloudEvent in "structured mode", where all CloudEvent information is
        /// included within the body of the response.
        /// </summary>
        [HttpGet("generate")]
        public ActionResult<string> GenerateCloudEvent()
        {
            var evt = new CloudEvent
            {
                Type = "CloudNative.CloudEvents.AspNetCoreSample",
                Source = new Uri("https://github.com/cloudevents/sdk-csharp"),
                Time = DateTimeOffset.Now,
                DataContentType = "application/json",
                Id = Guid.NewGuid().ToString(),
                Data = new
                {
                    Language = "C#",
                    EnvironmentVersion = Environment.Version.ToString()
                }
            };
            // Format the event as the body of the response. This is UTF-8 JSON because of
            // the CloudEventFormatter we're using, but EncodeStructuredModeMessage always
            // returns binary data. We could return the data directly, but for debugging
            // purposes it's useful to have the JSON string.
            var bytes = formatter.EncodeStructuredModeMessage(evt, out var contentType);
            string json = Encoding.UTF8.GetString(bytes.Span);
            var result = Ok(json);

            // Specify the content type of the response: this is what makes it a CloudEvent.
            // (In "binary mode", the content type is the content type of the data, and headers
            // indicate that it's a CloudEvent.)
            result.ContentTypes.Add(contentType.MediaType);
            return result;
        }
    }
}
