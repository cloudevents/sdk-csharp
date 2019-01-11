![Build](https://ci.appveyor.com/api/projects/status/github/cloudevents/sdk-csharp?svg=true)

# sdk-csharp

.NET Standard 2.0 (C#) SDK for CloudEvents

The `CloudNative.CloudEvents` package provides utility methods and classes for creating, encoding, decoding, sending, and receiving CNCF CloudEvents.


## CloudEvent

The `CloudEvent` class reflects the event envelope defined by the CNCF CloudEvents specification.
It supports both version 0.1 and (yet to be finalized) version 0.2 of the CloudEvents specification,
even though the strongly typed API already reflects the 0.2 naming.

The default specification version is 0.2, you can override this by specifying the version explicitly: 
`new CloudEvent(CloudEventsSpecVersion.V0_1)`. The `SpecVersion` property also allows the 
version to be switched, meaning you can receive a 0.1 event, switch the version number, and forward
it as a 0.2 event. 


| 0.1  | 0.2 | Property name              | CLR type 
|-----------------------|-----------------------|----------------------------|----------
| eventID | id | `CloudEvent.Id` | `System.String` 
| eventType | type | `CloudEvent.Type` | `System.String` 
| cloudEventsVersion | specversion | `CloudEvent.SpecVersion` | `System.String` 
| eventTime | time | `CloudEvent.Time` | `System.DateTime` 
| source | source | `CloudEvent.Source` | `System.Uri` 
| schemaUrl | schemaurl | `CloudEvent.SchemaUrl` | `System.Uri`
| source | source | `CloudEvent.Source` | `System.Uri` 
| contentType | contenttype | `CloudEvent.ContentType` | `System.Net.Mime.ContentType` 
| data | data | `CloudEvent.Data` | `System.Object` 


The `CloudEvent.Data` property is `object` typed, and may hold any valid serializable
CLR type. The following types have special handling:

* `System.String`:  In binary content mode, strings are copied into the transport
  message payload body using UTF-8 encoding.                    
* `System.Byte[]`:  In binary content mode, byte array content is copied into the 
  message paylaod body without further transformation. 
* `System.Stream`:  In binary content mode, stream content is copied into the 
  message paylaod body without further transformation. 

Any other data type is transformed using the given event formatter for the operation
or the JSON formatter by default before being added to the transport payload body.

All extension attributes can reached via the `CloudEvent.GetAttributes()` method,
which returns the internal attribute collection. The internal collection performs 
all required validations.

If a CloudEvents-prefixed transport header, like an HTTP header, is `string` typed and the value is surrounded by '{' and '}' or '[' and ']', it is assumed to hold JSON content.


## Extensions

CloudEvent extensions are represented by implementations of the `ICloudEventExtension` 
interface. The SDK includes strongly typed implementations for all offical CloudEvents
extensions:

* `DistributedTracingExtension` for [distributed tracing](https://github.com/cloudevents/spec/blob/master/extensions/distributed-tracing.md)
* `SampledRateExtension` for [sampled rate](https://github.com/cloudevents/spec/blob/master/extensions/sampled-rate.md)
* `SequenceExtension` for [sequence](https://github.com/cloudevents/spec/blob/master/extensions/sequence.md)

Extension classes provides type-safe access to the extension attributes, and implement the 
required validations as well as type mappings. An extension object is always created as an 
independent entity and is then attached to a `CloudEvent` instance. Once attached, the 
extension object's attributes are merged into the `CloudEvent` instance.

This snippet shows how to create a `CloudEvent` with an extensions: 

``` C#
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
response), allow for extension instances to be added to the respective methods, and
the extensions are invoked in the mapping process, for instance to extract information
from headers that deviate from the CloudEvents default mapping.   

For instance, the server-side mapping for `HttpRequestMessage` allows adding 
extensions like this:

``` C#
public async Task<HttpResponseMessage> Run( HttpRequestMessage req, ILogger log)
{
    var cloudEvent = req.ToCloudEvent(new DistributedTracingExtension());
}
```

## Transport Bindings

This SDK helps with mapping CloudEvents from and to messages or transport frames of 
popular .NET clients, but without getting in the way of your application's choices of 
whether you want to send an event via HTTP PUT or POST or how you want to handle 
settlement of transfers in AMQP or MQTT. The transport binding classes and extensions 
therefore don't wrap the send and receive operations; you still use the native 
API of the respective library.

### HTTP - System.Net.Http.HttpClient

The .NET [`HttpClient`](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) uses
the [`HttpContent`](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpcontent) 
abstraction to wrap payloads for sending requests that carry entity bodies. 

This SDK provides a [`CloudEventContent`] class derived from `HttpContent` that can be
created from a `CloudEvent` instance, the desired `ContentMode` and an event formatter.

``` C#

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
`HttpClient`, for instance from a queue-like structure, the `CloudEvent` is created from
the response message object rather than the content object using the `ToCloudEvent()` 
extension method on `HttpResponseMessage`:

``` C#
var httpClient = new HttpClient();
// delete and receive message from top of the queue
var result = await httpClient.DeleteAsync(new Uri("https://example.com/queue/messages/top"));
if (HttpStatusCode.OK == result.StatusCode) {
   var receivedCloudEvent = await result.ToCloudEvent();
}
```
   

### HTTP - System.Net.HttpWebRequest

If your application uses the `HttpWebRequest` client, you can copy a CloudEvent into
the request structure in structured or binary mode:

``` C#

HttpWebRequest httpWebRequest = WebRequest.CreateHttp("https://example.com/target");
httpWebRequest.Method = "POST";
await httpWebRequest.CopyFromAsync(cloudEvent, ContentMode.Structured, new JsonEventFormatter());
```
 
Mind that the `Method` property must be set to an HTTP method that allows an entity body
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

``` C#
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

``` C#
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

### AMQP

The SDK provides extensions for the [AMQPNetLite](https://github.com/Azure/amqpnetlite) package. 

For AMQP support, you must reference the `CloudNative.CloudEvents.Mqtt` assembly and 
reference the namespace in your code with `using CloudNative.CloudEvents.Mqtt`.

The `AmqpCloudEventMessage` extends the `AMQPNetLite.Message` class. The constructor
allows creating a new AMQP message that holds a CloudEvent in either structured or binary 
content mode. 

``` C#

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

``` C#
   var receivedCloudEvent = await message.ToCloudEvent();
```
  


## MQTT 

The SDK provides extensions for the [MQTTnet](https://github.com/chkr1011/MQTTnet) package.
For MQTT support, you must reference the `CloudNative.CloudEvents.Mqtt` assembly and 
reference the namespace in your code with `using CloudNative.CloudEvents.Mqtt`.

The `MqttCloudEventMessage` extends the `MqttApplicationMessage` class. The constructor
allows creating a new MQTT message that holds a CloudEvent in structured content mode. 

``` C#

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

``` C#
   var receivedCloudEvent = await message.ToCloudEvent();
```
  


 
