// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;
    using System.Collections.Generic;
    using System.Net.Mime;

    public class CloudEvent
    {
        public const string MediaType = "application/cloudevents";

        readonly IDictionary<string, object> attributes;

        /// <summary>
        /// Create a new CloudEvent instance.
        /// </summary>
        /// <param name="type">'type' of the CloudEvent</param>
        /// <param name="source">'source' of the CloudEvent</param>
        /// <param name="id">'id' of the CloudEvent</param>
        /// <param name="time">'time' of the CloudEvent</param>
        /// <param name="extensions">Extensions to be added to this CloudEvents</param>
        public CloudEvent(string type, Uri source, string id = null, DateTime? time = null,
            params ICloudEventExtension[] extensions) : this(extensions)
        {
            Type = type;
            Source = source;
            Id = id ?? Guid.NewGuid().ToString();
            Time = time ?? DateTime.UtcNow;
        }

        /// <summary>
        /// Create a new CloudEvent instance
        /// </summary>
        /// <param name="extensions">Extensions to be added to this CloudEvents</param>
        internal CloudEvent(IEnumerable<ICloudEventExtension> extensions)
        {
            attributes = new CloudEventAttributes(extensions);
            SpecVersion = "0.2";
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
        /// CloudEvent 'contenttype' attribute. Content type of the 'data' attribute value.
        /// This attribute enables the data attribute to carry any type of content, whereby
        /// format and encoding might differ from that of the chosen event format.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#contenttype"/>
        public ContentType ContentType
        {
            get => attributes[CloudEventAttributes.ContentTypeAttributeName] as ContentType;
            set => attributes[CloudEventAttributes.ContentTypeAttributeName] = value;
        }

        /// <summary>
        /// CloudEvent 'data' content.  The event payload. The payload depends on the type
        /// and the 'schemaurl'. It is encoded into a media format which is specified by the
        /// 'contenttype' attribute (e.g. application/json).
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#data-1"/>
        public object Data
        {
            get => attributes[CloudEventAttributes.DataAttributeName];
            set => attributes[CloudEventAttributes.DataAttributeName] = value;
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
            get => attributes[CloudEventAttributes.IdAttributeName] as string;
            set => attributes[CloudEventAttributes.IdAttributeName] = value;
        }

        /// <summary>
        /// CloudEvents 'schemaurl' attribute. A link to the schema that the data attribute
        /// adheres to. Incompatible changes to the schema SHOULD be reflected by a
        /// different URL.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#schemaurl"/>
        public Uri SchemaUrl
        {
            get => attributes[CloudEventAttributes.SchemaUrlAttributeName] as Uri;
            set => attributes[CloudEventAttributes.SchemaUrlAttributeName] = value;
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
            get => attributes[CloudEventAttributes.SourceAttributeName] as Uri;
            set => attributes[CloudEventAttributes.SourceAttributeName] = value;
        }

        /// <summary>
        /// CloudEvents 'specversion' attribute. The version of the CloudEvents
        /// specification which the event uses. This enables the interpretation of the context.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#specversion"/>
        public string SpecVersion
        {
            get => attributes[CloudEventAttributes.SpecVersionAttributeName] as string;
            set => attributes[CloudEventAttributes.SpecVersionAttributeName] = value;
        }

        /// <summary>
        /// CloudEvents 'time' attribute. Timestamp of when the event happened.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#time"/>
        public DateTime? Time
        {
            get => (DateTime?)attributes[CloudEventAttributes.TimeAttributeName];
            set => attributes[CloudEventAttributes.TimeAttributeName] = value;
        }

        /// <summary>
        /// CloudEvents 'type' attribute. Type of occurrence which has happened.
        /// Often this attribute is used for routing, observability, policy enforcement, etc.
        /// </summary>
        /// <see cref="https://github.com/cloudevents/spec/blob/master/spec.md#type"/>
        public string Type
        {
            get => attributes[CloudEventAttributes.TypeAttributeName] as string;
            set => attributes[CloudEventAttributes.TypeAttributeName] = value;
        }

        /// <summary>
        /// Use this method to access extensions added to this event.
        /// </summary>
        /// <typeparam name="T">Type of the extension class</typeparam>
        /// <returns>Extension instance if registered</returns>
        public T Extension<T>()
        {
            return (T)Extensions[typeof(T)];
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