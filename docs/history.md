# Version history (from 2.0)

## 2.1.1 (2021-07-21)

Bug fix ([#77](https://github.com/cloudevents/sdk-csharp/pull/177)): dependency on the
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
