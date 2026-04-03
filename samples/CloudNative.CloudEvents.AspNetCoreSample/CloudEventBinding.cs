// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.AspNetCore;
using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.SystemTextJson;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.AspNetCoreSample;

public class CloudEventBinding : IBindableFromHttpContext<CloudEventBinding>
{
    public CloudEvent Value { get; init; }

    public ProblemHttpResult Error { get; init; }

    public static async ValueTask<CloudEventBinding> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        Validation.CheckNotNull(context, nameof(context));
        Validation.CheckNotNull(parameter, nameof(parameter));

        var request = context.Request;

        // Even though we're not allowing non-JSON content in this binding,
        // types such as "text/xml" could still be parsed with the current JsonEventFormatter,
        // but it's just not making it strongly typed, or anything structured (XmlNode).
        // Depending on your use-case, it may or may not be desirable to allow that.
        if (request.ContentLength != 0 && !request.HasJsonContentType())
        {
            return new CloudEventBinding
            {
                Error = TypedResults.Problem(
                    statusCode: StatusCodes.Status415UnsupportedMediaType,
                    title: "Unsupported media type",
                    detail: "Request content type is not JSON and not fully supported in this binding. " +
                    "Please note: the CloudEvents specification does allow for any data content, " +
                    "as long as it adheres to the provided datacontenttype."
                )
            };
        }

        var formatter = context.RequestServices.GetRequiredService<JsonEventFormatter>();

        var cloudEvent = await request.ToCloudEventAsync(formatter);

        return new CloudEventBinding
        {
            Value = cloudEvent
        };
    }
}