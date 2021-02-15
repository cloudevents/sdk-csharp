// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.NewtonsoftJson
{
    /// <summary>
    /// Formatter that implements the JSON Event Format
    /// </summary>
    public class JsonEventFormatter : CloudEventFormatter
    {
        private const string DataBase64 = "data_base64";
        private const string Data = "data";
        public const string MediaTypeSuffix = "+json";

        public CloudEvent DecodeStructuredEvent(Stream data, params CloudEventAttribute[] extensionAttributes) =>
            DecodeStructuredEvent(data, (IEnumerable<CloudEventAttribute>) extensionAttributes);

        public override async Task<CloudEvent> DecodeStructuredEventAsync(Stream data, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            var jsonReader = new JsonTextReader(new StreamReader(data, Encoding.UTF8, true, 8192, true))
            {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };
            var jObject = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            return DecodeJObject(jObject, extensionAttributes);
        }

        public override CloudEvent DecodeStructuredEvent(Stream data, IEnumerable<CloudEventAttribute> extensionAttributes = null)
        {
            var jsonReader = new JsonTextReader(new StreamReader(data, Encoding.UTF8, true, 8192, true))
            {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };
            var jObject = JObject.Load(jsonReader);
            return DecodeJObject(jObject, extensionAttributes);
        }

        public CloudEvent DecodeStructuredEvent(byte[] data, params CloudEventAttribute[] extensionAttributes) =>
            DecodeStructuredEvent(data, (IEnumerable<CloudEventAttribute>)extensionAttributes);

        public override CloudEvent DecodeStructuredEvent(byte[] data, IEnumerable<CloudEventAttribute> extensionAttributes = null) =>
            DecodeStructuredEvent(new MemoryStream(data), extensionAttributes);

        // TODO: If we make this private, we'll have significantly more control over what token types we see.
        // For example, we could turn off date parsing entirely, and we may never get "Uri" tokens either.
        public CloudEvent DecodeJObject(JObject jObject, IEnumerable<CloudEventAttribute> extensionAttributes = null)
        {
            CloudEventsSpecVersion specVersion = CloudEventsSpecVersion.Default;
            if (jObject.TryGetValue(CloudEventsSpecVersion.SpecVersionAttribute.Name, out var specVersionToken))
            {
                string versionId = (string)specVersionToken;
                specVersion = CloudEventsSpecVersion.FromVersionId(versionId);
                // TODO: Throw if specVersion is null?
            }

            var cloudEvent = new CloudEvent(specVersion, extensionAttributes);
            foreach (var keyValuePair in jObject)
            {
                var key = keyValuePair.Key;
                var value = keyValuePair.Value;

                // Skip the spec version attribute, which we've already taken account of.
                if (key == CloudEventsSpecVersion.SpecVersionAttribute.Name)
                {
                    continue;
                }

                // TODO: Is the data_base64 name version-specific?
                if (specVersion == CloudEventsSpecVersion.V1_0 && key == DataBase64)
                {
                    // Handle base64 encoded binaries
                    cloudEvent.Data = Convert.FromBase64String((string)value);
                    continue;
                }
                if (key == Data)
                {
                    // FIXME: Deserialize where appropriate.
                    // Consider whether there are any options here to consider beyond "string" and "object".
                    // (e.g. arrays, numbers etc).
                    // Note: the cast to "object" is important here, otherwise the string branch is implicitly
                    // converted back to JToken...
                    cloudEvent.Data = value.Type == JTokenType.String ? (string) value : (object) value;
                    continue;
                }

                var attribute = cloudEvent.GetAttribute(key);

                // Set the attribute in the event, taking account of mismatches between the type in the JObject
                // and the attribute type as best we can.

                // TODO: This currently performs more conversions than it really should, in the cause of simplicity.
                // We basically need a matrix of "attribute type vs token type" but that's rather complicated.

                string attributeValue = value.Type switch
                {
                    JTokenType.String => (string)value,
                    JTokenType.Date => CloudEventAttributeType.Timestamp.Format((DateTimeOffset)value),
                    JTokenType.Uri => CloudEventAttributeType.UriReference.Format((Uri)value),
                    JTokenType.Null => null, // TODO: Check we want to do this. It's a bit weird.
                    JTokenType.Integer => CloudEventAttributeType.Integer.Format((int)value),
                    _ => throw new ArgumentException($"Invalid token type '{value.Type}' for CloudEvent attribute")
                };

                cloudEvent.SetAttributeFromString(key, attributeValue);
            }

            return cloudEvent;
        }

        public override byte[] EncodeStructuredEvent(CloudEvent cloudEvent, out ContentType contentType)
        {
            contentType = new ContentType("application/cloudevents+json")
            {
                CharSet = Encoding.UTF8.WebName
            };

            JObject jObject = new JObject();
            var attributes = cloudEvent.GetPopulatedAttributes();
            foreach (var keyValuePair in attributes)
            {
                jObject[keyValuePair.Key.Name] = JToken.FromObject(keyValuePair.Value);
            }

            // FIXME: This is all a bit arbitrary.
            if (cloudEvent.Data is object)
            {
                // FIXME: This assumes there's nothing beyond the media type...
                if (cloudEvent.DataContentType == "application/json")
                {
                    jObject[Data] = JToken.FromObject(cloudEvent.Data);
                }
                else if (cloudEvent.Data is string text && cloudEvent.DataContentType?.StartsWith("text/") == true)
                {
                    jObject[Data] = text;
                }
                else if (cloudEvent.Data is byte[] binary)
                {
                    jObject[DataBase64] = Convert.ToBase64String(binary);
                }
                else
                {
                    throw new ArgumentException($"{nameof(JsonEventFormatter)} cannot serialize data of type {cloudEvent.Data.GetType()} with content type {cloudEvent.DataContentType}");
                }
            }

            return Encoding.UTF8.GetBytes(jObject.ToString());
        }

        // TODO: How should the caller know whether the result is "raw" or should be stored in data_base64?
        public override byte[] EncodeData(object value)
        {
            // TODO: Check this is what we want.
            // In particular, if this is just other text or binary data, rather than JSON, what does it
            // mean to have a JSON event format?
            string json = value switch
            {
                JToken token => token.ToString(), // Formatting?
                string text => text,
                byte[] data => Convert.ToBase64String(data),
                null => null,
                _ => JsonConvert.SerializeObject(value)
            };
            return json is null ? new byte[0] : Encoding.UTF8.GetBytes(json);
        }

        public override object DecodeData(byte[] value, string contentType)
        {
            if (contentType == "application/json")
            {
                var jsonReader = new JsonTextReader(new StreamReader(new MemoryStream(value), Encoding.UTF8, true, 8192, true))
                {
                    DateParseHandling = DateParseHandling.DateTimeOffset
                };
                return JToken.Load(jsonReader);
            }
            else if (contentType?.StartsWith("text/") == true)
            {
                // FIXME: Even if we want to do this, we really need to know if there's a content encoding.
                return Encoding.UTF8.GetString(value);
            }
            // TODO: Clone?
            return value;
        }
    }
}