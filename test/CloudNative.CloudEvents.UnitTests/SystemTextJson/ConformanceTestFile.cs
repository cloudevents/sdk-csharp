// Copyright 2023 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CloudNative.CloudEvents.SystemTextJson.UnitTests;

#nullable disable

public class ConformanceTestFile
{
    private static readonly JsonSerializerOptions serializerOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [JsonPropertyName("testType")]
    public ConformanceTestType? TestType { get; set; }

    // Note: we need a setter here; System.Text.Json doesn't support adding to an existing collection.
    // See https://github.com/dotnet/runtime/issues/30258
    [JsonPropertyName("tests")]
    public List<JsonConformanceTest> Tests { get; set; } = new List<JsonConformanceTest>();

    public static ConformanceTestFile FromJson(string json)
    {
        var testFile = JsonSerializer.Deserialize<ConformanceTestFile>(json, serializerOptions) ?? throw new InvalidOperationException();
        foreach (var test in testFile.Tests)
        {
            test.TestType ??= testFile.TestType;
        }
        return testFile;
    }
}

public class JsonConformanceTest
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
    [JsonPropertyName("testType")]
    public ConformanceTestType? TestType { get; set; }
    [JsonPropertyName("sampleId")]
    public string SampleId { get; set; }
    [JsonPropertyName("event")]
    public JsonElement Event { get; set; }
    [JsonPropertyName("batch")]
    public JsonArray Batch { get; set; }
    [JsonPropertyName("sampleExtensionAttributes")]
    public bool SampleExtensionAttributes { get; set; }
    [JsonPropertyName("extensionConstraints")]
    public bool ExtensionConstraints { get; set; }
}

public enum ConformanceTestType
{
    ValidSingleEvent,
    ValidBatch,
    InvalidSingleEvent,
    InvalidBatch
}