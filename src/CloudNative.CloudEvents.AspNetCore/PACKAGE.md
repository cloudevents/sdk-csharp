## About

This package provides a protocol binding for transporting CloudEvents with ASP.NET Core request and response types; for an overview of protocol bindings and their types in this SDK, see the [Protocol bindings](https://github.com/cloudevents/sdk-csharp/blob/main/docs/guide.md#protocol-bindings) section of the user guide.

## How to Use

Use this package with a formatter in an ASP.NET Core endpoint to read a CloudEvent from the request and write one to the response:

```csharp
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.AspNetCore;
using CloudNative.CloudEvents.SystemTextJson;
using Microsoft.AspNetCore.Http;

app.MapPost("/events", async (HttpRequest request, HttpResponse response) =>
{
    var formatter = new JsonEventFormatter();
    var cloudEvent = await request.ToCloudEventAsync(formatter);

    var result = new CloudEvent
    {
        Id = Guid.NewGuid().ToString(),
        Source = new Uri("https://example.com/processed"),
        Type = "com.example.event.processed",
        DataContentType = "application/json",
        Data = new { OriginalId = cloudEvent.Id }
    };

    await result.CopyToHttpResponseAsync(response, ContentMode.Structured, formatter);
});
```

For more examples, see [Samples](https://github.com/cloudevents/sdk-csharp/tree/main/samples).

## Additional Documentation

- [CloudEvents Core SDK](https://www.nuget.org/packages/CloudNative.CloudEvents/)
- [Main docs](https://github.com/cloudevents/sdk-csharp/tree/main/docs)
- [CloudEvents HTTP Protocol Binding specification](https://github.com/cloudevents/spec/blob/ce@stable/cloudevents/bindings/http-protocol-binding.md)
- [ASP.NET Core sample](https://github.com/cloudevents/sdk-csharp/tree/main/samples/CloudNative.CloudEvents.AspNetCoreSample)
