# SDK documentation

This directory contains documentation on:

- Using the SDK as a consumer
- Implementing new event formats and protocol bindings

## Implementation utility classes

The `CloudNative.CloudEvents.Core` namespace contains utility
classes which are generally helpful when implementing [protocol
bindings](bindings.md) or [event formatters](formatters.md) but are
not expected to be used by code which only creates or consumes
CloudEvents.

The classes in this namespace are static classes, but the methods
are deliberately not extension methods. This avoids the methods from
being suggested to non-implementation code.
