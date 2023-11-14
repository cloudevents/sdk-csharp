// Copyright 2023 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CloudNative.CloudEvents.UnitTests.ConformanceTestData;

internal static class SampleEvents
{
    private static readonly ConcurrentDictionary<string, CloudEvent> eventsById = new ConcurrentDictionary<string, CloudEvent>();

    private static readonly IReadOnlyList<CloudEventAttribute> allExtensionAttributes = new List<CloudEventAttribute>()
    {
        CloudEventAttribute.CreateExtension("extinteger", CloudEventAttributeType.Integer),
        CloudEventAttribute.CreateExtension("extboolean", CloudEventAttributeType.Boolean),
        CloudEventAttribute.CreateExtension("extstring", CloudEventAttributeType.String),
        CloudEventAttribute.CreateExtension("exttimestamp", CloudEventAttributeType.Timestamp),
        CloudEventAttribute.CreateExtension("exturi", CloudEventAttributeType.Uri),
        CloudEventAttribute.CreateExtension("exturiref", CloudEventAttributeType.UriReference),
        CloudEventAttribute.CreateExtension("extbinary", CloudEventAttributeType.Binary),
    }.AsReadOnly();

    private static readonly CloudEvent minimal = new CloudEvent
    {
        Id = "minimal",
        Type = "io.cloudevents.test",
        Source = new Uri("https://cloudevents.io")
    }.Register();

    private static readonly CloudEvent allCore = minimal.With(evt =>
    {
        evt.Id = "allCore";
        evt.DataContentType = "text/plain";
        evt.DataSchema = new Uri("https://cloudevents.io/dataschema");
        evt.Subject = "tests";
        evt.Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero);
    }).Register();

    private static readonly CloudEvent minimalWithTime = minimal.With(evt =>
    {
        evt.Id = "minimalWithTime";
        evt.Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero);
    }).Register();

    private static readonly CloudEvent minimalWithRelativeSource = minimal.With(evt =>
    {
        evt.Id = "minimalWithRelativeSource";
        evt.Source = new Uri("#fragment", UriKind.RelativeOrAbsolute);
    }).Register();

    private static readonly CloudEvent simpleTextData = minimal.With(evt =>
    {
        evt.Id = "simpleTextData";
        evt.Data = "Simple text";
        evt.DataContentType = "text/plain";
    }).Register();

    private static readonly CloudEvent allExtensionTypes = minimal.WithSampleExtensionAttributes().With(evt =>
    {
        evt.Id = "allExtensionTypes";

        evt["extinteger"] = 10;
        evt["extboolean"] = true;
        evt["extstring"] = "text";
        evt["extbinary"] = new byte[] { 77, 97 };
        evt["exttimestamp"] = new DateTimeOffset(2023, 3, 31, 15, 12, 0, TimeSpan.Zero);
        evt["exturi"] = new Uri("https://cloudevents.io");
        evt["exturiref"] = new Uri("//authority/path", UriKind.RelativeOrAbsolute);
    }).Register();

    internal static CloudEvent Minimal => Clone(minimal);
    internal static CloudEvent AllCore => Clone(allCore);
    internal static CloudEvent MinimalWithTime => Clone(minimalWithTime);
    internal static CloudEvent MinimalWithRelativeSource => Clone(minimalWithRelativeSource);
    internal static CloudEvent SimpleTextData => Clone(simpleTextData);
    internal static CloudEvent AllExtensionTypes => Clone(allExtensionTypes);
    internal static IReadOnlyList<CloudEventAttribute> SampleExtensionAttributes => allExtensionAttributes;

    internal static CloudEvent FromId(string id) => eventsById.TryGetValue(id, out var evt)
        ? Clone(evt)
        : throw new ArgumentException($"No such sample event: '{id}'");

    // TODO: Make this available somewhere else?
    internal static CloudEvent Clone(CloudEvent evt)
    {
        var clone = new CloudEvent(evt.SpecVersion, evt.ExtensionAttributes);
        foreach (var attr in evt.GetPopulatedAttributes())
        {
            clone[attr.Key] = attr.Value;
        }
        // TODO: Deep copy where appropriate?
        clone.Data = evt.Data;
        return clone;
    }

    private static CloudEvent With(this CloudEvent evt, Action<CloudEvent> action)
    {
        var clone = Clone(evt);
        action(clone);
        return clone;
    }

    /// <summary>
    /// Returns a clone of the given CloudEvent, with all attributes in <see cref="allExtensionAttributes"/>
    /// registered but without values.
    /// </summary>
    private static CloudEvent WithSampleExtensionAttributes(this CloudEvent evt) => evt.With(clone =>
    {
        foreach (var attribute in allExtensionAttributes)
        {
            clone[attribute] = null;
        }
    });

    private static CloudEvent Register(this CloudEvent evt)
    {
        eventsById[evt.Id ?? throw new InvalidOperationException("No ID in sample event")] = evt;
        return evt;
    }
}
