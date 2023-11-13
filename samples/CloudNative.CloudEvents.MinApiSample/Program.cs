// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Http;
using CloudNative.CloudEvents.SystemTextJson;
using CloudNative.CloudEvents.AspNetCore;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var formatter = new JsonEventFormatter<Message>(MyJsonContext.Default);

app.MapPost("/api/events/receive/", async (HttpRequest request) =>
{
    var cloudEvent = await request.ToCloudEventAsync(formatter);
    using var ms = new MemoryStream();
    using var writer = new Utf8JsonWriter(ms, new() { Indented = true });
    writer.WriteStartObject();
    foreach (var (attribute, value) in cloudEvent.GetPopulatedAttributes())
        writer.WriteString(attribute.Name, attribute.Format(value));
    writer.WriteEndObject();
    await writer.FlushAsync();
    var attributeMap = Encoding.UTF8.GetString(ms.ToArray());
    return Results.Text($"Received event with ID {cloudEvent.Id}, attributes: {attributeMap}");
});

app.MapPost("/api/events/receive2/", (Event e) => Results.Json(e.CloudEvent.Data, MyJsonContext.Default));

app.MapPost("/api/events/receive3/", (Message message) => Results.Json(message, MyJsonContext.Default));

app.MapGet("/api/events/generate/", () =>
{
    var evt = new CloudEvent
    {
        Type = "CloudNative.CloudEvents.MinApiSample",
        Source = new Uri("https://github.com/cloudevents/sdk-csharp"),
        Time = DateTimeOffset.Now,
        DataContentType = "application/json",
        Id = Guid.NewGuid().ToString(),
        Data = new Message("C#", Environment.Version.ToString())
    };
    // Format the event as the body of the response. This is UTF-8 JSON because of
    // the CloudEventFormatter we're using, but EncodeStructuredModeMessage always
    // returns binary data. We could return the data directly, but for debugging
    // purposes it's useful to have the JSON string.
    var bytes = formatter.EncodeStructuredModeMessage(evt, out var contentType);
    string json = Encoding.UTF8.GetString(bytes.Span);
    // Specify the content type of the response: this is what makes it a CloudEvent.
    // (In "binary mode", the content type is the content type of the data, and headers
    // indicate that it's a CloudEvent.)
    return Results.Content(json, contentType.MediaType, Encoding.UTF8);
});

app.Run();

[JsonSerializable(typeof(Message))]
internal partial class MyJsonContext : JsonSerializerContext { }

public class Event
{
    private readonly static JsonEventFormatter formatter = new JsonEventFormatter<Message>(MyJsonContext.Default);
    // required for receive2
    public static async ValueTask<Event?> BindAsync(HttpContext context)
    {
        var cloudEvent = await context.Request.ToCloudEventAsync(formatter);
        return new Event { CloudEvent = cloudEvent };
    }
    public required CloudEvent CloudEvent { get; init; }
}

record class Message(string Language, string EnvironmentVersion)
{
    private readonly static JsonEventFormatter formatter = new JsonEventFormatter<Message>(MyJsonContext.Default);
    // required for receive3
    public static async ValueTask<Message?> BindAsync(HttpContext context)
    {
        var cloudEvent = await context.Request.ToCloudEventAsync(formatter);
        return cloudEvent.Data is Message message ? message : null;
    }
}

