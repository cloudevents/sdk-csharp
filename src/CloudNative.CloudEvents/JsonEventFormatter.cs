// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;
    using System.IO;
    using System.Net.Mime;
    using System.Text;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class JsonEventFormatter : ICloudEventFormatter
    {
        public CloudEvent DecodeStructuredEvent(Stream data, params ICloudEventExtension[] extensions)
        {
            var jsonReader = new JsonTextReader(new StreamReader(data, Encoding.UTF8, true, 8192, true));
            var jObject = JObject.Load(jsonReader);
            return DecodeJObject(jObject, extensions);
        }

        public CloudEvent DecodeStructuredEvent(byte[] data, params ICloudEventExtension[] extensions)
        {
            var jsonText = Encoding.UTF8.GetString(data);
            var jObject = JObject.Parse(jsonText);
            return DecodeJObject(jObject, extensions);
        }

        public CloudEvent DecodeJObject(JObject jObject, params ICloudEventExtension[] extensions)
        {
            var cloudEvent = new CloudEvent(extensions);
            var attributes = cloudEvent.GetAttributes();
            attributes.Clear();
            foreach (var keyValuePair in jObject)
            {
                switch (keyValuePair.Value.Type)
                {
                    case JTokenType.String:
                        attributes[keyValuePair.Key] = keyValuePair.Value.ToObject<string>();
                        break;
                    case JTokenType.Date:
                        attributes[keyValuePair.Key] = keyValuePair.Value.ToObject<DateTime>();
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
                CharSet = Encoding.UTF8.EncodingName
            };

            JObject jObject = new JObject();
            var attributes = cloudEvent.GetAttributes();
            foreach (var keyValuePair in attributes)
            {
                if (keyValuePair.Value is ContentType)
                {
                    jObject[keyValuePair.Key] = JToken.FromObject(((ContentType)keyValuePair.Value).ToString());
                }
                else
                {
                    jObject[keyValuePair.Key] = JToken.FromObject(keyValuePair.Value);
                }
            }
            return Encoding.UTF8.GetBytes(jObject.ToString());
        }

        public object DecodeAttribute(string name, byte[] data)
        {
            throw new NotImplementedException();
        }

        public byte[] EncodeAttribute(string name, object value)
        {
            throw new NotImplementedException();
        }
    }
}