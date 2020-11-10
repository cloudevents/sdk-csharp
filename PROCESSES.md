# Release processes

(This file aims to document the release process from 2.0 beta releases onwards.)

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
  (e.g. version history) and the version number change.
- The maintainer who creates and merges this change is also (by default) responsible for manually creating
  the GitHub release and (automatically) a corresponding tag. See below for the format of these.
- NuGet packages are automatically created and pushed when the release is created.

## Beta period

During the 2.0 beta period, it's helpful to use project references
between the "satellite" packages (e.g.
CloudNative.CloudEvents.AspNetCore) and the central SDK package
(CloudNative.CloudEvents). This requires that all packages are
released together, to avoid (for example) a satellite package being
released with a dependency on an unreleased feature in the SDK
package.

To facilitate this:

- Individual csproj files do not specify a version
- The [Directory.Build.props](src/Directory.Build.props) file has a `<Version>` element
  specifying the version of all packages

A single GitHub release (and tag) will be created for each beta release, to cover all packages.

- Example tag name: "CloudNative.CloudEvents.All-2.0.0-beta.1"
- Example release title: "All packages pre-release version 2.0.0-beta.1"

We are expecting to make breaking changes during the beta period. These will be listed in [HISTORY.md](HISTORY.md).

## 2.0.0 GA

Before any 2.0.0 packages are released, project references will be
converted into versioned package references, to avoid the "unreleased
dependency" problem mentioned earlier. At that point, the version
number will be removed from Directory.Build.props

From there onwards, each package needs a separate GitHub release.
The GitHub action to push the NuGet packages will use the tag name
to determine the package to push, so the format must exactly match
the example below. (The release title doesn't need to match exactly,
although it's better if it does.)

- Example tag name: "CloudNative.CloudEvents.AspNetCore-2.1.0"
- Example release title: "Release CloudNative.CloudEvents.AspNetCore version 2.1.0"

One corollary of this is that if (say)
CloudNative.CloudEvents.AspNetCore needs a new feature in
CloudNative.CloudEvents in order to expose a new feature itself,
four commits would be required:

- Code change in CloudNative.CloudEvents
- Release commit for CloudNative.CloudEvents (and then release)
- Code change and dependency update in CloudNative.CloudEvents.AspNetCore
- Release commit for CloudNative.CloudEvents.AspNetCore (and then release)
