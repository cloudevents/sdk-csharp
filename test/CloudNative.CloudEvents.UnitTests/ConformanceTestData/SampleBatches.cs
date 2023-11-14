// Copyright 2023 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents.UnitTests.ConformanceTestData;

public static class SampleBatches
{
    private static readonly ConcurrentDictionary<string, IReadOnlyList<CloudEvent>> batchesById = new ConcurrentDictionary<string, IReadOnlyList<CloudEvent>>();

    private static readonly IReadOnlyList<CloudEvent> empty = Register("empty");
    private static readonly IReadOnlyList<CloudEvent> minimal = Register("minimal", SampleEvents.Minimal);
    private static readonly IReadOnlyList<CloudEvent> minimal2 = Register("minimal2", SampleEvents.Minimal, SampleEvents.Minimal);
    private static readonly IReadOnlyList<CloudEvent> minimalAndAllCore = Register("minimalAndAllCore", SampleEvents.Minimal, SampleEvents.AllCore);
    private static readonly IReadOnlyList<CloudEvent> minimalAndAllExtensionTypes =
        Register("minimalAndAllExtensionTypes", SampleEvents.Minimal, SampleEvents.AllExtensionTypes);

    internal static IReadOnlyList<CloudEvent> Empty => Clone(empty);
    internal static IReadOnlyList<CloudEvent> Minimal => Clone(minimal);
    internal static IReadOnlyList<CloudEvent> Minimal2 => Clone(minimal2);
    internal static IReadOnlyList<CloudEvent> MinimalAndAllCore => Clone(minimalAndAllCore);
    internal static IReadOnlyList<CloudEvent> MinimalAndAllExtensionTypes => Clone(minimalAndAllExtensionTypes);

    internal static IReadOnlyList<CloudEvent> FromId(string id) => batchesById.TryGetValue(id, out var batch)
        ? Clone(batch)
        : throw new ArgumentException($"No such sample batch: '{id}'");

    private static IReadOnlyList<CloudEvent> Clone(IReadOnlyList<CloudEvent> cloudEvents) =>
        cloudEvents.Select(SampleEvents.Clone).ToList().AsReadOnly();

    private static IReadOnlyList<CloudEvent> Register(string id, params CloudEvent[] cloudEvents)
    {
        var list = new List<CloudEvent>(cloudEvents).AsReadOnly();
        batchesById[id] = list;
        return list;
    }
}
