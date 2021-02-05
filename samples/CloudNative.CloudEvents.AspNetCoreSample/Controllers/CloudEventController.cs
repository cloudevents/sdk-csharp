// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace CloudNative.CloudEvents.AspNetCoreSample.Controllers
{
    [Route("api/events")]
    [ApiController]
    public class CloudEventController : ControllerBase
    {
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
    }
}
