// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents.AspNetCoreSample.Controllers
{
    using System.Collections.Generic;
    using CloudNative.CloudEvents;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;

    [Route("api/events")]
    [ApiController]
    public class CloudEventController : ControllerBase
    {
        [HttpPost("receive")]
        public ActionResult<IEnumerable<string>> ReceiveCloudEvent([FromBody] CloudEvent cloudEvent)
        {
            return Ok($"Received event with ID {cloudEvent.Id}, attributes: {JsonConvert.SerializeObject(cloudEvent.GetAttributes())}");
        }
    }
}
