
namespace CloudNative.CloudEvents
{
    using System;
    using System.Collections.Generic;
    using System.Net.Mime;

    public class CloudEvent
    {
        readonly IDictionary<string, object> attributes;

        internal CloudEvent(params ICloudEventExtension[] extensions)
        {
            attributes = new CloudEventAttributes(extensions);
            SpecVersion = "0.1";
            this.Extensions = new Dictionary<Type, ICloudEventExtension>();
            foreach (var extension in extensions)
            {
                this.Extensions.Add(extension.GetType(), extension);
                extension.Attach(this);
            }
        }

        public CloudEvent(string type, Uri source, string id = null, DateTime? time = null, params ICloudEventExtension[] extensions) : this(extensions)
        {
            Type = type;
            Source = source;

            if (id == null)
            {
                Id = Guid.NewGuid().ToString();
            }
            else
            {
                Id = id;
            }

            if (time.HasValue)
            {
                Time = time.Value;
            }
            else
            {
                Time = DateTime.UtcNow;
            }
        }

        public Dictionary<Type, ICloudEventExtension> Extensions { get; private set; }

        public T Extension<T>()
        {
            return (T)Extensions[typeof(T)];
        }

        public string Type
        {
            get => attributes[CloudEventAttributes.TypeAttributeName] as string;
            set => attributes[CloudEventAttributes.TypeAttributeName] = value;
        }

        public string SpecVersion
        {
            get => attributes[CloudEventAttributes.SpecVersionAttributeName] as string;
            set => attributes[CloudEventAttributes.SpecVersionAttributeName] = value;
        }

        public Uri Source
        {
            get => attributes[CloudEventAttributes.SourceAttributeName] as Uri;
            set => attributes[CloudEventAttributes.SourceAttributeName] = value;
        }

        public string Id
        {
            get => attributes[CloudEventAttributes.IdAttributeName] as string;
            set => attributes[CloudEventAttributes.IdAttributeName] = value;
        }

        public DateTime? Time
        {
            get => (DateTime?)attributes[CloudEventAttributes.TimeAttributeName];
            set => attributes[CloudEventAttributes.TimeAttributeName] = value;
        }

        public Uri SchemaUrl
        {
            get => attributes[CloudEventAttributes.SchemaUrlAttributeName] as Uri;
            set => attributes[CloudEventAttributes.SchemaUrlAttributeName] = value;
        }

        public ContentType ContentType
        {
            get => attributes[CloudEventAttributes.ContentTypeAttributeName] as ContentType;
            set => attributes[CloudEventAttributes.ContentTypeAttributeName] = value;
        }

        public object Data
        {
            get => attributes[CloudEventAttributes.DataAttributeName];
            set => attributes[CloudEventAttributes.DataAttributeName] = value;
        }

        public IDictionary<string, object> GetAttributes()
        {
            return attributes;
        }
    }
}
