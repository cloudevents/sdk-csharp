# Version history (from 2.0)

## 2.3.0 (2022-05-31)

- Bug fix: BinaryDataUtilities.AsArray misbehavior with array segments ([#209](https://github.com/cloudevents/sdk-csharp/issues/209))
- Bug fix: Links within XML documentation corrected after spec repo change
- Feature: Reject "data" as a context attribute name (spec has been clarified for this)
- Feature: Support Data content type inference in event formatters
  - The JsonEventFormatter classes infer "application/json" for all data
- Feature: CloudNative.CloudEvents.Protobuf is now GA (same version as other packages)

## 2.2.0 (2022-02-02)

- Bug fix: the "source" attribute is now validated to be non-empty
- Bug fix: the JSON event formatters comply with the clarified JSON event format spec
- Dependency: Apache.Avro dependency updated to 1.11.0
- Feature: New package CloudNative.CloudEvents.Protobuf, released as 2.0.0-beta.1

## 2.1.1 (2021-07-21)

Bug fix ([#177](https://github.com/cloudevents/sdk-csharp/pull/177)): dependency on the
`Nullable` package was not declared with `PrivateAssets=all`,
leading to that showing up as a dependency. This would break users
who explicitly have a dependency on an older version of `Nullable`.

This fix shouldn't break anyone, as far as we're aware.

## 2.1.0 (2021-07-14)

New features:

- Nullable reference type annotations ([#170](https://github.com/cloudevents/sdk-csharp/issues/170))
- More batch support for HttpListener ([#166](https://github.com/cloudevents/sdk-csharp/issues/166))
- CloudEvent.CopyToHttpResponse extension method (part of [#148](https://github.com/cloudevents/sdk-csharp/issues/148))

Other improvements:

- More informative error message for non-CE HTTP requests ([#165](https://github.com/cloudevents/sdk-csharp/issues/165))

Bug fixes:

- Various small XML docs typos ([commit](https://github.com/cloudevents/sdk-csharp/commit/626089ea1e5bb6741868aeb389cb4d314e9e72ed))
- Don't set the content type to JSON in HttpListener when it's not set in the CloudEvent ([commit](https://github.com/cloudevents/sdk-csharp/commit/18e13635fe333b24432ac34d9ef040cd962d1063))

## 2.0.0 (2021-06-15)

Initial GA release for 2.x. See the ["changes since 1.x"
document](changes-since-1x.md) for more detail around what has
changed.
