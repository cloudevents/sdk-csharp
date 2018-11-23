# sdk-csharp
.NET Standard 2.0 (C#) SDK for CloudEvents

The `CloudNative.CloudEvents` package provides utility methods and classes for creating, encoding, decoding, sending, and receiving CNCF CloudEvents.


## CloudEvent

The `CloudEvent` class reflects the event envelope defined by the CNCF CloudEvents specification.

| CloudEvents attribute | Property name              | CLR type 
|-----------------------|----------------------------|----------
| id                    | `CloudEvent.Id`           | `System.String`   
| type                  | `CloudEvent.Type`         | `System.String`   
| specversion           | `CloudEvent.SpecVersion` | `System.String`   
| time                  | `CloudEvent.Time`         | `System.DateTime`   
| source                | `CloudEvent.Source`      | `System.Uri`   
| schemaurl             | `CloudEvent.SchemaUrl`   | `System.Uri`
| source                | `CloudEvent.Source`      | `System.Uri`      
| contenttype           | `CloudEvent.ContentType` | `System.Net.Mime.ContentType`   
| data                  | `CloudEvent.Data`        | `System.Object`      


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

CloudEvent extensions are reflected by implementations of the `ICloudEventExtension` interface.

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


