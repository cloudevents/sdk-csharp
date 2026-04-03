// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.AspNetCoreSample;
using CloudNative.CloudEvents.SystemTextJson;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
builder.Services.AddSingleton(new JsonEventFormatter(jsonOptions, new JsonDocumentOptions()));

var app = builder.Build();

var apiEvents = app.MapGroup("/api/events");
apiEvents.MapPost("/receive", CloudEventOperations.ReceiveCloudEvent);
apiEvents.MapGet("/generate", CloudEventOperations.GenerateCloudEvent);

app.Run();

// Generated `Program` class when using top-level statements
// is internal by default. Make this `public` here for tests.
public partial class Program { }
