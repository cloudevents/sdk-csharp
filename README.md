![Build](https://ci.appveyor.com/api/projects/status/github/cloudevents/sdk-csharp?svg=true)

## Status

This SDK current supports the following versions of CloudEvents:
- v1.0

# sdk-csharp

.NET Standard 2.0 (C#) SDK for CloudEvents

The `CloudNative.CloudEvents` package provides utility methods and classes for creating, encoding,
decoding, sending, and receiving [CNCF CloudEvents](https://github.com/cloudevents/spec).

## A few gotchas highlighted for the impatient who don't usually read docs

1. The [CloudEvent](src/CloudNative.CloudEvents/CloudEvent.cs) class is not meant to be used with 
   object serializers like JSON.NET and does not have a default constructor to underline this. If you need to serialize or deserialize a CloudEvent directly, always use an [ICloudEventFormatter](src/CloudNative.CloudEvents/ICloudEventFormatter.cs) like the [JsonEventFormatter](src/CloudNative.CloudEvents/JsonEventFormatter.cs).
2. The transport integration is provided in the form of extensions and the objective of those extensions
   is to map the CloudEvent to and from the respective protocol message, like an [HTTP request](src/CloudNative.CloudEvents/CloudEventContent.cs) or [response](src/CloudNative.CloudEvents/HttpClientExtension.cs#L249)
   object, but the application is otherwise fully in control of the client. Therefore, the extensions do not
   add security headers or credentials or any other headers or properties that may be required to interact
   with a particular product or service. Adding this information is up to the application.

## CloudEvent

The `CloudEvent` class reflects the event envelope defined by the 
[CNCF CloudEvents 1.0 specification](https://github.com/cloudevents/spec/blob/v1.0/spec.md).
It supports version 1.0 of CloudEvents by default. It can also handle the pre-release versions
0.1, 0.2, and 0.3 of the CloudEvents specification.

The strongly typed API reflects the 1.0 standard.

If required for compatibility with code that leans on one of the prerelease specifications, you can
override the specification version explicitly: `new CloudEvent(CloudEventsSpecVersion.V0_1)`.
The `SpecVersion` property also allows the version to be switched, meaning you can receive a 0.1
event, switch the version number, and forward it as a 1.0 event, with all required mappings done
for you.

| **1.0**             | Property name            | CLR type                      |
| ------------------- | ------------------------ | ----------------------------- |
| **id**              | `CloudEvent.Id`          | `System.String`               |
| **type**            | `CloudEvent.Type`        | `System.String`               |
| **specversion**     | `CloudEvent.SpecVersion` | `System.String`               |
| **time**            | `CloudEvent.Time`        | `System.DateTime`             |
| **source**          | `CloudEvent.Source`      | `System.Uri`                  |
| **subject**         | `CloudEvent.Subject`     | `System.String`               |
| **dataschema**      | `CloudEvent.DataSchema`  | `System.Uri`                  |
| **datacontenttype** | `CloudEvent.ContentType` | `System.Net.Mime.ContentType` |
| **data**            | `CloudEvent.Data`        | `System.Object`               |

The `CloudEvent.Data` property is `object` typed, and may hold any valid serializable
CLR type. The following types have special handling:

- `System.String`: In binary content mode, strings are copied into the transport
  message payload body using UTF-8 encoding.
- `System.Byte[]`: In binary content mode, byte array content is copied into the
  message paylaod body without further transformation.
- `System.Stream`: In binary content mode, stream content is copied into the
  message paylaod body without further transformation.

Any other data type is transformed using the given event formatter for the operation
or the JSON formatter by default before being added to the transport payload body.

All extension attributes can be reached via the `CloudEvent.GetAttributes()` method,
which returns the internal attribute collection. The internal collection performs
all required validations.

## Extensions

CloudEvent extensions are represented by implementations of the `ICloudEventExtension`
interface. The SDK includes strongly-typed implementations for all offical CloudEvents
extensions:

- `DistributedTracingExtension` for [distributed tracing](https://github.com/cloudevents/spec/blob/master/extensions/distributed-tracing.md)
- `SampledRateExtension` for [sampled rate](https://github.com/cloudevents/spec/blob/master/extensions/sampled-rate.md)
- `SequenceExtension` for [sequence](https://github.com/cloudevents/spec/blob/master/extensions/sequence.md)

Extension classes provide type-safe access to the extension attributes as well as implement the
required validations and type mappings. An extension object is always created as an
independent entity and is then attached to a `CloudEvent` instance. Once attached, the
extension object's attributes are merged into the `CloudEvent` instance.

This snippet shows how to create a `CloudEvent` with an extension:

```C#
 var cloudEvent = new CloudEvent(
    "com.github.pull.create",
    new Uri("https://github.com/cloudevents/spec/pull/123"),
    new DistributedTracingExtension()
    {
        TraceParent = "value",
        TraceState = "value"
    })
{
    ContentType = new ContentType("application/json"),
    Data = "[]"
};
```

The extension can later be accessed via the `Extension<T>()` method:

```
 var s = cloudEvent.Extension<DistributedTracingExtension>().TraceParent
```

All APIs where a `CloudEvent` is constructed from an incoming event (or request or
response) allow for extension instances to be added via their respective methods, and
the extensions are invoked in the mapping process (for instance, to extract information
from headers that deviate from the CloudEvents default mapping).

For example, the server-side mapping for `HttpRequestMessage` allows adding
extensions like this:

```C#
public async Task<HttpResponseMessage> Run( HttpRequestMessage req, ILogger log)
{
    var cloudEvent = req.ToCloudEvent(new DistributedTracingExtension());
}
```

## Transport Bindings

This SDK helps with mapping CloudEvents to and from messages or transport frames of
popular .NET clients in such a way as to be agnostic of your application's choices of
how you want to send an event (be it via HTTP PUT or POST) or how you want to handle
settlement of transfers in AMQP or MQTT. The transport binding classes and extensions
therefore don't wrap the send and receive operations; you still use the native
API of the respective library.

### HTTP - System.Net.Http.HttpClient

The .NET [`HttpClient`](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) uses
the [`HttpContent`](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpcontent)
abstraction to wrap payloads for sending requests that carry entity bodies.

This SDK provides a [`CloudEventContent`] class derived from `HttpContent` that can be
created from a `CloudEvent` instance, the desired `ContentMode`, and an event formatter.

```C#

var cloudEvent = new CloudEvent("com.example.myevent", new Uri("urn:example-com:mysource"))
{
    ContentType = new ContentType(MediaTypeNames.Application.Json),
    Data = JsonConvert.SerializeObject("hey there!")
};

var content = new CloudEventContent( cloudEvent,
                                     ContentMode.Structured,
                                     new JsonEventFormatter());

var httpClient = new HttpClient();
var result = (await httpClient.PostAsync(this.Url, content));
```

For responses, `HttpClient` puts all custom headers onto the `HttpResponseMessage` rather
than on the carried `HttpContent` instance. Therefore, if an event is retrieved with
`HttpClient` (for instance, from a queue-like structure) the `CloudEvent` is created from
the response message object rather than the content object using the `ToCloudEvent()`
extension method on `HttpResponseMessage`:

```C#
var httpClient = new HttpClient();
// delete and receive message from top of the queue
var result = await httpClient.DeleteAsync(new Uri("https://example.com/queue/messages/top"));
if (HttpStatusCode.OK == result.StatusCode) {
   var receivedCloudEvent = await result.ToCloudEvent();
}
```

### HTTP - System.Net.HttpWebRequest

If your application uses the `HttpWebRequest` client, you can copy a CloudEvent into
the request payload in structured or binary mode:

```C#

HttpWebRequest httpWebRequest = WebRequest.CreateHttp("https://example.com/target");
httpWebRequest.Method = "POST";
await httpWebRequest.CopyFromAsync(cloudEvent, ContentMode.Structured, new JsonEventFormatter());
```

Bear in mind that the `Method` property must be set to an HTTP method that allows an entity body
to be sent, otherwise the copy operation will fail.

### HTTP - System.Net.HttpListener (HttpRequestMessage)

On the server-side, you can extract a CloudEvent from the server-side `HttpRequestMessage`
with the `ToCloudEventAsync()` extension. If your code handles `HttpRequestContext`,
you will use the `Request` property:

```C#
var cloudEvent = await context.Request.ToCloudEventAsync();
```

If you use a functions framework that lets you handle `HttpResponseMessage` and return
`HttpResponseMessage`, you will call the extension on the request object directly:

```C#
public async Task<HttpResponseMessage> Run( HttpRequestMessage req, ILogger log)
{
    var cloudEvent = await req.ToCloudEventAsync();
}
```

The extension implementation will read the `ContentType` header of the incoming request and
automatically select the correct built-in event format decoder. Your code can always pass an
overriding format decoder instance as the first argument if needed.

If your HTTP handler needs to return a CloudEvent, you copy the `CloudEvent` into the
response with the `CopyFromAsync()` extension method:

```C#
var cloudEvent = new CloudEvent("com.example.myevent", new Uri("urn:example-com:mysource"))
{
    ContentType = new ContentType(MediaTypeNames.Application.Json),
    Data = JsonConvert.SerializeObject("hey there!")
};

await context.Response.CopyFromAsync(cloudEvent,
                                     ContentMode.Structured,
                                     new JsonEventFormatter());
context.Response.StatusCode = (int)HttpStatusCode.OK;
```

### HTTP - Microsoft.AspNetCore.Http.HttpRequest

On the server-side, you can extract a CloudEvent from the server-side `HttpRequest` 
with the `ReadCloudEventAsync()` extension.

```C#
var cloudEvent = await HttpContext.Request.ReadCloudEventAsync();
``` 

### HTTP - ASP.NET Core MVC

If you would like to deserialize CloudEvents in actions directly, you can register the
`CloudEventJsonInputFormatter` in the MVC options:

```C#
public void ConfigureServices(IServiceCollection services)
{
    services.AddMvc(opts =>
    {
        opts.InputFormatters.Insert(0, new CloudEventJsonInputFormatter());
    });
}
```

This formatter will only intercept parameters where CloudEvent is the expected type.

You can then receive CloudEvent objects in controller actions:

```C#
[HttpPost("resource")]
public IActionResult ReceiveCloudEvent([FromBody] CloudEvent cloudEvent)
{
    return Ok();
}
```

### AMQP

The SDK provides extensions for the [AMQPNetLite](https://github.com/Azure/amqpnetlite) package.

For AMQP support, you must reference the `CloudNative.CloudEvents.Amqp` assembly and
reference the namespace in your code with `using CloudNative.CloudEvents.Amqp`.

The `AmqpCloudEventMessage` extends the `AMQPNetLite.Message` class. The constructor
allows creating a new AMQP message that holds a CloudEvent in either structured or binary
content mode.

```C#

var cloudEvent = new CloudEvent("com.example.myevent", new Uri("urn:example-com:mysource"))
{
    ContentType = new ContentType(MediaTypeNames.Application.Json),
    Data = JsonConvert.SerializeObject("hey there!")
};

var message = new AmqpCloudEventMessage( cloudEvent,
                                         ContentMode.Structured,
                                         new JsonEventFormatter());

```

For mapping a received `Message` to a CloudEvent, you can use the `ToCloudEvent()` method:

```C#
   var receivedCloudEvent = await message.ToCloudEvent();
```

## MQTT

The SDK provides extensions for the [MQTTnet](https://github.com/chkr1011/MQTTnet) package.
For MQTT support, you must reference the `CloudNative.CloudEvents.Mqtt` assembly and
reference the namespace in your code with `using CloudNative.CloudEvents.Mqtt`.

The `MqttCloudEventMessage` extends the `MqttApplicationMessage` class. The constructor
allows creating a new MQTT message that holds a CloudEvent in structured content mode.

```C#

var cloudEvent = new CloudEvent("com.example.myevent", new Uri("urn:example-com:mysource"))
{
    ContentType = new ContentType(MediaTypeNames.Application.Json),
    Data = JsonConvert.SerializeObject("hey there!")
};

var message = new MqttCloudEventMessage( cloudEvent,
                                         new JsonEventFormatter());

```

For mapping a received `MqttApplicationMessage` to a CloudEvent, you can use the
`ToCloudEvent()` method:

```C#
   var receivedCloudEvent = await message.ToCloudEvent();
```

## Community

- There are bi-weekly calls immediately following the [Serverless/CloudEvents
  call](https://github.com/cloudevents/spec#meeting-time) at
  9am PT (US Pacific). Which means they will typically start at 10am PT, but
  if the other call ends early then the SDK call will start early as well.
  See the [CloudEvents meeting minutes](https://docs.google.com/document/d/1OVF68rpuPK5shIHILK9JOqlZBbfe91RNzQ7u_P7YCDE/edit#)
  to determine which week will have the call.
- Slack: #cloudeventssdk channel under
  [CNCF's Slack workspace](https://slack.cncf.io/).
- Contact for additional information: Clemens Vasters (`@Clemens Vasters`
  on slack).

