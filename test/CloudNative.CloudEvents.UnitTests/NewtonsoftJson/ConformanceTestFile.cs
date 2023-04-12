// Copyright 2023 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace CloudNative.CloudEvents.NewtonsoftJson.UnitTests;

#nullable disable

public class ConformanceTestFile
{
    private static readonly JsonSerializerSettings serializerSeettings = new() { DateParseHandling = DateParseHandling.None };

    public ConformanceTestType? TestType { get; set; }
    public List<JsonConformanceTest> Tests { get; } = new List<JsonConformanceTest>();

    public static ConformanceTestFile FromJson(string json)
    {
        var testFile = JsonConvert.DeserializeObject<ConformanceTestFile>(json, serializerSeettings) ?? throw new InvalidOperationException();
        foreach (var test in testFile.Tests)
        {
            test.TestType ??= testFile.TestType;
        }
        return testFile;
    }    
}

public class JsonConformanceTest
{
    public string Id { get; set; }
    public string Description { get; set; }
    public ConformanceTestType? TestType { get; set; }
    public string SampleId { get; set; }
    public JObject Event { get; set; }
    public JArray Batch { get; set; }
    public bool RoundTrip { get; set; }
    public bool SampleExtensionAttributes { get; set; }
    public bool ExtensionConstraints { get; set; }
}

public enum ConformanceTestType
{    
    ValidSingleEvent,
    ValidBatch,
    InvalidSingleEvent,
    InvalidBatch
}