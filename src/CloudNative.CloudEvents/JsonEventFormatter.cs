// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Mime;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Formatter that implements the JSON Event Format
    /// </summary>
    public class JsonEventFormatter : ICloudEventFormatter
    {
        public const string MediaTypeSuffix = "+json";

        public CloudEvent DecodeStructuredEvent(Stream data, params ICloudEventExtension[] extensions)
        {
            return DecodeStructuredEvent(data, (IEnumerable<ICloudEventExtension>)extensions);
        }

        public async Task<CloudEvent> DecodeStructuredEventAsync(Stream data, IEnumerable<ICloudEventExtension> extensions)
        {
            var jsonReader = new JsonTextReader(new StreamReader(data, Encoding.UTF8, true, 8192, true))
            {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };
            var jObject = await JObject.LoadAsync(jsonReader);
            return DecodeJObject(jObject, extensions);
        }

        public CloudEvent DecodeStructuredEvent(Stream data, IEnumerable<ICloudEventExtension> extensions = null)
        {
            var jsonReader = new JsonTextReader(new StreamReader(data, Encoding.UTF8, true, 8192, true))
            {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };
            var jObject = JObject.Load(jsonReader);
            return DecodeJObject(jObject, extensions);
        }

        public CloudEvent DecodeStructuredEvent(byte[] data, params ICloudEventExtension[] extensions)
        {
            return DecodeStructuredEvent(data, (IEnumerable<ICloudEventExtension>)extensions);
        }

        public CloudEvent DecodeStructuredEvent(byte[] data, IEnumerable<ICloudEventExtension> extensions = null) =>
            DecodeStructuredEvent(new MemoryStream(data), extensions);

        public CloudEvent DecodeJObject(JObject jObject, IEnumerable<ICloudEventExtension> extensions = null)
        {
            CloudEventsSpecVersion specVersion = CloudEventsSpecVersion.Default;
            if (jObject.ContainsKey(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_1)) ||
                jObject.ContainsKey(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_1).ToLowerInvariant()))
            {
                specVersion = CloudEventsSpecVersion.V0_1;
            }
            if (jObject.ContainsKey(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_2)) ||
                jObject.ContainsKey(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_2).ToLowerInvariant()))
            {
                specVersion =
                    ((string)jObject[CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_2)] ==
                     "0.2")
                        ? CloudEventsSpecVersion.V0_2 :
                        ((string)jObject[CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_3)] ==
                         "0.3")
                            ? CloudEventsSpecVersion.V0_3 : CloudEventsSpecVersion.Default;
            }

            var cloudEvent = new CloudEvent(specVersion, extensions);
            var attributes = cloudEvent.GetAttributes();
            foreach (var keyValuePair in jObject)
            {
                // skip the version since we set that above
                if (keyValuePair.Key.Equals(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_1), StringComparison.InvariantCultureIgnoreCase) ||
                    keyValuePair.Key.Equals(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_2), StringComparison.InvariantCultureIgnoreCase) ||
                    keyValuePair.Key.Equals(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V1_0), StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                if (specVersion == CloudEventsSpecVersion.V1_0)
                {
                    // handle base64 encoded binaries
                    if (keyValuePair.Key.Equals("data_base64"))
                    {
                        attributes["data"] = Convert.FromBase64String(keyValuePair.Value.ToString());
                        continue;
                    }
                }

                switch (keyValuePair.Value.Type)
                {
                    case JTokenType.String:
                        attributes[keyValuePair.Key] = keyValuePair.Value.ToObject<string>();
                        break;
                    case JTokenType.Date:
                        // TODO: Check this is appropriate. (Should we use Timestamps instead?)
                        attributes[keyValuePair.Key] = keyValuePair.Value.ToObject<DateTimeOffset>();
                        break;
                    case JTokenType.Uri:
                        attributes[keyValuePair.Key] = keyValuePair.Value.ToObject<Uri>();
                        break;
                    case JTokenType.Null:
                        attributes[keyValuePair.Key] = null;
                        break;
                    case JTokenType.Integer:
                        attributes[keyValuePair.Key] = keyValuePair.Value.ToObject<int>();
                        break;
                    default:
                        attributes[keyValuePair.Key] = (dynamic)keyValuePair.Value;
                        break;
                }
            }

            return cloudEvent;
        }

        public byte[] EncodeStructuredEvent(CloudEvent cloudEvent, out ContentType contentType)
        {
            contentType = new ContentType("application/cloudevents+json")
            {
                CharSet = Encoding.UTF8.WebName
            };

            JObject jObject = new JObject();
            var attributes = cloudEvent.GetAttributes();
            foreach (var keyValuePair in attributes)
            {
                if (keyValuePair.Value == null)
                {
                    continue;
                }

                if (keyValuePair.Value is ContentType contentTypeValue && !string.IsNullOrEmpty(contentTypeValue.MediaType))
                {
                    jObject[keyValuePair.Key] = JToken.FromObject(contentTypeValue.ToString());
                }
                else if (cloudEvent.SpecVersion == CloudEventsSpecVersion.V1_0 &&
                         keyValuePair.Key.Equals(CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion)))
                {
                    if (keyValuePair.Value is Stream stream)
                    {
                        using (var sr = new BinaryReader(stream))
                        {
                            jObject["data_base64"] = Convert.ToBase64String(sr.ReadBytes((int)sr.BaseStream.Length));
                        }
                    }
                    else if (keyValuePair.Value is IEnumerable<byte> bytes)
                    {
                        // TODO: Avoid creating a copy if it's already a byte array.
                        jObject["data_base64"] = Convert.ToBase64String(bytes.ToArray());
                    }
                    else
                    {
                        jObject["data"] = JToken.FromObject(keyValuePair.Value);
                    }   
                }
                else
                {
                    jObject[keyValuePair.Key] = JToken.FromObject(keyValuePair.Value);
                }
            }
            return Encoding.UTF8.GetBytes(jObject.ToString());
        }

        public object DecodeAttribute(CloudEventsSpecVersion specVersion, string name, byte[] data, IEnumerable<ICloudEventExtension> extensions = null)
        {
            if (name.Equals(CloudEventAttributes.IdAttributeName(specVersion)) ||
                name.Equals(CloudEventAttributes.TypeAttributeName(specVersion)) ||
                name.Equals(CloudEventAttributes.SubjectAttributeName(specVersion)))
            {
                return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), typeof(string));
            }

            if (name.Equals(CloudEventAttributes.TimeAttributeName(specVersion)))
            {
                return Timestamps.Parse(Encoding.UTF8.GetString(data));
            }

            if (name.Equals(CloudEventAttributes.SourceAttributeName(specVersion)) ||
                name.Equals(CloudEventAttributes.DataSchemaAttributeName(specVersion)))
            {
                var uri = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), typeof(string)) as string;
                return new Uri(uri);
            }

            if (name.Equals(CloudEventAttributes.DataContentTypeAttributeName(specVersion)))
            {
                var s = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), typeof(string)) as string;
                return new ContentType(s);
            }

            if (extensions != null)
            {
                foreach (var extension in extensions)
                {
                    // TODO: Use appropriate parsing, e.g. for timestamps
                    Type type = extension.GetAttributeType(name);
                    if (type != null)
                    {
                        return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), type);
                    }
                }
            }
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data));
        }

        public byte[] EncodeAttribute(CloudEventsSpecVersion specVersion, string name, object value, IEnumerable<ICloudEventExtension> extensions = null)
        {
            if (name.Equals(CloudEventAttributes.DataAttributeName(specVersion)))
            {
                if (value is Stream)
                {
                    using (var buffer = new MemoryStream())
                    {
                        ((Stream)value).CopyTo(buffer);
                        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(buffer.ToArray()));
                    }
                }
            }

            if (extensions != null)
            {
                foreach (var extension in extensions)
                {
                    Type type = extension.GetAttributeType(name);
                    if (type != null)
                    {
                        // TODO: Use appropriate formatting, e.g. for timestamps
                        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Convert.ChangeType(value, type)));
                    }
                }
            }

            if (value is DateTimeOffset dto)
            {
                return Encoding.UTF8.GetBytes(Timestamps.Format(dto));
            }

            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));
        }
    }
}