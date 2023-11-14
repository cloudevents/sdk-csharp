// Copyright 2020 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Linq;
using System.Net.Mime;
using Xunit;
using static CloudNative.CloudEvents.UnitTests.TestHelpers;

namespace CloudNative.CloudEvents.UnitTests
{
    public class CloudEventTest
    {
        private static readonly DateTimeOffset sampleTimestamp = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero);

        [Fact]
        public void CreateSimpleEvent()
        {
            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull/123"),
                Id = "A234-1234-1234",
                Time = sampleTimestamp,
                DataContentType = MediaTypeNames.Text.Xml,
                Data = "<much wow=\"xml\"/>",
                ["comexampleextension1"] = "value"
            };

            Assert.Equal(CloudEventsSpecVersion.Default, cloudEvent.SpecVersion);
            Assert.Equal("com.github.pull.create", cloudEvent.Type);
            Assert.Equal(new Uri("https://github.com/cloudevents/spec/pull/123"), cloudEvent.Source);
            Assert.Equal("A234-1234-1234", cloudEvent.Id);
            AssertTimestampsEqual("2018-04-05T17:31:00Z", cloudEvent.Time.Value);
            Assert.Equal(MediaTypeNames.Text.Xml, cloudEvent.DataContentType);
            Assert.Equal("<much wow=\"xml\"/>", cloudEvent.Data);

