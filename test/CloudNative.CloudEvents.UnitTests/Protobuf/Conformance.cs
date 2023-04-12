// Copyright 2023 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.UnitTests;
using CloudNative.CloudEvents.UnitTests.ConformanceTestData;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CloudNative.CloudEvents.Protobuf.UnitTests;

public class Conformance
{
    private static readonly IReadOnlyList<ConformanceTest> allTests =
        TestDataProvider.Protobuf.LoadTests(ConformanceTestFile.FromJson, file => file.Tests);

    private static ConformanceTest GetTestById(string id) => allTests.Single(test => test.Id == id);
    private static IEnumerable<object[]> SelectTestIds(ConformanceTest.EventOneofCase eventCase) =>
        allTests
            .Where(test => test.EventCase == eventCase)
            .Select(test => new object[] { test.Id });

    public static IEnumerable<object[]> ValidEventTestIds => SelectTestIds(ConformanceTest.EventOneofCase.ValidSingle);
    public static IEnumerable<object[]> InvalidEventTestIds => SelectTestIds(ConformanceTest.EventOneofCase.InvalidSingle);
    public static IEnumerable<object[]> ValidBatchTestIds => SelectTestIds(ConformanceTest.EventOneofCase.ValidBatch);
    public static IEnumerable<object[]> InvalidBatchTestIds => SelectTestIds(ConformanceTest.EventOneofCase.InvalidBatch);

    [Theory, MemberData(nameof(ValidEventTestIds))]
    public void ValidEvent(string testId)
    {
        var test = GetTestById(testId);
        CloudEvent expected = SampleEvents.FromId(test.SampleId);
        CloudEvent actual = new ProtobufEventFormatter().ConvertFromProto(test.ValidSingle, null);
        TestHelpers.AssertCloudEventsEqual(expected, actual, TestHelpers.InstantOnlyTimestampComparer);
    }

    [Theory, MemberData(nameof(InvalidEventTestIds))]
    public void InvalidEvent(string testId)
    {
        var test = GetTestById(testId);
        var formatter = new ProtobufEventFormatter();
        Assert.Throws<ArgumentException>(() => formatter.ConvertFromProto(test.InvalidSingle, null));
    }

    [Theory, MemberData(nameof(ValidBatchTestIds))]
    public void ValidBatch(string testId)
    {
        var test = GetTestById(testId);
        IReadOnlyList<CloudEvent> expected = SampleBatches.FromId(test.SampleId);

        // We don't have a convenience method for batches, so serialize batch back to binary.
        var body = test.ValidBatch.ToByteArray();
        IReadOnlyList<CloudEvent> actual = new ProtobufEventFormatter().DecodeBatchModeMessage(body, null, null);
        TestHelpers.AssertBatchesEqual(expected, actual, TestHelpers.InstantOnlyTimestampComparer);
    }

    [Theory, MemberData(nameof(InvalidBatchTestIds))]
    public void InvalidBatch(string testId)
    {
        var test = GetTestById(testId);
        var formatter = new ProtobufEventFormatter();
        // We don't have a convenience method for batches, so serialize batch back to binary.
        var body = test.InvalidBatch.ToByteArray();
        Assert.Throws<ArgumentException>(() => formatter.DecodeBatchModeMessage(body, null, null));
    }
}
