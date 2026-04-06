## About

Core CloudEvents support for .NET.

This package provides the core CloudEvents types for representing events and their attributes in .NET, plus the `CloudEventFormatter` base class.

It also includes the built-in HTTP protocol binding; for an overview of protocol bindings and their types in this SDK, see the [Protocol bindings](https://github.com/cloudevents/sdk-csharp/blob/main/docs/guide.md#protocol-bindings) section of the user guide.

## How to Use

You can use the core package on its own, or combine it with a formatter package such as [CloudNative.CloudEvents.SystemTextJson](https://www.nuget.org/packages/CloudNative.CloudEvents.SystemTextJson/) when you want to serialize events with the built-in HTTP binding:

```csharp
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Http;
using CloudNative.CloudEvents.SystemTextJson;

var cloudEvent = new CloudEvent
{
    Id = Guid.NewGuid().ToString(),
    Source = new Uri("https://example.com/orders"),
    Type = "com.example.order.created",
    DataContentType = "application/json",
    Data = new { OrderId = 1234 }
};

var formatter = new JsonEventFormatter();
var content = cloudEvent.ToHttpContent(ContentMode.Structured, formatter);
```

For more examples, see [Samples](https://github.com/cloudevents/sdk-csharp/tree/main/samples).

## Additional Documentation

- [Main docs](https://github.com/cloudevents/sdk-csharp/tree/main/docs)
- [CloudEvents Event Format specification](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md#event-format)
- [CloudEvents HTTP Protocol Binding specification](https://github.com/cloudevents/spec/blob/ce@stable/cloudevents/bindings/http-protocol-binding.md)
- [User guide](https://github.com/cloudevents/sdk-csharp/blob/main/docs/guide.md)
