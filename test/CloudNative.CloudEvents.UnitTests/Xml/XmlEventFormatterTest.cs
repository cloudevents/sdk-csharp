// Copyright 2022 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.
using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.UnitTests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace CloudNative.CloudEvents.Xml.UnitTests
{
    public class XmlEventFormatterTest
    {
        private static readonly XNamespace ceNamespace = XmlEventFormatter.CloudEventsNamespace;
        private static readonly XNamespace xsiNamespace = XmlEventFormatter.XsiNamespace;

        // TODO: Add CloudEventAttributeType.ForName?
        private static readonly Dictionary<string, CloudEventAttributeType> attributeTypeByName = new[]
        {
            CloudEventAttributeType.Boolean,
            CloudEventAttributeType.Binary,
            CloudEventAttributeType.String,
            CloudEventAttributeType.Timestamp,
            CloudEventAttributeType.UriReference,
            CloudEventAttributeType.Integer,
            CloudEventAttributeType.Uri
        }.ToDictionary(type => type.Name);

        private static readonly Dictionary<string, XElement> invalidTestElements = LoadTestResourceElements("InvalidEvents.xml");
        private static readonly Dictionary<string, XElement> validTestElements = LoadTestResourceElements("ValidEvents.xml");

        // Events that are expected to be equal to the ones in ValidEvents.xml
        private static readonly Dictionary<string, CloudEvent> validEvents = new Dictionary<string, CloudEvent>
        {
            { "Minimal", new CloudEvent().PopulateRequiredAttributes() }
        };

        private static Dictionary<string, XElement> LoadTestResourceElements(string resource) =>
            XDocument.Load(LoadResource(resource))
                .Root.Elements("test").ToDictionary(e => e.Attribute("name").Value);


        // Invalid events:
        // - Mismatch between bytes and declared encoding.

        // Is an empty element acceptable as an empty (but present) attribute value?

        public static IEnumerable<object[]> InvalidEventTestNames => invalidTestElements.Keys.Select(name => new object[] { name });
        public static IEnumerable<object[]> ValidEventTestNames => validTestElements.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(ValidEventTestNames))]
        public void ValidEvent(string name)
        {
            var expected = validEvents[name];
            var actual = ParseTestElement(validTestElements[name]);
            TestHelpers.AssertCloudEventsEqual(expected, actual);
        }

        [Theory]
        [MemberData(nameof(InvalidEventTestNames))]
        public void InvalidEvent (string name) =>
            Assert.Throws<ArgumentException>(() => ParseTestElement(invalidTestElements[name]));

        [Fact]
        public void InvalidEvent_ExtensionValueDoesntMatchType()
        {
            var eventElement = CreateMinimalEventElement();
            eventElement.Add(new XElement(ceNamespace + "ext"),
                new XAttribute(xsiNamespace + "type", "xs:int"),
                "x");
            Assert.Throws<ArgumentException>(() => ParseTestElement(CreateTestElement(eventElement)));
        }

        [Fact]
        public void InvalidEvent_InvalidBase64Data()
        {
            var eventElement = CreateMinimalEventElement();
            var dataElement = new XElement(XmlEventFormatter.DataElementName,
                new XAttribute(xsiNamespace + "type", "xs:base64Binary"),
                "x");
            eventElement.Add(dataElement);
            Assert.Throws<ArgumentException>(() => ParseTestElement(CreateTestElement(eventElement)));
        }

        [Fact]
        public void InvalidEvent_TextDataWithElement()
        {
            var eventElement = CreateMinimalEventElement();
            var dataElement = new XElement(XmlEventFormatter.DataElementName,
                new XAttribute(xsiNamespace + "type", "xs:string"),
                "xyz", new XElement("element"));
            Assert.Throws<ArgumentException>(() => ParseTestElement(CreateTestElement(eventElement)));
        }

        [Fact]
        public void InvalidEvent_BinaryDataWithElement()
        {
            var eventElement = CreateMinimalEventElement();
            var dataElement = new XElement(XmlEventFormatter.DataElementName,
                new XAttribute(xsiNamespace + "type", "xs:string"),
                TestHelpers.SampleBinaryDataBase64, new XElement("element"));
            Assert.Throws<ArgumentException>(() => ParseTestElement(CreateTestElement(eventElement)));
        }

        private CloudEvent ParseTestElement(XElement testElement)
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
                string validatorName = element.Attribute("validator").Value;
                Action<object>? validator = validatorName switch
                {
                    null => null,
                    "non-empty-string" => obj => Validation.CheckArgument(((string) obj).Length == 0, nameof(obj), "Value must be non-empty"),
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
    }
}
