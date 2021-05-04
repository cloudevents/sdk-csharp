# SDK documentation

**Note: all of this documentation is specific to versions 2.0-beta.2 and onwards**

This directory contains documentation on:

- [Usage guide](guide.md) (this is the most appropriate starting point for most
  developers if they simply plan on *using* the CloudEvents SDK)
- [Changes since version 1.x of the CloudNative.CloudEvents packages](changes-since-1x.md)
- Implementing new [event formats](formatters.md) and [protocol bindings](bindings.md)

## Implementation utility classes

The `CloudNative.CloudEvents.Core` namespace contains utility
classes which are generally helpful when implementing [protocol
bindings](bindings.md) or [event formatters](formatters.md) but are
not expected to be used by code which only creates or consumes
CloudEvents.

The classes in this namespace are static classes, but the methods
are deliberately not extension methods. This avoids the methods from
being suggested to non-implementation code.
