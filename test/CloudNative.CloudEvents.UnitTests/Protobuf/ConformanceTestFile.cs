// Copyright 2023 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace CloudNative.CloudEvents.Protobuf.UnitTests;

public partial class ConformanceTestFile
{
    private static readonly JsonParser jsonParser =
        new(JsonParser.Settings.Default.WithTypeRegistry(TypeRegistry.FromFiles(ConformanceTestsReflection.Descriptor)));

    internal static ConformanceTestFile FromJson(string json) =>
        jsonParser.Parse<ConformanceTestFile>(json);
}