            Assert.Equal("value", (string?) cloudEvent["comexampleextension1"]);
        }

        [Fact]
        public void CreateEventWithExtension()
        {
            var extension = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer);

            var cloudEvent = new CloudEvent(new[] { extension })
            {
                Type = "com.github.pull.create",
                Id = "A234-1234-1234",
                Time = sampleTimestamp,
                [extension] = 10
            };

            Assert.Equal(10, cloudEvent[extension]);
            Assert.Equal(10, cloudEvent["ext"]);

            Assert.Throws<ArgumentException>(() => cloudEvent.SetAttributeFromString("ext", "not an integer"));
            cloudEvent.SetAttributeFromString("ext", "20");
            Assert.Equal(20, cloudEvent[extension]);
        }

        [Fact]
        public void Invalid_ContentType_Throws()
        {
            var cloudEvent = new CloudEvent();
            var exception = Assert.Throws<ArgumentException>(() => cloudEvent.DataContentType = "text/html; charset:");
            Assert.StartsWith(Strings.ErrorContentTypeIsNotRFC2046, exception.InnerException!.Message);
        }

        [Fact]
        public void SetAttributePropertiesToNull()
        {
            var cloudEvent = new CloudEvent
            {
                Data = "some data",
                DataContentType = "text/plain",
                DataSchema = new Uri("https://schema")
            };

            cloudEvent.Type = null;
            cloudEvent.Source = null;
            cloudEvent.Subject = null;
            cloudEvent.Id = null;
            cloudEvent.Time = null;
            cloudEvent.Data = null;
            cloudEvent.DataContentType = null;
            cloudEvent.DataSchema = null;

            Assert.Null(cloudEvent.Type);
            Assert.Null(cloudEvent.Source);
            Assert.Null(cloudEvent.Subject);
            Assert.Null(cloudEvent.Id);
            Assert.Null(cloudEvent.Time);
            Assert.Null(cloudEvent.Data);
            Assert.Null(cloudEvent.DataContentType);
            Assert.Null(cloudEvent.DataSchema);
        }

        [Fact]
        public void Validate_Invalid()
        {
            var cloudEvent = new CloudEvent
            {
                Type = "type",
                DataContentType = "text/plain",
                Data = "text"
            };
            Assert.False(cloudEvent.IsValid);
            var exception1 = Assert.Throws<InvalidOperationException>(() => cloudEvent.Validate());
            Assert.Contains(CloudEventsSpecVersion.Default.IdAttribute.Name, exception1.Message);
            Assert.Contains(CloudEventsSpecVersion.Default.SourceAttribute.Name, exception1.Message);
            Assert.DoesNotContain(CloudEventsSpecVersion.Default.TypeAttribute.Name, exception1.Message);

            var exception2 = Assert.Throws<ArgumentException>(() => Validation.CheckCloudEventArgument(cloudEvent, "param"));
            Assert.Equal("param", exception2.ParamName);
            Assert.Contains(CloudEventsSpecVersion.Default.IdAttribute.Name, exception1.Message);
            Assert.Contains(CloudEventsSpecVersion.Default.SourceAttribute.Name, exception1.Message);
            Assert.DoesNotContain(CloudEventsSpecVersion.Default.TypeAttribute.Name, exception1.Message);
        }

        [Fact]
        public void Validate_Valid()
        {
            var cloudEvent = new CloudEvent
            {
                Type = "type",
                Id = "id",
                Source = new Uri("https://somewhere")
            };
            Assert.True(cloudEvent.IsValid);
            Assert.Same(cloudEvent, cloudEvent.Validate());
            Assert.Same(cloudEvent, Validation.CheckCloudEventArgument(cloudEvent, "param"));
        }

        [Fact]
        public void Constructor_SpecVersion()
        {
            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0);
            Assert.Equal(CloudEventsSpecVersion.V1_0, cloudEvent.SpecVersion);
        }

        [Fact]
        public void Constructor_NullVersion() =>
            Assert.Throws<ArgumentNullException>(() => new CloudEvent(specVersion: null!));

        [Fact]
        public void Constructor_SpecVersionAndExtensionAttributes()
        {
            var extension = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer);
            var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0, new[] { extension });
            // This fails if the extension isn't registered.
            cloudEvent["ext"] = 10;
            Assert.Equal(10, cloudEvent[extension]);
        }

        [Fact]
        public void Constructor_ExtensionAttributes_Duplicate()
        {
            var ext1 = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer);
            var ext2 = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer);
            var extensions = new[] { ext1, ext2 };
            Assert.Throws<ArgumentException>(() => new CloudEvent(extensions));
        }

        [Fact]
        public void Constructor_ExtensionAttributes_NullValue()
        {
            var extensions = new CloudEventAttribute[] { null! };
            Assert.Throws<ArgumentException>(() => new CloudEvent(extensions));
        }

        [Fact]
        public void Constructor_ExtensionAttributes_IncludesSpecVersionAttribute()
        {
            var extensions = new[] { CloudEventsSpecVersion.SpecVersionAttribute };
            Assert.Throws<ArgumentException>(() => new CloudEvent(extensions));
        }

        [Fact]
        public void Constructor_ExtensionAttributes_IncludesNonExtensionAttribute()
        {
            var extensions = new[] { CloudEventsSpecVersion.V1_0.DataContentTypeAttribute };
            Assert.Throws<ArgumentException>(() => new CloudEvent(extensions));
        }

        [Fact]
        public void ExtensionAttributesProperty()
        {
            var ext1 = CloudEventAttribute.CreateExtension("ext1", CloudEventAttributeType.Integer);
            var ext2 = CloudEventAttribute.CreateExtension("ext2", CloudEventAttributeType.Integer);
            var cloudEvent = new CloudEvent(new[] { ext1 });
            cloudEvent[ext2] = 10;
            var extensions = cloudEvent.ExtensionAttributes.OrderBy(attr => attr.Name).ToList();
            Assert.Equal(new[] { ext1, ext2 }, extensions);
        }

        /// <summary>
        /// This is effectively testing future proofing. If an extension attribute in 1.0 becomes an
        /// optional attribute in 1.1 for example, then code that has been using it *as* an extension
        /// attribute should still be able to work. The test has to use an attribute from 1.0 of course...
        /// </summary>
        [Fact]
        public void Indexer_SetUsingExtensionAttributeWithSameType()
        {
            var extension = CloudEventAttribute.CreateExtension("subject", CloudEventAttributeType.String);
            var cloudEvent = new CloudEvent();
            cloudEvent.Subject = "normal subject";
            cloudEvent[extension] = "extended subject";
            Assert.Equal("extended subject", cloudEvent.Subject);
        }

        [Fact]
        public void SetAttributeFromStringValue_Valid()
        {
            var extensions = new[]
            {
                CloudEventAttribute.CreateExtension("string", CloudEventAttributeType.String),
                CloudEventAttribute.CreateExtension("integer", CloudEventAttributeType.Integer),
                CloudEventAttribute.CreateExtension("binary", CloudEventAttributeType.Binary),
                CloudEventAttribute.CreateExtension("boolean", CloudEventAttributeType.Boolean),
                CloudEventAttribute.CreateExtension("timestamp", CloudEventAttributeType.Timestamp),
                CloudEventAttribute.CreateExtension("uri", CloudEventAttributeType.Uri),
                CloudEventAttribute.CreateExtension("urireference", CloudEventAttributeType.UriReference)
            };
            var cloudEvent = new CloudEvent(extensions);
            cloudEvent.SetAttributeFromString("string", "text");
            cloudEvent.SetAttributeFromString("integer", "10");
            cloudEvent.SetAttributeFromString("binary", "TQ==");
            cloudEvent.SetAttributeFromString("boolean", "true");
            cloudEvent.SetAttributeFromString("timestamp", "2021-02-09T11:58:12.242Z");
            cloudEvent.SetAttributeFromString("uri", "https://cloudevents.io");
            cloudEvent.SetAttributeFromString("urireference", "//auth");

            Assert.Equal("text", cloudEvent["string"]);
            Assert.Equal(10, cloudEvent["integer"]);
            Assert.Equal(new byte[] { 77 }, cloudEvent["binary"]);
            Assert.True((bool) cloudEvent["boolean"]!);
            AssertTimestampsEqual("2021-02-09T11:58:12.242Z", (DateTimeOffset) cloudEvent["timestamp"]!);
            Assert.Equal(new Uri("https://cloudevents.io"), cloudEvent["uri"]);
            Assert.Equal(new Uri("//auth", UriKind.RelativeOrAbsolute), cloudEvent["urireference"]);
        }

        [Fact]
        public void SetAttributeFromStringValue_Validates()
        {
            var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer);
            var cloudEvent = new CloudEvent(new[] { attr });
            Assert.Throws<ArgumentException>(() => cloudEvent.SetAttributeFromString("ext", "garbage"));
        }

        [Fact]
        public void SetAttributeFromStringValue_NewAttribute()
        {
            var cloudEvent = new CloudEvent();
            cloudEvent.SetAttributeFromString("ext", "text");
            Assert.Equal("text", cloudEvent["ext"]);
            Assert.Equal(CloudEventAttributeType.String, cloudEvent.GetAttribute("ext")!.Type);
        }

        [Fact]
        public void Indexer_NullKey_Throws()
        {
            var cloudEvent = new CloudEvent();
            Assert.Throws<ArgumentNullException>(() => cloudEvent[(string) null!]);
            Assert.Throws<ArgumentNullException>(() => cloudEvent[(CloudEventAttribute) null!]);
            Assert.Throws<ArgumentNullException>(() => cloudEvent[(string) null!] = "text");
            Assert.Throws<ArgumentNullException>(() => cloudEvent[(CloudEventAttribute) null!] = "text");
        }

        [Fact]
        public void Indexer_NullValue_Removes()
        {
            var cloudEvent = new CloudEvent
            {
                Id = "id",
                Type = "eventtype",
                Time = DateTimeOffset.UtcNow
            };
            cloudEvent["id"] = null;
            cloudEvent[CloudEventsSpecVersion.V1_0.TimeAttribute] = null;

            // We only have a single attribute left.
            var attributeAndValue = Assert.Single(cloudEvent.GetPopulatedAttributes());
            Assert.Equal("type", attributeAndValue.Key.Name);
            Assert.Equal("eventtype", attributeAndValue.Value);
        }

        /// <summary>
        /// Tests for behavior we're not sure of yet. This documents what *does* happen (because the tests
        /// pass) so we can more easily determine if it's what we *want* to happen.
        /// </summary>
        public class QuestionableBehavior
        {
            // We could infer the attribute type to be integer. 
            [Fact]
            public void SetNewAttributeWithNonString_Throws()
            {
                var cloudEvent = new CloudEvent();
                Assert.Throws<ArgumentException>(() => cloudEvent["ext"] = 10);
            }

            [Fact]
            public void FetchSpecVersionAttribute_Throws()
            {
                var cloudEvent = new CloudEvent();
                Assert.Throws<ArgumentException>(() => cloudEvent[CloudEventsSpecVersion.SpecVersionAttributeName]);
                Assert.Throws<ArgumentException>(() => cloudEvent[CloudEventsSpecVersion.SpecVersionAttribute]);
                Assert.Throws<ArgumentException>(() => cloudEvent[CloudEventsSpecVersion.SpecVersionAttributeName] = "1.0");
                Assert.Throws<ArgumentException>(() => cloudEvent[CloudEventsSpecVersion.SpecVersionAttribute] = "1.0");
            }

            [Fact]
            public void FetchIntegerExtensionSetImplicitlyWithString_Throws()
            {
                var cloudEvent = new CloudEvent();
                cloudEvent["ext"] = "10";

                var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer);
                Assert.Throws<ArgumentException>(() => cloudEvent[attr]);
            }

            [Fact]
            public void SetIntegerExtensionSetImplicitlyWithString_Updates()
            {
                var cloudEvent = new CloudEvent();
                cloudEvent["ext"] = "10";
                Assert.Equal(CloudEventAttributeType.String, cloudEvent.GetAttribute("ext")?.Type);

                var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer);
                // Setting the event with the attribute updates the extension registry...
                cloudEvent[attr] = 10;
                Assert.Equal(attr, cloudEvent.GetAttribute("ext"));
                // So we can fetch the value by string or attribute.
                Assert.Equal(10, cloudEvent[attr]);
                Assert.Equal(10, cloudEvent["ext"]);
            }

            [Fact]
            public void ClearNewExtensionAttributeRetainsAttributeType()
            {
                var cloudEvent = new CloudEvent();
                var attr = CloudEventAttribute.CreateExtension("ext", CloudEventAttributeType.Integer);
                cloudEvent[attr] = null; // Doesn't set any value, but remembers the extension...
                cloudEvent["ext"] = 10; // Which means it can be set as the integer later.
                Assert.Same(attr, cloudEvent.GetAttribute("ext"));
            }

            // We may want to relax this - as otherwise specifying extensions without an explicit
            // version is really brittle. (e.g. if "sampledrate" becomes an optional attribute in 1.1,
            // and if our default version becomes 1.1, then previously-valid code will start to fail.)
            [Fact]
            public void Constructor_ExtensionAttributes_IncludesExistingAttributeName()
            {
                var ext = CloudEventAttribute.CreateExtension("type", CloudEventAttributeType.String);
                var extensions = new[] { ext };
                Assert.Throws<ArgumentException>(() => new CloudEvent(extensions));
            }
        }
    }
}
