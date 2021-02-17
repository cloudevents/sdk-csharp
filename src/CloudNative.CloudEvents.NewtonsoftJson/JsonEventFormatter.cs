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
    /// Formatter that implements the JSON Event Format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When encoding CloudEvent data, the behavior depends on the data content type of the CloudEvent
    /// and the type of the <see cref="CloudEvent.Data"/> property value, following the rules below.
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// If the data value is null, the content is empty for a binary mode message, and neither the "data"
    /// nor "data_base64" property is populated in a structured mode message.
    /// </description></item>
    /// <item><description>
    /// If the data content type is absent or has a media type of "application/json", the data is encoded as JSON.
    /// If the data is already a <see cref="JToken"/>, that is serialized directly as JSON. Otherwise, the data
    /// is converted using the <see cref="JsonSerializer"/> passed into the constructor, or a
    /// default serializer.
    /// </description></item>
    /// <item><description>
    /// Otherwise, if the data content type has a media type beginning with "text/" and the data value is a string,
    /// the data is serialized as a string.
    /// </description></item>
    /// <item><description>
    /// Otherwise, if the data value is a byte array, it is serialized either directly as binary data
    /// (for binary mode messages) or as base64 data (for structured mode messages).
    /// </description></item>
    /// <item><description>
    /// Otherwise, the encoding operation fails.
    /// </description></item>
    /// </list>
    /// <para>
    /// When decoding CloudEvent data, the following rules are used:
    /// </para>
    /// <para>
    /// In a structured mode message, any data is either binary data within the "data_base64" property value,
    /// or is a JSON token as the "data" property value. Binary data is represented as a byte array.
    /// A JSON token is decoded as a string if is just a string value and the data content type is specified
    /// and has a media type beginning with "text/". A JSON token representing the null value always
    /// leads to a null data result. In any other situation, the JSON token is preserved as a <see cref="JToken"/>
    /// that can be used for further deserialization (e.g. to a specific CLR type).
    /// </para>
    /// <para>
    /// In a binary mode message, the data is parsed based on the content type of the message. When the content
    /// type is absent or has a media type of "application/json", the data is parsed as JSON, with the result as
    /// a <see cref="JToken"/>. When the content type has a media type beginning with "text/", the data is parsed
    /// as a string. In all other cases, the data is left as a byte array.
    /// </para>
    /// </remarks>
    public class JsonEventFormatter : CloudEventFormatter
    {
        private const string JsonMediaType = "application/json";
        private const string DataBase64 = "data_base64";
        private const string Data = "data";
        private const string MediaTypeSuffix = "+json";

        private readonly JsonSerializer serializer;

        /// <summary>
        /// Creates a JsonEventFormatter that uses a default <see cref="JsonSerializer"/>.
        /// </summary>
        public JsonEventFormatter() : this(JsonSerializer.CreateDefault())
        {
        }

        /// <summary>
        /// Creates a JsonEventFormatter that uses the specified <see cref="JsonSerializer"/>
        /// to serialize objects as JSON.
        /// </summary>
        public JsonEventFormatter(JsonSerializer serializer)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public override async Task<CloudEvent> DecodeStructuredModeMessageAsync(Stream data, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            var jsonReader = CreateJsonReader(data, contentType.GetEncoding());
            var jObject = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            return DecodeJObject(jObject, extensionAttributes);
        }

        public override CloudEvent DecodeStructuredModeMessage(Stream data, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes)
        {
            var jsonReader = CreateJsonReader(data, contentType.GetEncoding());
            var jObject = JObject.Load(jsonReader);
            return DecodeJObject(jObject, extensionAttributes);
        }

        public override CloudEvent DecodeStructuredModeMessage(byte[] data, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes) =>
            DecodeStructuredModeMessage(new MemoryStream(data), contentType, extensionAttributes);

        private CloudEvent DecodeJObject(JObject jObject, IEnumerable<CloudEventAttribute> extensionAttributes = null)
        {
            if (!jObject.TryGetValue(CloudEventsSpecVersion.SpecVersionAttribute.Name, out var specVersionToken)
                || specVersionToken.Type != JTokenType.String)
            {
                throw new ArgumentException($"Structured mode content does not represent a CloudEvent");
            }
            var specVersion = CloudEventsSpecVersion.FromVersionId((string) specVersionToken);
            if (specVersion is null)
            {
                throw new ArgumentException($"Unsupported CloudEvents spec version '{(string)specVersionToken}'");
            }

            var cloudEvent = new CloudEvent(specVersion, extensionAttributes);
            PopulateAttributesFromStructuredEvent(cloudEvent, jObject);
            PopulateDataFromStructuredEvent(cloudEvent, jObject);

            return cloudEvent;
        }

        private void PopulateAttributesFromStructuredEvent(CloudEvent cloudEvent, JObject jObject)
        {
            foreach (var keyValuePair in jObject)
            {
                var key = keyValuePair.Key;
                var value = keyValuePair.Value;

                // Skip the spec version attribute, which we've already taken account of.
                // Data is handled later, when everything else (importantly, the data content type)
                // has been populated.
                if (key == CloudEventsSpecVersion.SpecVersionAttribute.Name ||
                    key == DataBase64 ||
                    key == Data)
                {
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
                    JTokenType.Boolean => CloudEventAttributeType.Boolean.Format((bool)value),
                    JTokenType.Null => null, // TODO: Check we want to do this. It's a bit weird.
                    JTokenType.Integer => CloudEventAttributeType.Integer.Format((int)value),
                    _ => throw new ArgumentException($"Invalid token type '{value.Type}' for CloudEvent attribute")
                };
                // Note: we *could* infer an extension type of integer and Boolean, but not other extension types.
                // (We don't want to assume that everything that looks like a timestamp is a timestamp, etc.)
                // Stick to strings for consistency.
                cloudEvent.SetAttributeFromString(key, attributeValue);
            }
        }

        private void PopulateDataFromStructuredEvent(CloudEvent cloudEvent, JObject jObject)
        {
            jObject.TryGetValue(Data, out var dataToken);
            jObject.TryGetValue(DataBase64, out var dataBase64Token);
            if (dataToken is null && dataBase64Token is null)
            {
                return;
            }
            if (dataToken is object && dataBase64Token is object)
            {
                throw new ArgumentException($"Structured mode content cannot contain both '{Data}' and '{DataBase64}' properties.");
            }
            if (dataBase64Token is object)
            {
                if (dataBase64Token.Type != JTokenType.String)
                {
                    throw new ArgumentException($"Structured mode property '{DataBase64}' must be a string, when present.");
                }
                cloudEvent.Data = Convert.FromBase64String((string) dataBase64Token);
            }
            else
            {
                if (dataToken.Type == JTokenType.Null)
                {
                    return;
                }
                // Convert JSON string tokens to string values when the content type suggests that's appropriate,
                // otherwise leave the token as it is.
                cloudEvent.Data = dataToken.Type == JTokenType.String && cloudEvent.DataContentType?.StartsWith("text/") == true
                    ? (string) dataToken
                    : (object) dataToken; // Deliberately cast to object to avoid any implicit conversions
            }
        }

        public override byte[] EncodeStructuredModeMessage(CloudEvent cloudEvent, out ContentType contentType)
        {
            contentType = new ContentType("application/cloudevents+json")
            {
                CharSet = Encoding.UTF8.WebName
            };

            JObject jObject = new JObject();
            jObject[CloudEventsSpecVersion.SpecVersionAttribute.Name] = cloudEvent.SpecVersion.VersionId;
            var attributes = cloudEvent.GetPopulatedAttributes();
            foreach (var keyValuePair in attributes)
            {
                jObject[keyValuePair.Key.Name] = JToken.FromObject(keyValuePair.Value);
            }

            if (cloudEvent.Data is object)
            {
                ContentType dataContentType = new ContentType(cloudEvent.DataContentType ?? JsonMediaType);
                if (dataContentType.MediaType == JsonMediaType)
                {
                    jObject[Data] = cloudEvent.Data is JToken token
                        ? token
                        : JToken.FromObject(cloudEvent.Data);
                }
                else if (cloudEvent.Data is string text && dataContentType.MediaType.StartsWith("text/"))
                {
                    jObject[Data] = text;
                }
                else if (cloudEvent.Data is byte[] binary)
                {
                    jObject[DataBase64] = Convert.ToBase64String(binary);
                }
                else
                {
                    throw new ArgumentException($"{nameof(JsonEventFormatter)} cannot serialize data of type {cloudEvent.Data.GetType()} with content type '{cloudEvent.DataContentType}'");
                }
            }

            return Encoding.UTF8.GetBytes(jObject.ToString());
        }

        // TODO: How should the caller know whether the result is "raw" or should be stored in data_base64?
        public override byte[] EncodeBinaryModeEventData(CloudEvent cloudEvent)
        {
            if (cloudEvent.Data is null)
            {
                return Array.Empty<byte>();
            }
            ContentType contentType = new ContentType(cloudEvent.DataContentType ?? JsonMediaType);
            if (contentType.MediaType == JsonMediaType)
            {
                string json = JsonConvert.SerializeObject(cloudEvent.Data);
                return contentType.GetEncoding().GetBytes(json);
            }
            if (contentType.MediaType.StartsWith("text/") && cloudEvent.Data is string text)
            {
                return contentType.GetEncoding().GetBytes(text);
            }
            if (cloudEvent.Data is byte[] bytes)
            {
                return bytes;
            }
            throw new ArgumentException($"{nameof(JsonEventFormatter)} cannot serialize data of type {cloudEvent.Data.GetType()} with content type '{cloudEvent.DataContentType}'");
        }

        public override void DecodeBinaryModeEventData(byte[] value, CloudEvent cloudEvent)
        {
            ContentType contentType = new ContentType(cloudEvent.DataContentType ?? JsonMediaType);

            Encoding encoding = contentType.GetEncoding();

            if (contentType.MediaType == JsonMediaType)
            {
                var jsonReader = CreateJsonReader(new MemoryStream(value), encoding);
                cloudEvent.Data = JToken.Load(jsonReader);
            }
            else if (contentType.MediaType.StartsWith("text/") == true)
            {
                cloudEvent.Data = encoding.GetString(value);
            }
            else
            {
                cloudEvent.Data = value;
            }
        }

        private static JsonReader CreateJsonReader(Stream stream, Encoding encoding) =>
            new JsonTextReader(new StreamReader(stream, encoding ?? Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true))
            {
                DateParseHandling = DateParseHandling.None
            };
    }
}