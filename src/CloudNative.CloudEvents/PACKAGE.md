## About

CloudNative.CloudEvents is a NuGet package that provides support for creating, encoding, decoding, sending, and receiving CNCF CloudEvents.

## Key Features

* Supports constructing a spec-compliant CloudEvent
* Provides abstractions for building custom CloudEvent formatters

## How to Use

To start using the CloudNative.CloudEvents package in a .NET application, follow these steps:

### Installation

```sh
dotnet add package CloudNative.CloudEvents
```

### Configuration

In the application of your choosing, construct a `CloudEvent` as follows:

```C#
using CloudNative.CloudEvents;

CloudEvent cloudEvent = new CloudEvent
{
    Id = "event-id",
    Type = "event-type",
    Source = new Uri("https://cloudevents.io/"),
    Time = DateTimeOffset.UtcNow,
    DataContentType = "text/plain",
    Data = "This is CloudEvent data"
};
```

This package only provides the abstractions for constructing a CloudEvent. For complete deserialization and serialization behavior, you will need to use an accompanying formatter library. For more information on configuring and using CloudNative.CloudEvents, refer to the [official documentation](https://github.com/cloudevents/sdk-csharp/tree/main/docs).

## Main Types

The main types provided by this library are:

* `CloudEvent`: Represents a spec-compliant CloudEvent.
* `CloudEventFormatter`: Provides an abstract class that can be extended to implement an event formatter.

## Feedback & Contributing

CloudNative.CloudEvents is released as open-source under the [Apache license](https://licenses.nuget.org/Apache-2.0). Bug reports and contributions are welcome at [the GitHub repository](https://github.com/cloudevents/sdk-csharp).
