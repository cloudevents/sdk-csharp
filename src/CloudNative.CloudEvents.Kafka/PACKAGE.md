## About

This package provides a protocol binding for transporting CloudEvents over Kafka using `Confluent.Kafka`; for an overview of protocol bindings and their types in this SDK, see the [Protocol bindings](https://github.com/cloudevents/sdk-csharp/blob/main/docs/guide.md#protocol-bindings) section of the user guide.

## How to Use

Use this package with a CloudEvent and formatter to create a Kafka message:

```csharp
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Kafka;
using CloudNative.CloudEvents.SystemTextJson;

var cloudEvent = new CloudEvent
{
    Id = Guid.NewGuid().ToString(),
    Source = new Uri("https://example.com/orders"),
    Type = "com.example.order.created"
};

var formatter = new JsonEventFormatter();
var message = cloudEvent.ToKafkaMessage(ContentMode.Structured, formatter);
```

For more examples, see [Samples](https://github.com/cloudevents/sdk-csharp/tree/main/samples).

## Additional Documentation

- [CloudEvents Core SDK](https://www.nuget.org/packages/CloudNative.CloudEvents/)
- [Main docs](https://github.com/cloudevents/sdk-csharp/tree/main/docs)
- [CloudEvents Kafka Protocol Binding specification](https://github.com/cloudevents/spec/blob/ce@stable/cloudevents/bindings/kafka-protocol-binding.md)
