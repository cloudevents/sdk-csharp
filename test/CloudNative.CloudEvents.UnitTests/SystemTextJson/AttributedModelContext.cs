// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace CloudNative.CloudEvents.SystemTextJson.UnitTests;

/// <summary>
/// A source-generated <see cref="JsonSerializerContext"/> that supports <see cref="AttributedModel"/>.
/// </summary>
[JsonSerializable(typeof(AttributedModel))]
internal sealed partial class AttributedModelContext : JsonSerializerContext
{
}

/// <summary>
/// A source-generated <see cref="JsonSerializerContext"/> that does not support <see cref="AttributedModel"/>.
/// </summary>
[JsonSerializable(typeof(int))]
internal sealed partial class EmptyContext : JsonSerializerContext
{
}
