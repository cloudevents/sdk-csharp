// Copyright 2023 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CloudNative.CloudEvents.UnitTests.ConformanceTestData;

public static class SampleBatches
{
    private static readonly ConcurrentDictionary<string, ReadOnlyCollection<CloudEvent>> batchesById = new ConcurrentDictionary<string, ReadOnlyCollection<CloudEvent>>();

    private static readonly ReadOnlyCollection<CloudEvent> empty = Register("empty");
    private static readonly ReadOnlyCollection<CloudEvent> minimal = Register("minimal", SampleEvents.Minimal);
    private static readonly ReadOnlyCollection<CloudEvent> minimal2 = Register("minimal2", SampleEvents.Minimal, SampleEvents.Minimal);
    private static readonly ReadOnlyCollection<CloudEvent> minimalAndAllCore = Register("minimalAndAllCore", SampleEvents.Minimal, SampleEvents.AllCore);
    private static readonly ReadOnlyCollection<CloudEvent> minimalAndAllExtensionTypes =
        Register("minimalAndAllExtensionTypes", SampleEvents.Minimal, SampleEvents.AllExtensionTypes);

    internal static ReadOnlyCollection<CloudEvent> Empty => Clone(empty);
    internal static ReadOnlyCollection<CloudEvent> Minimal => Clone(minimal);
    internal static ReadOnlyCollection<CloudEvent> Minimal2 => Clone(minimal2);
    internal static ReadOnlyCollection<CloudEvent> MinimalAndAllCore => Clone(minimalAndAllCore);
    internal static ReadOnlyCollection<CloudEvent> MinimalAndAllExtensionTypes => Clone(minimalAndAllExtensionTypes);

    internal static ReadOnlyCollection<CloudEvent> FromId(string id) => batchesById.TryGetValue(id, out var batch)
        ? Clone(batch)
        : throw new ArgumentException($"No such sample batch: '{id}'");

    private static ReadOnlyCollection<CloudEvent> Clone(ReadOnlyCollection<CloudEvent> cloudEvents) =>
        cloudEvents.Select(SampleEvents.Clone).ToList().AsReadOnly();

    private static ReadOnlyCollection<CloudEvent> Register(string id, params CloudEvent[] cloudEvents)
    {
        var list = new List<CloudEvent>(cloudEvents).AsReadOnly();
        batchesById[id] = list;
        return list;
    }
}
