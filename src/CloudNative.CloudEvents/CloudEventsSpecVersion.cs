// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using static CloudNative.CloudEvents.CloudEventAttribute;

namespace CloudNative.CloudEvents
{
    // TODO: Rename to CloudEventSpecVersion? Using the plural form feels a little odd.

    /// <summary>
    /// Represents a version of the CloudEvents specification, including
    /// the context attribute values known to that version.
    /// </summary>
    public sealed class CloudEventsSpecVersion
    {
        internal const string SpecVersionAttributeName = "specversion";

        /// <summary>
        /// The attribute used to indicate the version of the CloudEvents specification being used.
        /// </summary>
        public static CloudEventAttribute SpecVersionAttribute { get; } = CreateRequired(SpecVersionAttributeName, CloudEventAttributeType.String, NonEmptyString);

        // Populated by the constructor.
        private static readonly List<CloudEventsSpecVersion> allVersions = new List<CloudEventsSpecVersion>();

        public static CloudEventsSpecVersion Default => V1_0;

        public static CloudEventsSpecVersion V1_0 { get; } = new CloudEventsSpecVersion(
            "1.0",
            CreateRequired("id", CloudEventAttributeType.String, NonEmptyString),
            CreateRequired("source", CloudEventAttributeType.UriReference, NonEmptyUri),
            CreateRequired("type", CloudEventAttributeType.String, NonEmptyString),
            CreateOptional("datacontenttype", CloudEventAttributeType.String, Rfc2046String), // TODO: Do we want some way of adding validation that this is per RFC 2046?
            CreateOptional("dataschema", CloudEventAttributeType.Uri, NonEmptyUri),
            CreateOptional("subject", CloudEventAttributeType.String, NonEmptyString),
            CreateOptional("time", CloudEventAttributeType.Timestamp, null));

        /// <summary>
        /// The ID of the spec version, in its canonical serialized form,
        /// such as "1.0".
        /// </summary>
        public string VersionId { get; }

        public CloudEventAttribute IdAttribute { get; }
        public CloudEventAttribute DataContentTypeAttribute { get; }
        public CloudEventAttribute DataSchemaAttribute { get; }
        public CloudEventAttribute SourceAttribute { get; }
        public CloudEventAttribute SubjectAttribute { get; }
        public CloudEventAttribute TimeAttribute { get; }
        public CloudEventAttribute TypeAttribute { get; }

        private Dictionary<string, CloudEventAttribute> attributesByName;

        // TODO: What's the compatibility story? What might be in 1.1, and how would we handle that in 1.0?

        /// <summary>
        /// Returns the CloudEvents spec version for the given version ID (e.g. "1.0"),
        /// or null if no such version is known.
        /// </summary>
        /// <param name="versionId">The version ID to check. May be null, in which case the result will be null.</param>
        public static CloudEventsSpecVersion FromVersionId(string versionId) =>
            allVersions.FirstOrDefault(version => version.VersionId == versionId);

        private CloudEventsSpecVersion(
            string versionId,
            CloudEventAttribute idAttribute,
            CloudEventAttribute sourceAttribute,
            CloudEventAttribute typeAttribute,
            CloudEventAttribute dataContentTypeAttribute,
            CloudEventAttribute dataSchemaAttribute,
            CloudEventAttribute subjectAttribute,
            CloudEventAttribute timeAttribute)
        {
            VersionId = versionId;
            IdAttribute = idAttribute;
            SourceAttribute = sourceAttribute;
            TypeAttribute = typeAttribute;
            DataContentTypeAttribute = dataContentTypeAttribute;
            DataSchemaAttribute = dataSchemaAttribute;
            SubjectAttribute = subjectAttribute;
            TimeAttribute = timeAttribute;

            var allAttributes = new[]
            {
                idAttribute, sourceAttribute, typeAttribute, dataContentTypeAttribute,
                dataSchemaAttribute, subjectAttribute, timeAttribute
            };
            RequiredAttributes = allAttributes.Where(a => a.IsRequired).ToList().AsReadOnly();
            OptionalAttributes = allAttributes.Where(a => !a.IsRequired).ToList().AsReadOnly();
            AllAttributes = RequiredAttributes.Concat(OptionalAttributes).ToList().AsReadOnly();
            attributesByName = AllAttributes.ToDictionary(attr => attr.Name);
            allVersions.Add(this);
        }

        /// <summary>
        /// Returns the attribute with the given name, or null if this
        /// spec version does not contain any such attribute.
        /// </summary>
        /// <param name="name">The name of the attribute to find.</param>
        /// <returns>The attribute with the given name, or null if this spec version does not contain any such attribute.</returns>
        internal CloudEventAttribute GetAttributeByName(string name) => attributesByName.GetValueOrDefault(name);

        /// <summary>
        /// Returns all required attributes in this version of the CloudEvents specification.
        /// </summary>
        public IEnumerable<CloudEventAttribute> RequiredAttributes { get; }

        /// <summary>
        /// Returns all optional, non-extension attributes in this version of the CloudEvents specification.
        /// </summary>
        public IEnumerable<CloudEventAttribute> OptionalAttributes { get; }

        /// <summary>
        /// Returns all the non-extension attributes in this version of the CloudEvents specification.
        /// All required attributes are returned before optional attributes.
        /// </summary>
        public IEnumerable<CloudEventAttribute> AllAttributes { get; }

        private static void NonEmptyString(object value)
        {
            string text = (string)value;
            if (text.Length == 0)
            {
                throw new ArgumentException("Value must be non-empty");
            }
        }

        private static void NonEmptyUri(object value)
        {
            // TODO: is this actually useful?
        }

        private static void Rfc2046String(object value)
        {
            try
            {
                _ = new ContentType((string)value);
            }
            catch
            {
                throw new ArgumentException(Strings.ErrorContentTypeIsNotRFC2046);
            }
        }
    }
}
