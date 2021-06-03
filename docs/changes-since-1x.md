# Changes since version 1.x

Many aspects of the SDK have changed since the 1.x versions. Users
adopting 2.x should expect to rewrite some code and retest
thoroughly when migrating from 1.x.

The following sections are not exhaustive, but describe the most
important changes.

## Core package

The `CloudEvent` type constructor now only accepts the spec version
and initial extension attributes (with no values). Everything else
(type, ID, timestamp etc) must be set via properties or indexers.
(In particular, the timestamp and ID are no longer populated
automatically.) The spec version for an event is immutable once
constructed; everything else can be modified after construction.

The types used to specify attribute values must match the
corresponding attribute type exactly; there is no implicit
conversion available. For example, `cloudEvent["source"] =
"https://cloudevents.io";` will fail because the `source` attribute
is expected to be a URI.

The following are now fully-abstracted concepts, rather than
implicitly using more primitive types:

- Spec version (`CloudEventSpecVersion`)
- Attributes (`CloudEventAttribute`)
- Attribute types (`CloudEventAttributeType`)

The 1.x `CloudEventAttributes` class has now been removed, however -
the attributes are contained directly in a map within `CloudEvent`.

Timestamp attributes are now represented by `DateTimeOffset` instead
of `DateTime`, as this provides a more specific "timestamp" concept
with fewer ambiguities.

Extension attributes are no longer expected to implement an
interface (the old `ICloudEventExtension`). Instead,
`CloudEventAttribute` is used to represent all kinds of attribute,
and extensions are encouraged to be provided using C# extension
methods and static properties. See the [user
guide](guide.md#extension-attributes) for more details.

The distributed tracing extension attributes have been removed for
now, while their long-term future is discussed in the broader
CloudEvent ecosystem.

## Event formatters

`CloudEventFormatter` is now an abstract base class (compared with
the 1.x interface `ICloudEventFormatter`). Attribute encoding is no
longer part of the responsibility of a `CloudEventFormatter`, but
binary data encoding (and batch encoding where supported) *are* part
of the event formatter.

The core package no longer contains any event formatters; the
Json.NET-based event formatter is now in a separate package
(`CloudNative.CloudEvents.NewtonsoftJson`) to avoid an unnecessary
dependency. An alternative implementation based on System.Text.Json
is now available in the `CloudNative.CloudEvents.SystemTextJson`
package.

Event formatters no longer supports streams for data, but are
expected to handle strings and byte arrays, as well as supporting
any formatter-specific types (e.g. JSON objects for JSON
formatters). While each event formatter is still able to determine
its own approach to serialization (meaning that formatters aren't
really interchangable), the data responsiblities are more clearly
documented, and each formatter should provide details of its
serialization and deserialization algorithm.

## Protocol bindings

Protocol bindings now typically require a `CloudEventFormatter` for
all serialization and deserialization operations, as there's no
built-in formatter to use by default. (This sounds inconvenient, but
does make the dependency on a specific event format explicit.)

The method names have been made consistent as far as possible. See
[the protocol bindings implementation guide](bindings.md) for
details.
