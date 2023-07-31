# Version history (from 2.0)

## 2.7.0 (2023-07-31)

- Add the ability to specify a custom serializer for Avro.
  Fixes ([#261](https://github.com/cloudevents/sdk-csharp/issues/261)).

## 2.6.0 (2023-03-07)

- Dependencies: system-level dependencies updated
- The NuGet package now uses PackageLicenseExpression (but still
  includes the licence file as well).
  Fixes ([#252](https://github.com/cloudevents/sdk-csharp/issues/252)).
- Regenerated protobuf schema using the original filename of
  cloudevents.proto instead of ProtoSchema.proto. An additional
  ProtoSchemaReflection class has been added purely for compatibility.
  Fixes ([#256](https://github.com/cloudevents/sdk-csharp/issues/256)).

## 2.5.1 (2022-11-10)

- Dependencies: update dependencies in CloudNative.CloudEvents.Avro
  - Add explicit dependency on Newtonsoft.Json 13.0.1 to avoid
    transitive dependency on a version containing vulnerabilities
  - Update Apache.Avro to 1.11.1

No APIs have changed. This is a patch release as the dependency
changes are very minor (but necessary to avoid vulnerabilities).

## 2.5.0 (2022-10-17)

- Dependencies: update dependencies in CloudNative.CloudEvents.AspNetCore:
  - Remove dependency on Microsoft.AspNetCore.Mvc.Core (as we don't use it)
  - Update dependency on Microsoft.AspNetCore.Http to 2.1.34
  - Explicitly add dependency on System.Text.Encodings.Web 6.0.0 to avoid security issue in older version

No APIs have changed, but this is a minor release due to the significant dependency changes.

## 2.4.0 (2022-09-08)

- Feature: Implement underscore prefixes for AMQP (see below for more details) ([#236](https://github.com/cloudevents/sdk-csharp/pull/236))
- Feature: Allow empty payloads in Kafka ([#224](https://github.com/cloudevents/sdk-csharp/pull/224))
- Feature: Implement conversions to and from JObject/JsonElement in JsonEventFormatter ([#234](https://github.com/cloudevents/sdk-csharp/pull/234), part of [#231](https://github.com/cloudevents/sdk-csharp/issues/231))
- Bug fix: Observe JSON serializer options in JsonEventFormat ([#226](https://github.com/cloudevents/sdk-csharp/pull/226), fixes [#225](https://github.com/cloudevents/sdk-csharp/issues/225))
- Bug fix: Put AvroEventFormatter in the right namespace ([#220](https://github.com/cloudevents/sdk-csharp/pull/220), fixes [#219](https://github.com/cloudevents/sdk-csharp/issues/219))
- Bug fix: Use content headers when parsing HTTP requests/responses ([#222](https://github.com/cloudevents/sdk-csharp/pull/222), fixes [#221](https://github.com/cloudevents/sdk-csharp/issues/221))
- Bug fix: Perform release builds with ContinuousIntegrationBuild=true ([#223](https://github.com/cloudevents/sdk-csharp/pull/223), fixes [#175](https://github.com/cloudevents/sdk-csharp/issues/175))

The AMQP change is significant: the [AMQP CloudEvent binding
specification](https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/amqp-protocol-binding.md)
now recommends using `cloudEvents_` instead of `cloudEvents:` as the
prefix. The change in this release allows both prefixes to be parsed, and
there are now three conversion extension methods:

- `ToAmqpMessageWithUnderscorePrefix` - always uses `cloudEvents_`
- `ToAmqpMessageWithColonPrefix` - always uses `cloudEvents:`
- `ToAmqpMessage` - currently uses `cloudEvents:` but will use `cloudEvents_` in a future release (planned for a March 2023 release)

The intention is to effectively give six months notice of a change
in the default behavior.

## 2.3.1 (2022-06-29)

- Bug fix: ignore the charset when determining the content type for decoding JSON ([#216](https://github.com/cloudevents/sdk-csharp/issues/216))
- Bug fix: make the NuGet package deterministic ([#175](https://github.com/cloudevents/sdk-csharp/issues/175))

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
