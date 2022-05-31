# Implementing an event formatter

The `CloudEventFormatter` abstract type in the C# SDK is an
augmentation of the [Event
Format](https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md#event-format)
concept in the specification.

Strictly speaking, CloudEvent data is simply a sequence of bytes. In
practical terms, it's useful to be able to store any object
reference in the `CloudEvent.Data` property, leaving a
`CloudEventFormatter` to perform serialization and deserialization
when requested.

This means that `CloudEventFormatter` implementations need to be
aware of all content modes (binary, structured and batch) and
document how they handle data of various types. A
`CloudEventFormatter` implementation *may* implement only a subset
of content modes, but should document this very clearly. (Note:
batch content mode is not currently implemented in the SDK.)

## Data serialization and deserialization

When serializing data in binary mode messages, general purpose formatters
should handle data provided as a `byte[]`, serializing it without any
modification. Formatters are also encouraged to support serializing
strings in the obvious way (obeying any character encoding indicated
in the `datacontenttype` attribute).

When deserializing data in binary mode messages, event formatters
may use the content type to determine the in-memory object type to
deserialize to. For example, a JSON formatter may decode data in a
message with a content type of "application/json" to a JSON API type
(such as `Newtonsoft.Json.Linq.JToken`). Formatters are encouraged
to deserialize data with a content type beginning `text/` (and which
don't otherwise have a special meaning to the formatter) as
strings, obeying any character encoding indicated in the content
type. If the content type is unknown to the formatter, the data
should be populated in the `CloudEvent` as a simple byte array.

When serializing and deserializing data in a structured mode
message, an event formatter should follow the rules of the event
format it is implementing. The event formatter should be as
consistent as is reasonably possible in terms of its handling of
binary mode data and structured mode data, however. In particular, a
well-designed event format should usually not be restricted to any
specific data type, so any data that can be serialized in a binary
mode message should be serializable in a structured mode message
too.

Inconsistencies may still arise, when the structured message
contains more information about the original data than the
corresponding binary message. For example, an event format may use a
different serialization format for text and binary data, allowing
string and byte arrays to be serialized and then deserialized
seamlessly even if the content type is unknown to the formatter.
However, a binary mode messages serialized from the same data string may
lose that distinction, resulting in a `Data` property with a byte
array reference rather than a string, if nothing within the content
type indicates that the data is text.

Event formatters should document their behavior clearly. While this
doesn't allow `CloudEventFormatter` instances to be used
interchangably, it at least provides consumers with some certainty
around what they can expect for a specific formatter.

### General purpose vs single-type event formatters

The above description of data handling is designed as guidance for
general purpose event formatters, which should be able to handle any
kind of CloudEvent data with some reasonable (and well-documented)
behavior.

CloudEvent formatters can also be designed to be "single-type",
explicitly only handling a single type of CloudEvent data, known 
as the *target type* of the formatter. These are typically generic
types, where the target type is expressed as the type argument. For
example, both of the built-in JSON formatters have a general purpose
formatter (`JsonEventFormatter`) and a single-type formatter
(`JsonEventFormatter<T>`).

Single-type formatters should still support CloudEvents *without*
any data (omitting any data when serializing, and deserializing to a
CloudEvent with a null `Data` property) but may expect that any data
that *is* provided is expected to be of their target type, and
expressed in an appropriate format, without taking note of the data
content type. For example, `JsonEventFormatter<PubSubMessage>` would
throw an `IllegalCastException` if it is asked to serialize a
CloudEvent with a `Data` property referring to an instance of
`StorageEvent`.

## Validation

Formatter implementations should validate references documented as
being non-null, and additionally perform CloudEvent validation on:

- Any `CloudEvent`s returned by the formatter from
  `DecodeStructuredModeMessage` or `DecodeStructuredModeMessageAsync`
- The `CloudEvent` accepted in `EncodeBinaryModeEventData` or
  `EncodeStructuredModeMessage`

Validation should be performed using the `Validation.CheckCloudEventArgument`
method, so that an appropriate `ArgumentException` is thrown.

The formatter should *not* perform validation on the `CloudEvent`
accepted in `DecodeBinaryModeEventData`, beyond asserting that the
argument is not null. This is typically called by a protocol binding
which should perform validation itself later.

## Data content type inference

Some event formats (e.g. JSON) infer the data content type from the
actual data provided. In the C# SDK, this is implemented via the
`CloudEventFormatter` methods `GetOrInferDataContentType` and
`InferDataContentType`. The first of these is primarily a
convenience method to be called by bindings; the second may be
overridden by any formatter implementation that wishes to infer
a data content type when one is not specified. Implementations *can*
override `GetOrInferDataContentType` if they have unusual
requirements, but the default implementation is usually sufficient.

The base implementation of `InferDataContentType` always returns
null; this means that no content type is inferred by default.
