## About

This package provides a CloudEvent formatter for the Protobuf event format using `Google.Protobuf`; for an overview of formatters and their types in this SDK, see the [CloudEvent formatters](https://github.com/cloudevents/sdk-csharp/blob/main/docs/guide.md#cloudevent-formatters) section of the user guide.

## How to Use

Create the formatter, encode a CloudEvent, then decode it again:

```csharp
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Protobuf;

var cloudEvent = new CloudEvent
{
    Id = Guid.NewGuid().ToString(),
    Source = new Uri("https://example.com/orders"),
    Type = "com.example.order.created",
    Data = "Order created"
};

var formatter = new ProtobufEventFormatter();
var body = formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
var decoded = formatter.DecodeStructuredModeMessage(body, contentType, extensionAttributes: null);
```

For more examples, see [Samples](https://github.com/cloudevents/sdk-csharp/tree/main/samples).

## Additional Documentation

- [CloudEvents Core SDK](https://www.nuget.org/packages/CloudNative.CloudEvents/)
- [Main docs](https://github.com/cloudevents/sdk-csharp/tree/main/docs)
- [Formatter implementation guide](https://github.com/cloudevents/sdk-csharp/blob/main/docs/formatters.md)
- [CloudEvents Protobuf Event Format specification](https://github.com/cloudevents/spec/blob/ce@stable/cloudevents/formats/protobuf-format.md)
