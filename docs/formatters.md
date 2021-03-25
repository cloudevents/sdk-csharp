# Implementing an event formatter

The `CloudEventFormatter` abstract type in the C# SDK is an
augmentation of the [Event
Format](https://github.com/cloudevents/spec/blob/v1.0.1/spec.md#event-format)
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

When serializing data in binary mode messages, all formatters should
handle data provided as a `byte[]`, serializing it without any
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