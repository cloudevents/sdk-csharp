// Copyright 2022 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.
using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.UnitTests;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.Xml.UnitTests
{
    public class XmlEventFormatterTest
    {
        private static readonly XNamespace ceNamespace = XmlEventFormatter.CloudEventsNamespace;
        private static readonly XNamespace xsiNamespace = XmlEventFormatter.XsiNamespace;

        // TODO: Add CloudEventAttributeType.ForName?
        private static readonly Dictionary<string, CloudEventAttributeType> attributeTypeByName = new[]
        {
            CloudEventAttributeType.Binary,
            CloudEventAttributeType.Boolean,
            CloudEventAttributeType.Integer,
            CloudEventAttributeType.String,
            CloudEventAttributeType.Timestamp,
            CloudEventAttributeType.Uri,
            CloudEventAttributeType.UriReference
        }.ToDictionary(type => type.Name);

        private static readonly Dictionary<string, XElement> invalidTestEventElements = LoadTestResourceElements("InvalidEvents.xml");
        private static readonly Dictionary<string, XElement> validTestEventElements = LoadTestResourceElements("ValidEvents.xml");
        private static readonly Dictionary<string, XElement> invalidTestBatchElements = LoadTestResourceElements("InvalidBatches.xml");
        private static readonly Dictionary<string, XElement> validTestBatchElements = LoadTestResourceElements("ValidBatches.xml");

        // Events that are expected to be equal to the ones in ValidEvents.xml
        private static readonly Dictionary<string, CloudEvent> validEvents = new Dictionary<string, CloudEvent>
        {
            { "Minimal", new CloudEvent().PopulateRequiredAttributes() },
            { "CommentsAreIgnored", new CloudEvent { Data = "Test data" }.PopulateRequiredAttributes() },
            { "AllV1Attributes",
                new CloudEvent { DataContentType = "text/plain", Data = "Test data", Subject = "test-subject", Time = SampleTimestamp, DataSchema = SampleUri }
                    .PopulateRequiredAttributes()
            },
            { "AllV1AttributesWithXsiTypes",
                new CloudEvent { DataContentType = "text/plain", Data = "Test data", Subject = "test-subject", Time = SampleTimestamp, DataSchema = SampleUri }
                    .PopulateRequiredAttributes()
            },
            { "AllExtensionAttributeTypes",
                new CloudEvent(AllTypesExtensions) {
                    ["binary"] = SampleBinaryData,
                    ["boolean"] = true,
                    ["integer"] = 10,
                    ["string"] = "text",
                    ["timestamp"] = SampleTimestamp,
                    ["uri"] = SampleUri,
                    ["urireference"] = SampleUriReference
                }.PopulateRequiredAttributes()
            },
            { "AllExtensionAttributeTypesPrespecified",
                new CloudEvent(AllTypesExtensions) {
                    ["binary"] = SampleBinaryData,
                    ["boolean"] = true,
                    ["integer"] = 10,
                    ["string"] = "text",
                    ["timestamp"] = SampleTimestamp,
                    ["uri"] = SampleUri,
                    ["urireference"] = SampleUriReference
                }.PopulateRequiredAttributes()
            },
            {
              "ExtensionValueSatisfiesValidation",
              new CloudEvent(new[] { CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.String) })
              {
                  ["ext"] = "xyz"
              }.PopulateRequiredAttributes()
            },
            { "TextData", new CloudEvent { Data = "Test data" }.PopulateRequiredAttributes() },
            { "BinaryData", new CloudEvent { Data = SampleBinaryData }.PopulateRequiredAttributes() },
            { "XmlData", new CloudEvent { Data = new XElement("test", new XAttribute("attr", "x"), "Text", new XElement("nested")) }.PopulateRequiredAttributes() },
        };

        private static Dictionary<string, XElement> LoadTestResourceElements(string resource) =>
            XDocument.Load(LoadResource(resource))
                .Root.Elements("test").ToDictionary(e => e.Attribute("name").Value);

        private static readonly Dictionary<string, IReadOnlyList<CloudEvent>> validBatches = new Dictionary<string, IReadOnlyList<CloudEvent>>
        {
            { "Empty", new CloudEvent[0] },
            { "MinimalSingle", new[] { new CloudEvent().PopulateRequiredAttributes() } },
            { "WithAndWithoutData",
                new[]
                {
                    new CloudEvent { Id = "test-id-1", Type = "test-type-1", Source = new Uri("//test-1", UriKind.Relative) },
                    new CloudEvent { Id = "test-id-2", Type = "test-type-2", Source = new Uri("//test-2", UriKind.Relative), DataContentType = "text/plain", Data = "Test data" }
                }
            },
            { "CommentsAreIgnored", new[] { new CloudEvent().PopulateRequiredAttributes() } },
        };

        // Invalid events:
        // - Mismatch between bytes and declared encoding.

        // Is an empty element acceptable as an empty (but present) attribute value?

        public static IEnumerable<object[]> InvalidEventTestNames => invalidTestEventElements.Keys.Select(name => new object[] { name });
        // All events in ValidEvents.xml can be decoded
        public static IEnumerable<object[]> ValidEventDecodeTestNames => validTestEventElements.Keys.Select(name => new object[] { name });
        // Some events in ValidEvents.xml won't roundtrip, e.g. due to redundantly specifying xsi:type values.
        public static IEnumerable<object[]> ValidEventEncodeTestNames => validTestEventElements
            .Where(pair => pair.Value.Attribute("roundtrip")?.Value != "false")
            .Select(pair => new object[] { pair.Key });

        public static IEnumerable<object[]> InvalidBatchTestNames => invalidTestBatchElements.Keys.Select(name => new object[] { name });
        public static IEnumerable<object[]> ValidBatchTestNames => validTestBatchElements.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(ValidEventDecodeTestNames))]
        public void ValidEvent_DecodeXElement(string name)
        {
            var expected = validEvents[name];
            var actual = ParseTestEventElement(validTestEventElements[name]);
            AssertCloudEventsEqual(expected, actual, new DataComparer());
        }

        [Theory]
        [MemberData(nameof(ValidEventEncodeTestNames))]
        public void ValidEvent_EncodeXElement(string name)
        {
            var expected = validTestEventElements[name].Elements().Last();
            var encodedBytes = new XmlEventFormatter().EncodeStructuredModeMessage(validEvents[name], out var contentType);
            var actual = XElement.Load(BinaryDataUtilities.AsStream(encodedBytes));
            Assert.Equal(expected, actual, XEqualityComparer.Instance);
        }

        [Theory]
        [MemberData(nameof(InvalidEventTestNames))]
        public void InvalidEvent (string name) =>
            Assert.Throws<ArgumentException>(() => ParseTestEventElement(invalidTestEventElements[name]));

        [Fact]
        public void InvalidEvent_ExtensionValueDoesntMatchType()
        {
            var eventElement = CreateMinimalEventElement();
            eventElement.Add(new XElement(ceNamespace + "ext"),
                new XAttribute(xsiNamespace + "type", "xs:int"),
                "x");
            Assert.Throws<ArgumentException>(() => ParseTestEventElement(CreateTestElement(eventElement)));
        }

        [Fact]
        public void InvalidEvent_InvalidBase64Data()
        {
            var eventElement = CreateMinimalEventElement();
            var dataElement = new XElement(XmlEventFormatter.DataElementName,
                new XAttribute(xsiNamespace + "type", "xs:base64Binary"),
                "x");
            eventElement.Add(dataElement);
            Assert.Throws<ArgumentException>(() => ParseTestEventElement(CreateTestElement(eventElement)));
        }

        [Fact]
        public void InvalidEvent_TextDataWithElement()
        {
            var eventElement = CreateMinimalEventElement();
            var dataElement = new XElement(XmlEventFormatter.DataElementName,
                new XAttribute(xsiNamespace + "type", "xs:string"),
                "xyz", new XElement("element"));
            Assert.Throws<ArgumentException>(() => ParseTestEventElement(CreateTestElement(eventElement)));
        }

        [Fact]
        public void InvalidEvent_BinaryDataWithElement()
        {
            var eventElement = CreateMinimalEventElement();
            var dataElement = new XElement(XmlEventFormatter.DataElementName,
                new XAttribute(xsiNamespace + "type", "xs:string"),
                SampleBinaryDataBase64, new XElement("element"));
            Assert.Throws<ArgumentException>(() => ParseTestEventElement(CreateTestElement(eventElement)));
        }

        [Theory]
        [MemberData(nameof(ValidBatchTestNames))]
        public void ValidBatch_DecodeXElement(string name)
        {
            var expected = validBatches[name];
            var actual = ParseTestBatchElement(validTestBatchElements[name]);
            AssertBatchesEqual(expected, actual, new DataComparer());
        }

        [Theory]
        [MemberData(nameof(ValidBatchTestNames))]
        public void ValidBatch_EncodeXElement(string name)
        {
            var expected = validTestBatchElements[name].Elements().Last();
            var encodedBytes = new XmlEventFormatter().EncodeBatchModeMessage(validBatches[name], out var contentType);
            var actual = XElement.Load(BinaryDataUtilities.AsStream(encodedBytes));
            Assert.Equal(expected, actual, XEqualityComparer.Instance);
        }

        [Theory]
        [MemberData(nameof(InvalidBatchTestNames))]
        public void InvalidBatch(string name) =>
            Assert.Throws<ArgumentException>(() => ParseTestBatchElement(invalidTestBatchElements[name]));

        private CloudEvent ParseTestEventElement(XElement testElement)
        {
            var eventElement = testElement.Elements().Last();
            var formatter = new XmlEventFormatter();
            var extensionAttributes = testElement
                .Elements("extension")
                .Select(CreateExtension)
                .ToList();
            return formatter.DecodeEvent(eventElement, extensionAttributes);

            CloudEventAttribute CreateExtension(XElement element)
            {
                string name = element.Attribute("name").Value;
                var type = attributeTypeByName[element.Attribute("type").Value];
                string? validatorName = element.Attribute("validator")?.Value;
                Action<object>? validator = validatorName switch
                {
                    null => null,
                    "non-empty-string" => obj => Validation.CheckArgument(((string) obj).Length != 0, nameof(obj), "Value must be non-empty"),
                    _ => throw new ArgumentException($"Unknown validator: {validatorName}")
                };
                return CloudEventAttribute.CreateExtension(name, type, validator);
            }
        }

        private IReadOnlyList<CloudEvent> ParseTestBatchElement(XElement testElement)
        {
            var batchElement = testElement.Elements().Last();
            var formatter = new XmlEventFormatter();
            var extensionAttributes = testElement
                .Elements("extension")
                .Select(CreateExtension)
                .ToList();
            return formatter.DecodeBatch(batchElement, extensionAttributes);

            CloudEventAttribute CreateExtension(XElement element)
            {
                string name = element.Attribute("name").Value;
                var type = attributeTypeByName[element.Attribute("type").Value];
                string? validatorName = element.Attribute("validator")?.Value;
                Action<object>? validator = validatorName switch
                {
                    null => null,
                    "non-empty-string" => obj => Validation.CheckArgument(((string) obj).Length != 0, nameof(obj), "Value must be non-empty"),
                    _ => throw new ArgumentException($"Unknown validator: {validatorName}")
                };
                return CloudEventAttribute.CreateExtension(name, type, validator);
            }
        }

        private XElement CreateMinimalEventElement() =>
            new XElement(ceNamespace + "event",
                new XElement(ceNamespace + "id", "test-id"),
                new XElement(ceNamespace + "type", "test-type"),
                new XElement(ceNamespace + "source", "//test-source"));

        private XElement CreateTestElement(XElement eventElement) => new XElement("test", eventElement);

        private static Stream LoadResource(string name) => TestHelpers.LoadResource($"CloudNative.CloudEvents.UnitTests.Xml.{name}");

        private class DataComparer : IEqualityComparer<object?>
        {
            public new bool Equals([AllowNull] object? x, [AllowNull] object? y) => (x, y) switch
            {
                (null, null) => true,
                (_, null) => false,
                (null, _) => false,
                (byte[] xBytes, byte[] yBytes) => xBytes.SequenceEqual(yBytes),
                (string xString, string yString) => xString == yString,
                (XElement xElement, XElement yElement) => XEqualityComparer.Instance.Equals(xElement, yElement),
                _ => throw new InvalidOperationException($"Can't compare {x.GetType()} with {y.GetType()}")
            };

            public int GetHashCode([DisallowNull] object? obj) => throw new NotImplementedException();
        }

        // Note: this deliberately doesn't try to handle null values.
        private class XEqualityComparer : IEqualityComparer<XElement?>, IEqualityComparer<XAttribute?>
        {
            internal static XEqualityComparer Instance { get; } = new XEqualityComparer();

            public bool Equals(XElement? x, XElement? y) =>
                x!.Name == y!.Name &&
                // We skip xmnls attributes, as we don't particularly care how individual elements "get" their names
                x.Attributes().Where(IsNotXmnls).SequenceEqual(y.Attributes().Where(IsNotXmnls), this) &&
                //x.Value.Trim() == y.Value.Trim() &&
                x.Elements().OrderBy(e => e.Name.ToString()).SequenceEqual(y.Elements().OrderBy(e => e.Name.ToString()), this);

            public bool Equals(XAttribute? x, XAttribute? y) => x!.Name == y!.Name && x.Value == y.Value;

            private static bool IsNotXmnls(XAttribute attr) => attr.Name.Namespace != XNamespace.Xmlns;

            public int GetHashCode([DisallowNull] XElement? obj) => throw new NotImplementedException();
            public int GetHashCode([DisallowNull] XAttribute? obj) => throw new NotImplementedException();
        }
    }
}
