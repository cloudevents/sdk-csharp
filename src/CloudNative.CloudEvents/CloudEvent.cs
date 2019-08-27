// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;
    using System.Collections.Generic;
    using System.Net.Mime;

    /// <summary>
    /// Represents a CloudEvent 
    /// </summary>
    public class CloudEvent
    {
        public const string MediaType = "application/cloudevents";

        readonly CloudEventAttributes attributes;

        /// <summary>
        /// Create a new CloudEvent instance.
        /// </summary>
        /// <param name="type">'type' of the CloudEvent</param>
        /// <param name="source">'source' of the CloudEvent</param>
        /// <param name="id">'id' of the CloudEvent</param>
        /// <param name="time">'time' of the CloudEvent</param>
        /// <param name="extensions">Extensions to be added to this CloudEvents</param>
        public CloudEvent(string type, Uri source, string id = null, DateTime? time = null,
            params ICloudEventExtension[] extensions) : this(CloudEventsSpecVersion.Default, type, source, id, time, extensions)
        {
        }

        /// <summary>
        /// Create a new CloudEvent instance.
        /// </summary>
        /// <param name="specVersion">CloudEvents specification version</param>
        /// <param name="type">'type' of the CloudEvent</param>
        /// <param name="source">'source' of the CloudEvent</param>
        /// <param name="id">'id' of the CloudEvent</param>
        /// <param name="time">'time' of the CloudEvent</param>
        /// <param name="extensions">Extensions to be added to this CloudEvents</param>
        public CloudEvent(CloudEventsSpecVersion specVersion, string type, Uri source, string id = null, DateTime? time = null,
            params ICloudEventExtension[] extensions) : this(specVersion, extensions)
        {
            Type = type;
            Source = source;
            Id = id ?? Guid.NewGuid().ToString();
            Time = time ?? DateTime.UtcNow;
        }

        /// <summary>
        /// Create a new CloudEvent instance.
        /// </summary>
        /// <param name="specVersion">CloudEvents specification version</param>
        /// <param name="type">'type' of the CloudEvent</param>
        /// <param name="source">'source' of the CloudEvent</param>
        /// <param name="subject">'subject' of the CloudEvent</param>
        /// <param name="id">'id' of the CloudEvent</param>
        /// <param name="time">'time' of the CloudEvent</param>
        /// <param name="extensions">Extensions to be added to this CloudEvents</param>
        public CloudEvent(CloudEventsSpecVersion specVersion, string type, Uri source, string subject, string id = null, DateTime? time = null,
            params ICloudEventExtension[] extensions) : this(specVersion, type, source, id, time, extensions)
        {
            Subject = subject;
        }

        /// <summary>
        /// Create a new CloudEvent instance
        /// </summary>
        /// <param name="specVersion">CloudEvents specification version</param>
        /// <param name="extensions">Extensions to be added to this CloudEvents</param>
        internal CloudEvent(CloudEventsSpecVersion specVersion, IEnumerable<ICloudEventExtension> extensions)
        {
            attributes = new CloudEventAttributes(specVersion, extensions);
            this.Extensions = new Dictionary<Type, ICloudEventExtension>();
            if (extensions != null)
            {
                foreach (var extension in extensions)
                {
                    this.Extensions.Add(extension.GetType(), extension);
                    extension.Attach(this);
                }
            }
        }

        /// <summary>
        /// CloudEvent 'datacontenttype' attribute. Content type of the 'data' attribute value.
        /// This attribute enables the data attribute to carry any type of content, whereby
        /// format and encoding might differ from that of the chosen event format.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#datacontenttype"/>
        public ContentType DataContentType
        {
            get => attributes[CloudEventAttributes.DataContentTypeAttributeName(attributes.SpecVersion)] as ContentType;
            set => attributes[CloudEventAttributes.DataContentTypeAttributeName(attributes.SpecVersion)] = value;
        }

        /// <summary>
        /// CloudEvent 'datacontentencoding' attribute.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#datacontentencoding"/>
        public string DataContentEncoding
        {
            get => attributes[CloudEventAttributes.DataContentEncodingAttributeName(attributes.SpecVersion)] as string;
            set => attributes[CloudEventAttributes.DataContentEncodingAttributeName(attributes.SpecVersion)] = value;
        }

        [Obsolete("Cloud events 0.1 and 0.2 name replaced by 'DataContentType1'. Will be removed in an upcoming release.")]
        public ContentType ContentType
        {
            get => DataContentType;
            set => DataContentType = value;
        }

        /// <summary>
        /// CloudEvent 'data' content.  The event payload. The payload depends on the type
        /// and the 'schemaurl'. It is encoded into a media format which is specified by the
        /// 'contenttype' attribute (e.g. application/json).
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#data-1"/>
        public object Data
        {
            get => attributes[CloudEventAttributes.DataAttributeName(attributes.SpecVersion)];
            set => attributes[CloudEventAttributes.DataAttributeName(attributes.SpecVersion)] = value;
        }

        /// <summary>
        /// Extensions registered with this event. 
        /// </summary>
        protected internal Dictionary<Type, ICloudEventExtension> Extensions { get; private set; }

        /// <summary>
        /// CloudEvent 'id' attribute. ID of the event. The semantics of this string are explicitly
        /// undefined to ease the implementation of producers. Enables deduplication.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#id"/>
        public string Id
        {
            get => attributes[CloudEventAttributes.IdAttributeName(attributes.SpecVersion)] as string;
            set => attributes[CloudEventAttributes.IdAttributeName(attributes.SpecVersion)] = value;
        }

        /// <summary>
        /// CloudEvents 'schemaurl' attribute. A link to the schema that the data attribute
        /// adheres to. Incompatible changes to the schema SHOULD be reflected by a
        /// different URL.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#schemaurl"/>
        public Uri SchemaUrl
        {
            get => attributes[CloudEventAttributes.SchemaUrlAttributeName(attributes.SpecVersion)] as Uri;
            set => attributes[CloudEventAttributes.SchemaUrlAttributeName(attributes.SpecVersion)] = value;
        }

        /// <summary>
        /// CloudEvents 'subject' attribute. 
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#subject"/>
        public string Subject
        {
            get => attributes[CloudEventAttributes.SubjectAttributeName(attributes.SpecVersion)] as string;
            set => attributes[CloudEventAttributes.SubjectAttributeName(attributes.SpecVersion)] = value;
        }

        /// <summary>
        /// CloudEvents 'source' attribute. This describes the event producer. Often this
        /// will include information such as the type of the event source, the
        /// organization publishing the event, the process that produced the
        /// event, and some unique identifiers.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#source"/>
        public Uri Source
        {
            get => attributes[CloudEventAttributes.SourceAttributeName(attributes.SpecVersion)] as Uri;
            set => attributes[CloudEventAttributes.SourceAttributeName(attributes.SpecVersion)] = value;
        }

        /// <summary>
        /// CloudEvents 'specversion' attribute. The version of the CloudEvents
        /// specification which the event uses. This enables the interpretation of the context.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#specversion"/>
        public CloudEventsSpecVersion SpecVersion
        {
            get => attributes.SpecVersion;
            set => attributes.SpecVersion = value;
        }

        /// <summary>
        /// CloudEvents 'time' attribute. Timestamp of when the event happened.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#time"/>
        public DateTime? Time
        {
            get => (DateTime?)attributes[CloudEventAttributes.TimeAttributeName(attributes.SpecVersion)];
            set => attributes[CloudEventAttributes.TimeAttributeName(attributes.SpecVersion)] = value;
        }

        /// <summary>
        /// CloudEvents 'type' attribute. Type of occurrence which has happened.
        /// Often this attribute is used for routing, observability, policy enforcement, etc.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#type"/>
        public string Type
        {
            get => attributes[CloudEventAttributes.TypeAttributeName(attributes.SpecVersion)] as string;
            set => attributes[CloudEventAttributes.TypeAttributeName(attributes.SpecVersion)] = value;
        }

        /// <summary>
        /// Use this method to access extensions added to this event.
        /// </summary>
        /// <typeparam name="T">Type of the extension class</typeparam>
        /// <returns>Extension instance if registered</returns>
        public T Extension<T>()
        {
            var key = typeof(T);
            if (Extensions.TryGetValue(key, out var extension))
            {
                return (T)extension;
            }

            return default(T);
        }

        /// <summary>
        /// Provides direct access to the attribute collection.
        /// </summary>
        /// <returns>Attribute collection</returns>
        public IDictionary<string, object> GetAttributes()
        {
            return attributes;
        }
    }
}