# Release processes

(This file aims to document the release process from 2.0 releases onwards.)

## General

- Packages are released via GitHub actions, with a securely configured NuGet API key.
- Packages are pushed to https://nuget.org; there are no other NuGet package repositories involved.
- Other than "while a release is pending", the version in the repository is the "most recently released"
  version of the code.
- Packages are only created and pushed based on a GitHub release
  (so there should never be a package pushed that we can't later find the right source code).
- The commit that is tagged for each release should only contain changes to the version number and
  documentation (e.g. the version history). Code changes should appear in previous commits.
- A pull request may contain commits that affect the code and also a commit for a release; the release
  commit must be the final commit within the pull request.

The normal steps are expected to be:

- All contributors make code changes and get them approved and merged as normal
- The maintainers agree on the need for a new release (either on GitHub or externally)
- A PR is created and merged by a maintainer (with normal approval) that contains documentation changes
  (e.g. [version history](docs/history.md)) and the version number change.
- The maintainer who creates and merges this change is also (by default) responsible for manually creating
  the GitHub release and (automatically) a corresponding tag. See below for the format of these.
- NuGet packages are automatically created and pushed when the release is created.
- After a minor or major release, the `PackageValidationBaselineVersion` is updated
  to the new version number as the baseline for a future release to be compatible with.

## Stable package versioning

It's helpful to use project references between the "satellite"
packages (e.g. CloudNative.CloudEvents.AspNetCore) and the central
SDK package (CloudNative.CloudEvents). This requires that all
packages are released together, to avoid (for example) a satellite
package being released with a dependency on an unreleased feature in
the SDK package. While this may mean some packages are re-released
without any actual changes other than their dependency on the core
package, it makes versioning problems much less likely, and also
acts as encouragement to use the latest version of the core package.

Within this repository, this is achieved by the following mechanisms:

- Individual csproj files do not specify a version
- The [Directory.Build.props](src/Directory.Build.props) file has a `<Version>` element
  specifying the version of all packages

A single GitHub release (and tag) will be created for each beta release, to cover all packages.

- Example tag name: "CloudNative.CloudEvents.All-2.0.0"
- Example release title: "All packages version 2.0.0"

## New / unstable package versioning

New packages are introduced with alpha and beta versions as would
normally be expected. To avoid "chasing" the current stable release
version, these will be labeled using 1.0.0 as the notional stable
version, before synchronizing with the current "umbrella" stable
version when it becomes stable. (This requires a new release for all
packages, for the same reasons given above. This is not expected to
be a frequent occurence, however.)

For example, a new package might have the following version sequence:

- 1.0.0-alpha.1
- 1.0.0-beta.1
- 1.0.0-beta.2
- 2.1.0

While a package is in pre-release, it should specify the Version
element in its project file, which will effectively override the
"default" stable version.

<!-- TODO: work out how to do multiple pre-releases of a new package
without worrying about the issue of depending on unreleased parts of
core. It may well not come up, or we can just handle it really
carefully. That's probably easier than trying to generalize through
infrastructure. -->
