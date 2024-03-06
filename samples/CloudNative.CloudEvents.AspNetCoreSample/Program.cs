// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.AspNetCoreSample;
using Microsoft.AspNetCore.Builder;
using CloudNative.CloudEvents.NewtonsoftJson;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(opts =>
    opts.InputFormatters.Insert(0, new CloudEventJsonInputFormatter(new JsonEventFormatter())));

var app = builder.Build();

app.MapControllers();

app.Run();

// Generated `Program` class when using top-level statements
// is internal by default. Make this `public` here for tests.
public partial class Program { }
