// Copyright 2023 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.UnitTests;
using CloudNative.CloudEvents.UnitTests.ConformanceTestData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace CloudNative.CloudEvents.NewtonsoftJson.UnitTests;

public class ConformanceTest
{
    private static readonly IReadOnlyList<JsonConformanceTest> allTests =
        TestDataProvider.Json.LoadTests(ConformanceTestFile.FromJson, file => file.Tests);

    private static JsonConformanceTest GetTestById(string id) => allTests.Single(test => test.Id == id);
    private static IEnumerable<object[]> SelectTestIds(ConformanceTestType type) =>
        allTests
            .Where(test => test.TestType == type)
            .Select(test => new object[] { test.Id });

    public static IEnumerable<object[]> ValidEventTestIds => SelectTestIds(ConformanceTestType.ValidSingleEvent);
    public static IEnumerable<object[]> InvalidEventTestIds => SelectTestIds(ConformanceTestType.InvalidSingleEvent);
    public static IEnumerable<object[]> ValidBatchTestIds => SelectTestIds(ConformanceTestType.ValidBatch);
    public static IEnumerable<object[]> InvalidBatchTestIds => SelectTestIds(ConformanceTestType.InvalidBatch);

    [Theory, MemberData(nameof(ValidEventTestIds))]
    public void ValidEvent(string testId)
    {
        var test = GetTestById(testId);
        CloudEvent expected = SampleEvents.FromId(test.SampleId);
        var extensions = test.SampleExtensionAttributes ? SampleEvents.SampleExtensionAttributes : null;
        CloudEvent actual = new JsonEventFormatter().ConvertFromJObject(test.Event, extensions);
        TestHelpers.AssertCloudEventsEqual(expected, actual, TestHelpers.InstantOnlyTimestampComparer);
    }

    [Theory, MemberData(nameof(InvalidEventTestIds))]
    public void InvalidEvent(string testId)
    {
        var test = GetTestById(testId);
        var formatter = new JsonEventFormatter();
        var extensions = test.SampleExtensionAttributes ? SampleEvents.SampleExtensionAttributes : null;
        Assert.Throws<ArgumentException>(() => formatter.ConvertFromJObject(test.Event, extensions));
    }

    [Theory, MemberData(nameof(ValidBatchTestIds))]
    public void ValidBatch(string testId)
    {
        var test = GetTestById(testId);
        IReadOnlyList<CloudEvent> expected = SampleBatches.FromId(test.SampleId);
        var extensions = test.SampleExtensionAttributes ? SampleEvents.SampleExtensionAttributes : null;
        // We don't have a convenience method for batches, so serialize the array back to JSON.
        var json = test.Batch.ToString();
        var body = Encoding.UTF8.GetBytes(json);
        IReadOnlyList<CloudEvent> actual = new JsonEventFormatter().DecodeBatchModeMessage(body, contentType: null, extensions);
        TestHelpers.AssertBatchesEqual(expected, actual, TestHelpers.InstantOnlyTimestampComparer);
    }

    [Theory, MemberData(nameof(InvalidBatchTestIds))]
    public void InvalidBatch(string testId)
    {
        var test = GetTestById(testId);
        var formatter = new JsonEventFormatter();
        var extensions = test.SampleExtensionAttributes ? SampleEvents.SampleExtensionAttributes : null;
        // We don't have a convenience method for batches, so serialize the array back to JSON.
        var json = test.Batch.ToString();
        var body = Encoding.UTF8.GetBytes(json);
        Assert.Throws<ArgumentException>(() => formatter.DecodeBatchModeMessage(body, contentType: null, extensions));
    }
}
