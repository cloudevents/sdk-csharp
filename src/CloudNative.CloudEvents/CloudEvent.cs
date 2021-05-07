// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CloudNative.CloudEvents
{
    // TODO: Document really clearly that the SpecVersion attribute isn't included anywhere here...
    // TODO: Consider implementing IDictionary<,>

    /// <summary>
    /// Represents a CloudEvent.
    /// </summary>
    public sealed class CloudEvent
    {
        /// <summary>
        /// The media type (also known as MIME type) for CloudEvents. Related media types
        /// (e.g. for a batch of CloudEvents, or with a specific format) usually begin with this string.
        /// </summary>
        public const string MediaType = "application/cloudevents";

        private readonly Dictionary<string, CloudEventAttribute> extensionAttributes = new Dictionary<string, CloudEventAttribute>();

        /// <summary>
        /// Values for all attributes other than spec version.
        /// </summary>
        private readonly Dictionary<string, object> attributeValues = new Dictionary<string, object>();

        /// <summary>
        /// Creates a new instance, using the default <see cref="CloudEventsSpecVersion"/>
        /// and no initial extension attributes.
        /// </summary>
        public CloudEvent() : this(CloudEventsSpecVersion.Default, null)
        {
        }

        /// <summary>
        /// Creates a new instance, using the specified <see cref="CloudEventsSpecVersion"/>
        /// and no initial extension attributes.
        /// </summary>
        /// <param name="specVersion">CloudEvents Specification version for this instance. Must not be null.</param>
        public CloudEvent(CloudEventsSpecVersion specVersion) : this(specVersion, null)
        {
        }

        /// <summary>
        /// Creates a new instance, using the default <see cref="CloudEventsSpecVersion"/>
        /// and the specified initial extension attributes.
        /// </summary>
        /// <param name="extensionAttributes">Initial extension attributes. May be null, which is equivalent
        /// to an empty sequence.</param>
        public CloudEvent(IEnumerable<CloudEventAttribute> extensionAttributes) : this(CloudEventsSpecVersion.Default, extensionAttributes)
        {
        }

        /// <summary>
        /// Creates a new instance, using the specified <see cref="CloudEventsSpecVersion"/>
        /// and the specified initial extension attributes.
        /// </summary>
        /// <param name="specVersion">CloudEvents Specification version for this instance. Must not be null.</param>
        /// <param name="extensionAttributes">Initial extension attributes. May be null, which is equivalent
        /// to an empty sequence.</param>
        public CloudEvent(CloudEventsSpecVersion specVersion, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            // TODO: Work out how to be more efficient, e.g. not creating a dictionary at all if there are no
            // extension attributes.
            SpecVersion = Validation.CheckNotNull(specVersion, nameof(specVersion));
            if (extensionAttributes is object)
            {
                foreach (var extension in extensionAttributes)
                {
                    Validation.CheckArgument(
                        extension is object,
                        nameof(extensionAttributes),
                        "Extension attribute collection cannot contain null elements");
                    Validation.CheckArgument(
                        extension.Name != CloudEventsSpecVersion.SpecVersionAttributeName,
                        nameof(extensionAttributes),
                        "The 'specversion' attribute cannot be specified as an extension attribute");
                    Validation.CheckArgument(
                        SpecVersion.GetAttributeByName(extension.Name) is null,
                        nameof(extensionAttributes),
                        "'{0}' cannot be specified as the name of an extension attribute; it is already a context attribute",
                        extension.Name);
                    Validation.CheckArgument(
                        extension.IsExtension,
                        nameof(extensionAttributes),
                        "'{0}' is not an extension attribute",
                        extension.Name);
                    Validation.CheckArgument(
                        !this.extensionAttributes.ContainsKey(extension.Name),
                        nameof(extensionAttributes),
                        "'{0}' cannot be specified more than once as an extension attribute");
                    this.extensionAttributes.Add(extension.Name, extension);
                }
            }
        }

        /// <summary>
        /// The CloudEvents specification version for this event.
        /// </summary>
        public CloudEventsSpecVersion SpecVersion { get; }

        /// <summary>
        /// Sets or fetches the value associated with the given attribute.
        /// If the attribute is not known in this event, fetching the value always returns null, and
        /// setting the value adds the attribute, which must be an extension attribute with a name which is
        /// not otherwise present known to the event.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If non-null, the value must be compatible with the type of the attribute. For example, an attempt
        /// to store a Time context attribute with a string value will fail with an <see cref="ArgumentException"/>.
        /// </para>
        /// <para>
        /// The the value being set is null, any existing value is removed from the event.
        /// </para>
        /// </remarks>
        /// <param name="attribute">The attribute whose value should be set or fetched.</param>
        /// <returns>The fetched attribute value, or null if the attribute has no value in this event.</returns>
        public object this[CloudEventAttribute attribute]
        {
            get
            {
                Validation.CheckNotNull(attribute, nameof(attribute));
                Validation.CheckArgument(attribute.Name != CloudEventsSpecVersion.SpecVersionAttributeName, nameof(attribute), Strings.ErrorCannotIndexBySpecVersionAttribute);

                // TODO: Is this validation definitely useful? It does mean we never return something
                // that's invalid for the attribute, which is potentially good...
                var value = attributeValues.GetValueOrDefault(attribute.Name);
                if (value is object)
                {
                    attribute.Validate(value);
                }
                return value;
            }
            set
            {
                Validation.CheckNotNull(attribute, nameof(attribute));
                Validation.CheckArgument(attribute.Name != CloudEventsSpecVersion.SpecVersionAttributeName, nameof(attribute), Strings.ErrorCannotIndexBySpecVersionAttribute);

                string name = attribute.Name;
                var knownAttribute = GetAttribute(name);

                // TODO: Are we happy to add the extension in even if the value is null?
                Validation.CheckArgument(knownAttribute is object || attribute.IsExtension,
                    nameof(attribute),
                    "Cannot add an unknown non-extension attribute to an event.");

                // If the attribute is new, or we previously had an extension attribute, replace it with our new information.
                // TODO: Alternatively, we could validate that it's got the same type... but what if it has
                // different validation criteria?
                if (knownAttribute is null || (knownAttribute.IsExtension && knownAttribute != attribute))
                {
                    extensionAttributes[name] = attribute;
                }

                if (value is null)
                {
                    attributeValues.Remove(name);
                    return;
                }
                // TODO: We could convert the attribute value here instead? Or is that a bit too much "magic"?
                attributeValues[name] = attribute.Validate(value);
            }
        }

        /// <summary>
        /// Sets or fetches the value associated with the given attribute name.
        /// Setting a value of null removes the value from the event, if it exists.
        /// If the attribute is not known in this event, fetching the value always returns null, and
        /// setting the value add a new extension attribute with the given name, and a type of string.
        /// (The value for an unknown attribute must be a string or null.)
        /// </summary>
        public object this[string attributeName]
        {
            get
            {
                // TODO: Validate the attribute name is valid (e.g. not upper case)? Seems overkill.
                Validation.CheckNotNull(attributeName, nameof(attributeName));
                Validation.CheckArgument(attributeName != CloudEventsSpecVersion.SpecVersionAttributeName, nameof(attributeName), Strings.ErrorCannotIndexBySpecVersionAttribute);
                return attributeValues.GetValueOrDefault(Validation.CheckNotNull(attributeName, nameof(attributeName)));
            }            
            set
            {
                Validation.CheckNotNull(attributeName, nameof(attributeName));
                Validation.CheckArgument(attributeName != CloudEventsSpecVersion.SpecVersionAttributeName, nameof(attributeName), Strings.ErrorCannotIndexBySpecVersionAttribute);

                var knownAttribute = GetAttribute(attributeName);

                // TODO: Are we happy to add the extension in even if the value is null?
                // (It's a simple way of populating extensions after the fact...)
                if (knownAttribute is null)
                {
                    Validation.CheckArgument(value is null || value is string,
                        nameof(value), "Cannot assign value of type {0} to unknown attribute '{1}'",
                        value.GetType(), attributeName);
                    knownAttribute = CloudEventAttribute.CreateExtension(attributeName, CloudEventAttributeType.String);
                    extensionAttributes[attributeName] = knownAttribute;
                }

                if (value is null)
                {
                    attributeValues.Remove(attributeName);
                    return;
                }
                // TODO: We could convert the attribute value here instead? Or is that a bit too much "magic"?
                attributeValues[attributeName] = knownAttribute.Validate(value);
            }
        }

        // TODO: Find everywhere that assumes data is an attribute.

        /// <summary>
        /// CloudEvent 'data' content.  The event payload. The payload depends on the type
        /// and the 'schemaurl'. It is encoded into a media format which is specified by the
        /// 'contenttype' attribute (e.g. application/json).
        /// </summary>
        /// <see href="https://github.com/cloudevents/spec/blob/master/spec.md#data-1"/>
        public object Data { get; set; }

        /// <summary>
        /// CloudEvent <see href="https://github.com/cloudevents/spec/blob/master/spec.md#id">'datacontenttype'</see> attribute.
        /// This is the content type of the <see cref="Data"/> property.
        /// This attribute enables the data attribute to carry any type of content, where the
        /// format and encoding might differ from that of the chosen event format.
        /// </summary>
        /// <see href="https://github.com/cloudevents/spec/blob/master/spec.md#contenttype"/>
        public string DataContentType
        {
            // TODO: Guard against a version that doesn't have this attribute?
            get => (string)this[SpecVersion.DataContentTypeAttribute];
            set => this[SpecVersion.DataContentTypeAttribute] = value;
        }

        /// <summary>
        /// CloudEvent <see href="https://github.com/cloudevents/spec/blob/master/spec.md#id">'id'</see> attribute,
        /// This is the ID of the event. When combined with <see cref="Source"/>, this enables deduplication.
        /// </summary>
        public string Id
        {
            get => (string)this[SpecVersion.IdAttribute];
            set => this[SpecVersion.IdAttribute] = value;
        }

        /// <summary>
        /// CloudEvents <see href="https://github.com/cloudevents/spec/blob/master/spec.md#dataschema">'dataschema'</see> attribute.
        /// A link to the schema that the data attribute adheres to.
        /// Incompatible changes to the schema SHOULD be reflected by a different URI.
        /// </summary>
        public Uri DataSchema
        {
            get => (Uri)this[SpecVersion.DataSchemaAttribute];
            set => this[SpecVersion.DataSchemaAttribute] = value;
        }

        /// <summary>
        /// CloudEvents <see href="https://github.com/cloudevents/spec/blob/master/spec.md#source">'source'</see> attribute.
        /// This describes the event producer. Often this will include information such as the type of the event source, the
        /// organization publishing the event, the process that produced the event, and some unique identifiers.
        /// When combined with <see cref="Id"/>, this enables deduplication.
        /// </summary>
        public Uri Source
        {
            get => (Uri)this[SpecVersion.SourceAttribute];
            set => this[SpecVersion.SourceAttribute] = value;
        }

        // TODO: Consider exposing publicly.
        /* FIXME: Reimplement
        internal CloudEvent WithSpecVersion(CloudEventsSpecVersion newSpecVersion) =>
            new CloudEvent(attributes.WithSpecVersion(newSpecVersion), Extensions.Values);
        */

        /// <summary>
        /// CloudEvents <see href="https://github.com/cloudevents/spec/blob/master/spec.md#subject">'subject'</see> attribute.
        /// This describes the subject of the event in the context of the event producer (identified by <see cref="Source"/>).
        /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source,
        /// but the source identifier alone might not be sufficient as a qualifier for any specific event if the source context has
        /// internal sub-structure.
        /// </summary>
        public string Subject
        {
            get => (string)this[SpecVersion.SubjectAttribute];
            set => this[SpecVersion.SubjectAttribute] = value;
        }

        /// <summary>
        /// CloudEvents <see href="https://github.com/cloudevents/spec/blob/master/spec.md#time">'time'</see> attribute.
        /// Timestamp of when the occurrence happened.
        /// </summary>
        public DateTimeOffset? Time
        {
            get => (DateTimeOffset?)this[SpecVersion.TimeAttribute];
            set => this[SpecVersion.TimeAttribute] = value;
        }

        /// <summary>
        /// CloudEvents <see href="https://github.com/cloudevents/spec/blob/master/spec.md#type">'type'</see> attribute.
        /// Type of occurrence which has happened.
        /// Often this attribute is used for routing, observability, policy enforcement, etc.
        /// </summary>
        public string Type
        {
            get => (string)this[SpecVersion.TypeAttribute];
            set => this[SpecVersion.TypeAttribute] = value;
        }

        // TODO: Should we validate that the name is a valid attribute name?

        /// <summary>
        /// Returns the attribute with the given name, which may be a standard
        /// context attribute or an extension. Note that this returns the attribute
        /// definition, not the value of the attribute.
        /// </summary>
        /// <param name="name">The attribute name to look up.</param>
        /// <returns>The attribute with the given name, or null if no this event
        /// does not know of such an attribute.</returns>
        public CloudEventAttribute GetAttribute(string name) =>
            SpecVersion.GetAttributeByName(name) ?? extensionAttributes.GetValueOrDefault(name);

        /// <summary>
        /// Returns the extension attributes known to this event, regardless of whether or not
        /// they're populated.
        /// </summary>
        public IEnumerable<CloudEventAttribute> ExtensionAttributes => extensionAttributes.Values;

        /// <summary>
        /// Returns a sequence of attributes and their values, for values which are populated in this event.
        /// This does not include the CloudEvents spec version attribute.
        /// </summary>
        public IEnumerable<KeyValuePair<CloudEventAttribute, object>> GetPopulatedAttributes()
        {
            foreach (var pair in attributeValues)
            {
                yield return new KeyValuePair<CloudEventAttribute, object>(GetAttribute(pair.Key), pair.Value);
            }
        }

        /// <summary>
        /// Sets the value for the attribute with the given name, based on its string value which is
        /// expected to be the CloudEvents canonical representation of the value.
        /// The value will be parsed and converted for non-string attributes. Unknown attributes are
        /// assumed to be string-values extension attributes.
        /// </summary>
        /// <param name="name">The name of the attribute to set. Must not be null.</param>
        /// <param name="value">The value of the attribute to set. Must not be null.</param>
        public void SetAttributeFromString(string name, string value)
        {
            Validation.CheckNotNull(name, nameof(name));
            Validation.CheckNotNull(value, nameof(value));

            var attribute = GetAttribute(name);
            if (attribute is null)
            {
                // Populate a new extension attribute with the value.
                this[name] = value;
            }
            else
            {
                // Perform any string to value parsing and validating required.
                this[attribute] = attribute.Parse(value);
            }
        }

        /// <summary>
        /// Validates that this CloudEvent is valid in the same way as <see cref="IsValid"/>,
        /// but throwing an <see cref="InvalidOperationException"/> if the event is invalid.
        /// </summary>
        /// <exception cref="InvalidOperationException">The event is invalid.</exception>
        /// <returns>A reference to the same object, for simplicity of method chaining.</returns>
        public CloudEvent Validate()
        {
            if (IsValid)
            {
                return this;
            }
            var missing = SpecVersion.RequiredAttributes.Where(attr => this[attr] is null).ToList();
            string joinedMissing = string.Join(", ", missing);
            throw new InvalidOperationException($"Missing required attributes: {joinedMissing}");
        }

        /// <summary>
        /// Returns whether this CloudEvent is valid, i.e. whether all required attributes have
        /// values.
        /// </summary>
        public bool IsValid => SpecVersion.RequiredAttributes.All(attr => this[attr] is object);
    }
}