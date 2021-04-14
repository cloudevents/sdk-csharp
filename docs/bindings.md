# Implementing a protocol binding

From the [specification](https://github.com/cloudevents/spec/blob/v1.0.1/spec.md#protocol-binding):

> A protocol binding describes how events are sent and received over a given protocol.

In SDK terms, this usually means methods converting between
the SDK `CloudEvent` type and protocol-specific request/response
types.

This document describes suggested conventions for implementing a new
protocol binding. These are only conventions rather than
requirements, but following them will promote consistency, leading
to a more predictable user experience.

In this document, it is assumed that the binding has the concepts
of "structured" or "binary" content modes. Readers should
make appropriate choices where this is not the case.

It is encouraged to provide conversions in both directions where
this makes sense, but that won't be the case in all situations.

## Conversions to CloudEvent

Provide extension methods on the protocol-specific message types,
with the following pattern, demonstrated with an imaginary
`ProtocolMessage` type (such as `HttpRequestMessage` or
`HttpResponseMessage`):

```
public static bool IsCloudEvent(this ProtocolMessage message);

public static CloudEvent ToCloudEvent(
    this ProtocolMessage message,
    CloudEventFormatter formatter,
    params CloudEventAtribute[] extensionAttributes);

public static CloudEvent ToCloudEvent(
    this ProtocolMessage message,
    CloudEventFormatter formatter,
    IEnumerable<CloudEventAtribute> extensionAttributes);
```

Where message content can only be read asynchronously, async methods
(with a type `Task<CloudEvent>` or `ValueTask<CloudEvent>` and an
`Async` method name suffix) should be provided instead.

The reason for providing two overloads is for caller convenience to
cover three scenarios:

- No extension attribute (call uses the `params` overload)
- A single extension attribute, or multiple known individual ones
  (call uses the `params` overload)
- An immutable collection of extension attributes, either provided by
  the SDK (e.g. `Sampling.AllAttriutes`) or constructed once by the
  caller.

### IsCloudEvent

The `IsCloudEvent` method should provide a reasonable indication of
whether or not the message contains a CloudEvent, but should not
perform full decoding and validation. Typically it checks metadata
for one of:

- A content type beginning with "application/cloudevents" (for
  structured content mode events) but *not* beginning with
  "application/cloudevents-batch" (even if the binding does not
  currently support batch events)
- The presence of a CloudEvents specification version header (for
  binary mode events)

### ToCloudEvent methods

Typically multiple overloads of `ToCloudEvent` will resolve to a
single internal implementation. The implementation should use the
following pseudo-code as structure:

- Validate that `message` and `formatter` are non-null. (The
  `extensionAttributes` parameter may be null, which is equivalent
  to an empty collection.)
- Determine the content mode of the message
- For a structured content mode message, delegate to the
  `CloudEventFormatter` specified in the `formatter` parameter,
  using its `DecodeStructuredModeMessage` method. The formatter
  should take care of validating the resulting event.
- For binary content mode message:
  - Determine the specification version based on metadata. (The
    transport *may* specify a default specification, but typically
    this is required metadata which serves to check that the message
    is intended to represent a CloudEvent.) Use
    `CloudEventsSpecVersion.FromVersionId` and check for a null return
    value.
  - Construct a `CloudEvent` instance with the given specification
    version and extension attributes.
  - Look for all CloudEvent attributes within metadata, and populate
    the attributes within the `CloudEvent` instance.
  - If the message contains content, call the
    `formatter.DecodeBinaryModeEventData` method to populate the
    `CloudEvent.Data` property appropriately.
  - Return the result of `Validation.CheckCloudEventArgument` which
    will validate the event, and either return the original reference if
    the event is valid, or throw an appropriate `ArgumentException`
    otherwise.

## Conversions from CloudEvent to protocol message types

There are two patterns here, depending on whether it's appropriate
for the conversion to construct a new instance or whether it should
populate an existing instance:

```csharp
public static ProtocolMessage ToProtocolMessage(
    this CloudEvent cloudEvent,
    ContentMode contentMode,
    CloudEventFormatter formatter);

public static void CopyToProtocolMessage(
    this CloudEvent cloudEvent,
    ProtocolMessage destination,
    ContentMode contentMode,
    CloudEventFormatter formatter);
```

Here the `ProtocolMessage` part of the message name varies by target
type. Where the type involved is sufficiently clear, that can be
used directly. If the type involved only has a generic name such as
`Message` or `Request`, it is useful to qualify it with the protocol
name. Examples:

```csharp
// HttpContent is already unambiguous.
public static HttpContent ToHttpContent(
    this CloudEvent cloudEvent,
    ContentMode contentMode,
    CloudEventFormatter formatter);

// Message is ambiguous, so clarify it in the method name.
public static Message ToAmqpMessage(
    this CloudEvent cloudEvent,
    ContentMode contentMode,
    CloudEventFormatter formatter);
```

Again, where asynchrony is required, make the methods async with
appropriate changes to the return type and method name.

Even if only a single content mode is currently supported, the
inclusion of the `contentMode` parameter provides consistency and a
seamless path for change later. If the protocol inherently means
that `contentMode` is meaningless (so can *never* be supported), it
can be omitted.

Any additional parameters required for the conversion should be
added after the `formatter` parameter.

The conversion should follow the following steps of pseudo-code:

- Parameter validation (which may be completed in any order):
  - `cloudEvent` and `formatter` should be non-null
    (`CheckCloudEventArgument` will validate this for the `cloudEvent` parameter)
  - In a `CopyTo...` method, `destination` should be non-null
  - The `contentMode` should be a known, supported value
  - Call `Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent))`
    for validation of the original CloudEvent.
- For structured mode encoding:
  - Call `formatter.EncodeStructuredModeMessage` to encode
    the CloudEvent and indicate the resulting content type
    for the protocol message.
- For binary mode encoding:
  - Call `formatter.EncodeBinaryModeEventData` to encode
    the data within the CloudEvent
  - Populate metadata in the message from the attributes in the
    CloudEvent.
- For `To...` methods, return the resulting protocol message.
  This must not be null. (`CopyTo...` messages do not return
  anything.)

Note that depending on the protocol binding, when encoding a CloudEvent
in binary mode, it *may* still be worth populating metadata in the
message from the CloudEvent attributes.

## Batch conversions

Protocol bindings that support batch conversions should introduce
equivalent batch methods:

```
public static bool IsCloudEventBatch(this ProtocolMessage message);

public static IReadOnlyList<CloudEvent> ToCloudEventBatch(
    this ProtocolMessage message,
    CloudEventFormatter formatter,
    params CloudEventAtribute[] extensionAttributes);

public static IReadOnlyList<CloudEvent> ToCloudEventBatch(
    this ProtocolMessage message,
    CloudEventFormatter formatter,
    IEnumerable<CloudEventAtribute> extensionAttributes);

public static ProtocolMessage ToProtocolMessage(
    this IReadOnlyList<CloudEvent> cloudEvents,
    CloudEventFormatter formatter);

public static void CopyToProtocolMessage(
    this IReadOnlyList<CloudEvent> cloudEvents,
    ProtocolMessage destination,
    CloudEventFormatter formatter);
```

Note that no `ContentMode` is specified when converting to protocol
messages, as there is no ambiguity.
