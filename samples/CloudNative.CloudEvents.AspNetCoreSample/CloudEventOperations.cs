// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.AspNetCore;
using CloudNative.CloudEvents.SystemTextJson;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.AspNetCoreSample;

public static class CloudEventOperations
{
    public static async Task<Results<ProblemHttpResult, JsonHttpResult<object>>> ReceiveCloudEvent(CloudEventBinding cloudEventBinding)
    {
        if (cloudEventBinding.Error is not null)
        {
            return cloudEventBinding.Error;
        }

        var cloudEvent = cloudEventBinding.Value;

        var cloudEventAttributes = cloudEvent.GetPopulatedAttributes()
            .ToDictionary(pair => pair.Key.Name, pair => pair.Key.Format(pair.Value));

        return TypedResults.Json<object>(new
        {
            note = "wow, such event, much disassembling, very skill",
            cloudEvent.SpecVersion.VersionId,
            cloudEventAttributes,
            cloudEvent.Data
        });
    }

    /// <summary>
    /// Generates a CloudEvent.
    /// </summary>
    public static async Task GenerateCloudEvent(HttpResponse response, JsonEventFormatter formatter, ContentMode contentMode = ContentMode.Structured)
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

        response.StatusCode = StatusCodes.Status200OK;
        await evt.CopyToHttpResponseAsync(response, contentMode, formatter);
    }
}
