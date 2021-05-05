# Usage guide

This guide provides a whistle-stop tour of .NET CloudEvents SDK. It
is not exhaustive by any means; please [file an
issue](https://github.com/cloudevents/sdk-csharp/issues) if you
would like to suggest a specific area for further documentation.

## NuGet packages

The CloudEvents SDK consists of a number of NuGet packages, to avoid
unnecessary dependencies. These packages are:

|NuGet package|Description|
|-|-|
|[CloudNative.CloudEvents](https://www.nuget.org/packages/CloudNative.CloudEvents)|Core SDK|
|[CloudNative.CloudEvents.Amqp](https://www.nuget.org/packages/CloudNative.CloudEvents.Amqp)|AMQP protocol binding using [AMQPNetLite](https://www.nuget.org/packages/AMQPNetLite)|
|[CloudNative.CloudEvents.AspNetCore](https://www.nuget.org/packages/CloudNative.CloudEvents.AspNetCore)|ASP.NET Core support for CloudEvents|
|[CloudNative.CloudEvents.Avro](https://www.nuget.org/packages/CloudNative.CloudEvents.Avro)|Avro event formatter using [Apache.Avro](https://www.nuget.org/packages/Apache.Avro)|
|[CloudNative.CloudEvents.Kafka](https://www.nuget.org/packages/CloudNative.CloudEvents.Kafka)|Kafka protocol binding using [Confluent.Kafka](https://www.nuget.org/packages/Confluent.Kafka)|
|[CloudNative.CloudEvents.Mqtt](https://www.nuget.org/packages/CloudNative.CloudEvents.Mqtt)|MQTT protocol binding using [MQTTnet](https://www.nuget.org/packages/MQTTnet)|
|[CloudNative.CloudEvents.NewtonsoftJson](https://www.nuget.org/packages/CloudNative.CloudEvents.NewtonsoftJson)|JSON event formatter using [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json)|
|[CloudNative.CloudEvents.System.Text.Json](https://www.nuget.org/packages/CloudNative.CloudEvents.SystemTextJson)|JSON event formatter using [System.Text.Json](https://www.nuget.org/packages/System.Text.Json)|

Note that protocol bindings for HTTP using `HttpRequestMessage`,
`HttpResponseMessage`, `HttpContent`, `HttpListenerRequest`,
`HttpListenerResponse` and `HttpWebRequest` are part of the core SDK.

## In-memory CloudEvent representation

The most important type in the CloudEvents SDK is the `CloudEvent`
type. This contains all the information about a CloudEvent,
including its *attributes* and *data*.

Attributes are effectively metadata about the CloudEvent. Each
attribute is represented by a `CloudEventAttribute` which is aware
of the attribute name, its kind (see below), its data type (as a
`CloudEventAttributeType`) and any constraints (such as whether it
can be present but empty).

There are three kinds of attributes:

- Required: these attributes are part of the CloudEvents
  specification, and are required on all valid CloudEvents.
- Optional: these attributes are part of the CloudEvents
  specification, but are not required to be present in order for
  a CloudEvent to be considered valid.
- Extension: these attributes are not formalized as part of the
  CloudEvents specification. The CloudEvents specification repository
  [includes descriptions of some extension
  attributes](https://github.com/cloudevents/spec/tree/v1.0.1/extensions)
  that may become standardized over time, but they are not
  considered part of the specification.
  
One attribute is handled differently to all others within the
.NET CloudEvents SDK: the `specversion` attribute. Once a
`CloudEvent` object has been created, its `specversion` cannot be
changed. Currently, only the 1.0 specification is supported anyway;
when new versions arise, we expect to provide a method to create a
new `CloudEvent` object from an existing one, but with a new version
(and with modified properties where appropriate). The specification
version can be specified explicitly in the CloudEvent constructor,
but otherwise defaults to 1.0.

The optional and required attributes can be accessed in three ways:

- Via specific properties, e.g. `cloudEvent.Id` or `cloudEvent.Time`
- Via the string-based indexer, e.g. `cloudEvent["id"]`
- Via the CloudEventAttribute-based indexer, e.g.
  `cloudEvent[myAttribute]`

Extension attributes do not have specific properties, so can only be
accessed via one of the indexers.

The value returned by the indexer (or accepted when calling the
setter) depends on the attribute type:

|CloudEvent attribute type|.NET type|
|-|-|
|String|System.String|
|Integer|System.Int32|
|Boolean|System.Boolean|
|Binary|System.Byte\[\]|
|URI|System.Uri|
|URI-Reference|System.Uri|
|Timestamp|System.DateTimeOffset|

When a value is set by the string-based indexer and the CloudEvent
isn't already aware of the attribute, it is assumed to be a
string-based extension attribute with no constraints.

The `CloudEvent.Data` property deserves special consideration, but
is best understood after reading about [protocol
bindings](#protocol-bindings) and [CloudEvent
formatters](#cloudevent-formatters). If you're already familiar with
those topics, [jump straight to data
considerations](#data-considerations).

## Extension attributes

Extension attributes can be specified without any values when a
CloudEvent is created. This is typically the case when using a
protocol binding to parse a transport message: if you're aware of
any extensions you *might* see in the CloudEvent, and want to use
them later, pass those extensions into the relevant method and the
CloudEvent will be created with them. This allows any extension
attribute values to be validated while the CloudEvent is being
parsed.

The CloudEvents SDK contains some predefined extension attributes in
the `CloudNative.CloudEvents.Extensions` namespace. The SDK exposes
these with the following pattern, which you are encouraged to follow
if you write your own extensions:

- Create a static class for all related extension attributes (e.g. the
  `sequence` and `sequencetype` extension attributes are both exposed
  via the `CloudNative.CloudEvents.Extension.Sequence` class)
- Create a static read-only property of type `CloudEventAttribute`
  for each extension attribute
- Create a static read-only property of type
  `IEnumerable<CloudEventAttribute>` called `AllAttributes`, typically
  implemented via a `ReadOnlyCollection<T>`. This makes it easy to
  pass "all the related extensions" into the CloudEvent constructor
  or protocol binding methods accepting
  `IEnumerable<CloudEventAttribute>`. It also makes it easy to combine
  multiple extension attributes using the LINQ `Concat` method
- Create extension methods to interact with CloudEvents, such as the
  `SetSequence(this CloudEvent cloudEvent, object value)` method
  in `Sequence`.

When fetching extension attribute values from a CloudEvent, if the
attribute type is not String, you *may* wish fetch the value by
attribute name rather than by the attribute. This allows you to
handle the case where the attribute value has been populated without
prior knowledge of the attribute, and defaulted to a String type. If
you know that the CloudEvent will always have been populated using
the correct extension attribute, this is unnecessary complexity -
but if you need to work with arbitrary CloudEvent instances, it can
be more flexible.

## Protocol bindings

*Protocol bindings* are used to transport CloudEvents on specific
protocols (e.g. HTTP or Kafka). Each protocol binding has its own
methods, typically extracting a CloudEvent from an existing
transport message, or creating/populating a transport message with
an existing CloudEvent.

Protocol bindings work with [CloudEvent formatters](#event-formatters) to
determine exactly how the CloudEvent is represented within any
given transport message.

Due to differences between protocols, there's no abstract base class
or interface for protocol bindings. However, protocol bindings are
encouraged to follow certain conventions to provide a reasonably
consistent experience across protocols. See the
[bindings.md](protocol bindings implementation guide) for more
details of these conventions.

The following table summarizes the protocol bindings available:

|Protocol binding|Namespace|Types|
|-|-|-|
|HTTP (built-in)|CloudNative.CloudEvents.Http|HttpClientExtensions, HttpContentExtensions, HttpListenerExtensions, HttpWebExtensions|
|HTTP (ASP.NET Core)|CloudNative.CloudEvents.AspNetCore|HttpRequestExtensions, CloudEventJsonInputFormatter|
|AMQP|CloudNative.CloudEvents.Amqp|AmqpClientExtensions|
|Kafka|CloudNative.CloudEvents.Kafka|KafkaClientExtensions|
|MQTT|CloudNative.CloudEvents.Mqtt|MqttClientExtensions|

### Content modes and batches

Most protocol bindings support two *content modes*:

- In *structured mode*, all the CloudEvent information is placed in the protocol message body,
  with the exact format governed by the [CloudEvent format](#cloudevent-formatters) in use. The
  content type of the message indicates that the message represents a CloudEvent.
- In *binary mode*, the CloudEvent data is placed in the protocol message body,
  but the attributes of the CloudEvent are placed in the protocol metadata (e.g. HTTP headers).
  In this case, the content type of the message is the content type of the data of the CloudEvent.

Protocol bindings typically expose this option via a parameter of type `ContentMode` when serializing
a CloudEvent into a protocol message. Deserialization is typically transparent, using the appropriate
content mode based on the content type of the message being read.

Some protocol bindings (e.g. HTTP) also support a *batch mode*. This
is like structured mode, in that all the CloudEvent information is
placed in the message body, but the message body can contain any
number of CloudEvents (including none). Where a protocol binding
supports batch mode, batch-specific methods are typically provided.

## CloudEvent formatters

For structured mode (and batch mode) messages, the way in which the
CloudEvent (or batch of CloudEvents) is represented is determined by
the *CloudEvent format* being used. In the .NET SDK, a CloudEvent
format is represented by concrete types derived from the
`CloudEventFormatter` abstract base class. Two formats are supported:

- JSON, via the `JsonEventFormatter` types in the `CloudNative.CloudEvents.SystemTextJson` and
  `CloudNative.CloudEvents.NewtonsoftJson` packages
- Avro, via the `AvroEventFormatter` type in the `CloudNative.CloudEvents.Avro` package

Note that a `CloudEventFormatter` in the .NET SDK has more
responsibility than a CloudEvent format in the specification, in
that it is *also* responsible for serializing the data of the event
in both structured and binary modes. For example, the
`JsonEventFormatter` implementations will serialize objects as JSON
objects. See the [Data considerations](#data-considerations) section for more details.

There are two different JSON implementations as they use different
JSON APIs for implementation purposes. This can affect the
serialized data, as each underlying JSON API has its own set of
attributes and settings governing the serialization and
deserialization. Both are provided separately from the core
CloudNative.CloudEvents package to avoid unnecessary dependencies.
We would recommend using a single JSON implementation across an
application where possible, for simplicity and consistency.

## Sample code for protocol bindings and event formatters

Sample code for creating a CloudEvent and using it to populate an
`HttpRequestMessage` (typically for sending with `HttpClient`):

<!-- Sample: PopulateHttpRequestMessage -->
```csharp
CloudEvent cloudEvent = new CloudEvent
{
    Id = "event-id",
    Type = "event-type",
    Source = new Uri("https://cloudevents.io/"),
    Time = DateTimeOffset.UtcNow,
    DataContentType = "text/plain",
    Data = "This is CloudEvent data"
};

CloudEventFormatter formatter = new JsonEventFormatter();
HttpRequestMessage request = new HttpRequestMessage
{
    Method = HttpMethod.Post,
    Content = cloudEvent.ToHttpContent(ContentMode.Structured, formatter)
};
```

`ToHttpContent` is an extension method requiring a `using` directive of

```csharp
using CloudNative.CloudEvents.Http;
```

Sample code for consuming a CloudEvent within an ASP.NET Core `HttpRequest`:

<!-- Sample: ParseHttpRequest -->
```csharp
CloudEventFormatter formatter = new JsonEventFormatter();
CloudEvent cloudEvent = await request.ToCloudEventAsync(formatter);
```

`ToCloudEventAsync` is an extension method requiring a `using` directive of

```csharp
using CloudNative.CloudEvents.AspNetCore;
```

## Data considerations

The `CloudEvent.Data` property is of type `System.Object` and can
hold any value. However, outside unit testing, CloudEvents are
almost always serialized using a protocol binding and event
formatter, and then deserialized later. When creating a CloudEvent
you need to consider the representation you want the CloudEvent data
to take when "on the wire". Likewise when you parse a CloudEvent
from a transport message, you need to be aware of the limitations of
the protocol binding and event formatter you're using, in terms of
how data is deserialized.

As a concrete example, suppose you have a class `GameResult`
representing the result of a single game, and you wish to create a
CloudEvent for this result, using a JSON representation of the data
in an HTTP request. The class might look like this:

<!-- Sample: GameResult -->

```csharp
public class GameResult
{
    [JsonProperty("playerId")]
    public string PlayerId { get; set; }
    
    [JsonProperty("gameId")]
    public string GameId { get; set; }
    
    [JsonProperty("score")]
    public int Score { get; set; }
}
```

Using the `JsonEventFormatter` from the
`CloudNative.CloudEvents.NewtonsoftJson` package, including an
instance of `GameResult` as the data of a CloudEvent and then using
that as the content of an `HttpRequestMessage` is simple:

<!-- Sample: SerializeGameResult -->

```csharp
var result = new GameResult
{
    PlayerId = "player1",
    GameId = "game1",
    Score = 200
};
var cloudEvent = new CloudEvent
{
    Id = "result-1",
    Type = "game.played.v1",
    Source = new Uri("https://cloudevents.io/"),
    Time = DateTimeOffset.UtcNow,
    DataContentType = "application/json",
    Data = result
};
var formatter = new JsonEventFormatter();
var request = new HttpRequestMessage
{
    Method = HttpMethod.Post,
    Content = cloudEvent.ToHttpContent(ContentMode.Binary, formatter)
};
```

The `GameResult` object is automatically serialized as JSON in the
HTTP request.

When the CloudEvent is deserialized at the receiving side, however,
it's a little more complex. The event formatter can use the content
type of "application/json" to detect that this is JSON, but it
doesn't know to deserialize it as a `GameResult`. Instead, it
deserializes it as a `JToken` (in this case a `JObject`, as the
content represents a JSON object). The calling code then has to use
normal Json.NET deserialization to convert the `JObject` stored in
`CloudEvent.Data` into a `GameResult`:

<!-- Sample: DeserializeGameResult -->

```csharp
CloudEventFormatter formatter = new JsonEventFormatter();
CloudEvent cloudEvent = await request.ToCloudEventAsync(formatter);
JObject dataAsJObject = (JObject) cloudEvent.Data;
GameResult result = dataAsJObject.ToObject<GameResult>();
```

A future CloudEvent formatter could be written to know what type of
data to expect and deserialize it directly; that formatter could
even be a generic class derived from the existing
`JsonEventFormatter`. The `JObject` behavior is particular to
`JsonEventFormatter` - but the important point is that you need to
be aware of what the event formatter you're using is capable of.
Every event formatter should carefully document how it handles data,
both for serialization and deserialization purposes.
