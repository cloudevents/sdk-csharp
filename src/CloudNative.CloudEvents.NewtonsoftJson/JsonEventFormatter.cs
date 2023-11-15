// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
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
    /// Formatter that implements the JSON Event Format, using Newtonsoft.Json (also known as Json.NET) for JSON serialization and deserialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When encoding CloudEvent data, the behavior of this implementation depends on the data
    /// content type of the CloudEvent and the type of the <see cref="CloudEvent.Data"/> property value,
    /// following the rules below. Derived classes can specialize this behavior by overriding
    /// <see cref="EncodeStructuredModeData(CloudEvent, JsonWriter)"/> or <see cref="EncodeBinaryModeEventData(CloudEvent)"/>.
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// If the data value is null, the content is empty for a binary mode message, and neither the "data"
    /// nor "data_base64" property is populated in a structured mode message.
    /// </description></item>
    /// <item><description>
    /// If the data value is a byte array, it is serialized either directly as binary data
    /// (for binary mode messages) or as base64 data (for structured mode messages).
    /// </description></item>
    /// <item><description>
    /// Otherwise, if the data content type is absent or has a media type indicating JSON, the data is encoded as JSON.
    /// If the data is already a <see cref="JToken"/>, that is serialized directly as JSON. Otherwise, the data
    /// is converted using the <see cref="JsonSerializer"/> passed into the constructor, or a
    /// default serializer.
    /// </description></item>
    /// <item><description>
    /// Otherwise, if the data content type has a media type beginning with "text/" and the data value is a string,
    /// the data is serialized as a string.
    /// </description></item>
    /// <item><description>
    /// Otherwise, the encoding operation fails.
    /// </description></item>
    /// </list>
    /// <para>
    /// When decoding structured mode CloudEvent data, this implementation uses the following rules,
    /// which can be modified by overriding <see cref="DecodeStructuredModeDataBase64Property(JToken, CloudEvent)"/>
    /// and <see cref="DecodeStructuredModeDataProperty(JToken, CloudEvent)"/>.
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// If the "data_base64" property is present, its value is decoded as a byte array.
    /// </description></item>
    /// <item><description>
    /// If the "data" property is present (and non-null) and the content type is absent or indicates a JSON media type,
    /// the JSON token present in the property is preserved as a <see cref="JToken"/> that can be used for further
    /// deserialization (e.g. to a specific CLR type).
    /// </description></item>
    /// <item><description>
    /// If the "data" property has a string value and a non-JSON content type has been specified, the data is
    /// deserialized as a string.
    /// </description></item>
    /// <item><description>
    /// If the "data" property has a non-null, non-string value and a non-JSON content type has been specified,
    /// the deserialization operation fails.
    /// </description></item>
    /// </list>
    /// <para>
    /// In a binary mode message, the data is parsed based on the content type of the message. When the content
    /// type is absent or has a JSON media type, the data is parsed as JSON, with the result as
    /// a <see cref="JToken"/> (or null if the data is empty). When the content type has a media type beginning
    /// with "text/", the data is parsed as a string. In all other cases, the data is left as a byte array.
    /// This behavior can be specialized by overriding <see cref="DecodeBinaryModeEventData(ReadOnlyMemory{byte}, CloudEvent)"/>.
    /// </para>
    /// </remarks>
    public class JsonEventFormatter : CloudEventFormatter
    {
        private static readonly IReadOnlyDictionary<CloudEventAttributeType, JTokenType> expectedTokenTypesForReservedAttributes =
            new Dictionary<CloudEventAttributeType, JTokenType>
            {
                { CloudEventAttributeType.Binary, JTokenType.String },
                { CloudEventAttributeType.Boolean, JTokenType.Boolean },
                { CloudEventAttributeType.Integer, JTokenType.Integer },
                { CloudEventAttributeType.String, JTokenType.String },
                { CloudEventAttributeType.Timestamp, JTokenType.String },
                { CloudEventAttributeType.Uri, JTokenType.String },
                { CloudEventAttributeType.UriReference, JTokenType.String }
            };

        private const string JsonMediaType = "application/json";
        private const string MediaTypeSuffix = "+json";

        private static readonly string StructuredMediaType = MimeUtilities.MediaType + MediaTypeSuffix;
        private static readonly string BatchMediaType = MimeUtilities.BatchMediaType + MediaTypeSuffix;

        /// <summary>
        /// The property name to use for base64-encoded binary data in a structured-mode message.
        /// </summary>
        protected const string DataBase64PropertyName = "data_base64";

        /// <summary>
        /// The property name to use for general data in a structured-mode message.
        /// </summary>
        protected const string DataPropertyName = "data";

        /// <summary>
        /// The serializer to use when performing JSON conversions.
        /// </summary>
        protected JsonSerializer Serializer { get; }

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
            Serializer = Validation.CheckNotNull(serializer, nameof(serializer));
        }

        /// <inheritdoc />
        public override async Task<CloudEvent> DecodeStructuredModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(body, nameof(body));

            var jsonReader = CreateJsonReader(body, MimeUtilities.GetEncoding(contentType));
            var jObject = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            return DecodeJObject(jObject, extensionAttributes, nameof(body));
        }

        /// <inheritdoc />
        public override CloudEvent DecodeStructuredModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(body, nameof(body));

            var jsonReader = CreateJsonReader(body, MimeUtilities.GetEncoding(contentType));
            var jObject = JObject.Load(jsonReader);
            return DecodeJObject(jObject, extensionAttributes, nameof(body));
        }

        /// <inheritdoc />
        public override CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            DecodeStructuredModeMessage(BinaryDataUtilities.AsStream(body), contentType, extensionAttributes);

        /// <inheritdoc />
        public override async Task<IReadOnlyList<CloudEvent>> DecodeBatchModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(body, nameof(body));

            var jsonReader = CreateJsonReader(body, MimeUtilities.GetEncoding(contentType));
            var array = await JArray.LoadAsync(jsonReader).ConfigureAwait(false);
            return DecodeJArray(array, extensionAttributes, nameof(body));
        }

        /// <inheritdoc />
        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(body, nameof(body));

            var jsonReader = CreateJsonReader(body, MimeUtilities.GetEncoding(contentType));
            var array = JArray.Load(jsonReader);
            return DecodeJArray(array, extensionAttributes, nameof(body));
        }

        /// <inheritdoc />
        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            DecodeBatchModeMessage(BinaryDataUtilities.AsStream(body), contentType, extensionAttributes);

        /// <summary>
        /// Converts the given <see cref="JObject"/> into a <see cref="CloudEvent"/>.
        /// </summary>
        /// <param name="jObject">The JSON representation of a CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The SDK representation of the CloudEvent.</returns>
        public CloudEvent ConvertFromJObject(JObject jObject, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            DecodeJObject(Validation.CheckNotNull(jObject, nameof(jObject)), extensionAttributes, nameof(jObject));

        private IReadOnlyList<CloudEvent> DecodeJArray(JArray jArray, IEnumerable<CloudEventAttribute>? extensionAttributes, string paramName)
        {
            List<CloudEvent> events = new List<CloudEvent>(jArray.Count);
            foreach (var token in jArray)
            {
                if (token is JObject obj)
                {
                    events.Add(DecodeJObject(obj, extensionAttributes, paramName));
                }
                else
                {
                    throw new ArgumentException($"Invalid array element index {events.Count} within batch; expected an object, but token type was '{token?.Type}'", paramName);
                }
            }
            return events;
        }

        private CloudEvent DecodeJObject(JObject jObject, IEnumerable<CloudEventAttribute>? extensionAttributes, string paramName)
        {
            if (!jObject.TryGetValue(CloudEventsSpecVersion.SpecVersionAttribute.Name, out var specVersionToken)
                || specVersionToken.Type != JTokenType.String)
            {
                throw new ArgumentException($"Structured mode content does not represent a CloudEvent");
            }
            var specVersion = CloudEventsSpecVersion.FromVersionId((string?) specVersionToken)
                ?? throw new ArgumentException($"Unsupported CloudEvents spec version '{(string?) specVersionToken}'");

            var cloudEvent = new CloudEvent(specVersion, extensionAttributes);
            PopulateAttributesFromStructuredEvent(cloudEvent, jObject);
            PopulateDataFromStructuredEvent(cloudEvent, jObject);
            return Validation.CheckCloudEventArgument(cloudEvent, paramName);
        }

        private void PopulateAttributesFromStructuredEvent(CloudEvent cloudEvent, JObject jObject)
        {
            foreach (var keyValuePair in jObject)
            {
                var key = keyValuePair.Key;
                JToken value = keyValuePair.Value!;

                // Skip the spec version attribute, which we've already taken account of.
                // Data is handled later, when everything else (importantly, the data content type)
                // has been populated.
                if (key == CloudEventsSpecVersion.SpecVersionAttribute.Name ||
                    key == DataBase64PropertyName ||
                    key == DataPropertyName)
                {
                    continue;
                }

                // For non-extension attributes, validate that the token type is as expected.
                // We're more forgiving for extension attributes: if an integer-typed extension attribute
                // has a value of "10" (i.e. as a string), that's fine. (If it has a value of "garbage",
                // that will throw in SetAttributeFromString.)
                ValidateTokenTypeForAttribute(cloudEvent.GetAttribute(key), value.Type);

                // TODO: This currently performs more conversions than it really should, in the cause of simplicity.
                // We basically need a matrix of "attribute type vs token type" but that's rather complicated.

                string? attributeValue = value.Type switch
                {
                    JTokenType.String => (string?) value,
                    JTokenType.Boolean => CloudEventAttributeType.Boolean.Format((bool) value),
                    JTokenType.Null => null,
                    JTokenType.Integer => CloudEventAttributeType.Integer.Format((int) value),
                    _ => throw new ArgumentException($"Invalid token type '{value.Type}' for CloudEvent attribute")
                };
                if (attributeValue is null)
                {
                    continue;
                }
                // Note: we *could* infer an extension type of integer and Boolean, but not other extension types.
                // (We don't want to assume that everything that looks like a timestamp is a timestamp, etc.)
                // Stick to strings for consistency.
                cloudEvent.SetAttributeFromString(key, attributeValue);
            }
        }

        private void ValidateTokenTypeForAttribute(CloudEventAttribute? attribute, JTokenType tokenType)
        {
            // We can't validate unknown attributes, don't check for extension attributes,
            // and null values will be ignored anyway.
            if (attribute is null || attribute.IsExtension || tokenType == JTokenType.Null)
            {
                return;
            }
            // We use TryGetValue so that if a new attribute type is added without this being updated, we "fail valid".
            // (That should only happen in major versions anyway, but it's worth being somewhat forgiving here.)
            if (expectedTokenTypesForReservedAttributes.TryGetValue(attribute.Type, out JTokenType expectedTokenType) &&
                tokenType != expectedTokenType)
            {
                throw new ArgumentException($"Invalid token type '{tokenType}' for CloudEvent attribute '{attribute.Name}' with type '{attribute.Type}'");
            }
        }

        private void PopulateDataFromStructuredEvent(CloudEvent cloudEvent, JObject jObject)
        {
            // Fetch data and data_base64 tokens, and treat null as missing.
            jObject.TryGetValue(DataPropertyName, out var dataToken);
            if (dataToken is JToken { Type: JTokenType.Null })
            {
                dataToken = null;
            }
            jObject.TryGetValue(DataBase64PropertyName, out var dataBase64Token);
            if (dataBase64Token is JToken { Type: JTokenType.Null })
            {
                dataBase64Token = null;
            }

            // If we don't have any data, we're done.
            if (dataToken is null && dataBase64Token is null)
            {
                return;
            }
            // We can't handle both properties being set.
            if (dataToken is object && dataBase64Token is object)
            {
                throw new ArgumentException($"Structured mode content cannot contain both '{DataPropertyName}' and '{DataBase64PropertyName}' properties.");
            }
            // Okay, we have exactly one non-null data/data_base64 property.
            // Decode it, potentially using overridden methods for specialization.
            if (dataBase64Token is object)
            {
                DecodeStructuredModeDataBase64Property(dataBase64Token, cloudEvent);
            }
            else
            {
                // If no content type has been specified, default to application/json
                cloudEvent.DataContentType ??= JsonMediaType;

                // We know that dataToken must be non-null here, due to the above conditions.
                DecodeStructuredModeDataProperty(dataToken!, cloudEvent);
            }
        }

        /// <summary>
        /// Decodes the "data_base64" property provided within a structured-mode message,
        /// populating the <see cref="CloudEvent.Data"/> property accordingly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This implementation converts JSON string tokens to byte arrays, and fails for any other token type.
        /// </para>
        /// <para>
        /// Override this method to provide more specialized conversions.
        /// </para>
        /// </remarks>
        /// <param name="dataBase64Token">The "data_base64" property value within the structured-mode message. Will not be null, and will
        /// not have a null token type.</param>
        /// <param name="cloudEvent">The event being decoded. This should not be modified except to
        /// populate the <see cref="CloudEvent.Data"/> property, but may be used to provide extra
        /// information such as the data content type. Will not be null.</param>
        /// <returns>The data to populate in the <see cref="CloudEvent.Data"/> property.</returns>
        protected virtual void DecodeStructuredModeDataBase64Property(JToken dataBase64Token, CloudEvent cloudEvent)
        {
            if (dataBase64Token.Type != JTokenType.String)
            {
                throw new ArgumentException($"Structured mode property '{DataBase64PropertyName}' must be a string, when present.");
            }
            cloudEvent.Data = Convert.FromBase64String((string) dataBase64Token!);
        }

        /// <summary>
        /// Decodes the "data" property provided within a structured-mode message,
        /// populating the <see cref="CloudEvent.Data"/> property accordingly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This implementation will populate the Data property with the verbatim <see cref="JToken"/> if
        /// the content type is deemed to be JSON according to <see cref="IsJsonMediaType(string)"/>. Otherwise,
        /// it validates that the token is a string, and the Data property is populated with that string.
        /// </para>
        /// <para>
        /// Override this method to provide more specialized conversions.
        /// </para>
        /// </remarks>
        /// <param name="dataToken">The "data" property value within the structured-mode message. Will not be null, and will
        /// not have a null token type.</param>
        /// <param name="cloudEvent">The event being decoded. This should not be modified except to
        /// populate the <see cref="CloudEvent.Data"/> property, but may be used to provide extra
        /// information such as the data content type. Will not be null, and the <see cref="CloudEvent.DataContentType"/>
        /// property will be non-null.</param>
        /// <returns>The data to populate in the <see cref="CloudEvent.Data"/> property.</returns>
        protected virtual void DecodeStructuredModeDataProperty(JToken dataToken, CloudEvent cloudEvent)
        {
            if (IsJsonMediaType(new ContentType(cloudEvent.DataContentType!).MediaType))
            {
                cloudEvent.Data = dataToken;
            }
            else
            {
                if (dataToken.Type != JTokenType.String)
                {
                    throw new ArgumentException("CloudEvents with a non-JSON datacontenttype can only have string data values.");
                }
                cloudEvent.Data = (string?) dataToken;
            }
        }

        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeStructuredModeMessage(CloudEvent cloudEvent, out ContentType contentType)
        {
            // The cloudEvent parameter will be validated in WriteCloudEventForBatchOrStructuredMode

            contentType = new ContentType(StructuredMediaType)
            {
                CharSet = Encoding.UTF8.WebName
            };

            var stream = new MemoryStream();
            var writer = CreateJsonTextWriter(stream);
            WriteCloudEventForBatchOrStructuredMode(writer, cloudEvent);
            writer.Flush();
            return stream.ToArray();
        }

        /// <summary>
        /// Converts the given <see cref="CloudEvent"/> to a <see cref="JObject"/> containing the structured mode JSON format representation
        /// of the event.
        /// </summary>
        /// <param name="cloudEvent">The event to convert. Must not be null.</param>
        /// <returns>A <see cref="JObject"/> containing the structured mode JSON format representation of the event.</returns>
        public JObject ConvertToJObject(CloudEvent cloudEvent)
        {
            var writer = new JTokenWriter();
            WriteCloudEventForBatchOrStructuredMode(writer, cloudEvent);
            return (JObject) writer.Token!;
        }

        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeBatchModeMessage(IEnumerable<CloudEvent> cloudEvents, out ContentType contentType)
        {
            Validation.CheckNotNull(cloudEvents, nameof(cloudEvents));

            contentType = new ContentType(BatchMediaType)
            {
                CharSet = Encoding.UTF8.WebName
            };

            var stream = new MemoryStream();
            var writer = CreateJsonTextWriter(stream);
            writer.WriteStartArray();
            foreach (var cloudEvent in cloudEvents)
            {
                WriteCloudEventForBatchOrStructuredMode(writer, cloudEvent);
            }
            writer.WriteEndArray();
            writer.Flush();
            return stream.ToArray();
        }

        private JsonTextWriter CreateJsonTextWriter(Stream stream) =>
            // TODO: Allow settings to be specified separately?
            // JsonSerializer doesn't allow us to set the indentation or indentation character, for example.
            new JsonTextWriter(new StreamWriter(stream))
            {
                Formatting = Serializer.Formatting,
                DateFormatHandling = Serializer.DateFormatHandling,
                DateFormatString = Serializer.DateFormatString,
                DateTimeZoneHandling = Serializer.DateTimeZoneHandling,
                FloatFormatHandling = Serializer.FloatFormatHandling,
                Culture = Serializer.Culture,
                StringEscapeHandling = Serializer.StringEscapeHandling,
            };

        private void WriteCloudEventForBatchOrStructuredMode(JsonWriter writer, CloudEvent cloudEvent)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));

            writer.WriteStartObject();
            writer.WritePropertyName(CloudEventsSpecVersion.SpecVersionAttribute.Name);
            writer.WriteValue(cloudEvent.SpecVersion.VersionId);
            var attributes = cloudEvent.GetPopulatedAttributes();
            foreach (var keyValuePair in attributes)
            {
                var attribute = keyValuePair.Key;
                var value = keyValuePair.Value;
                writer.WritePropertyName(attribute.Name);
                switch (CloudEventAttributeTypes.GetOrdinal(attribute.Type))
                {
                    case CloudEventAttributeTypeOrdinal.Integer:
                        writer.WriteValue((int) value);
                        break;
                    case CloudEventAttributeTypeOrdinal.Boolean:
                        writer.WriteValue((bool) value);
                        break;
                    default:
                        writer.WriteValue(attribute.Type.Format(value));
                        break;
                }
            }

            if (cloudEvent.Data is object)
            {
                if (cloudEvent.DataContentType is null && GetOrInferDataContentType(cloudEvent) is string inferredDataContentType)
                {
                    cloudEvent.SpecVersion.DataContentTypeAttribute.Validate(inferredDataContentType);
                    writer.WritePropertyName(cloudEvent.SpecVersion.DataContentTypeAttribute.Name);
                    writer.WriteValue(inferredDataContentType);
                }
                EncodeStructuredModeData(cloudEvent, writer);
            }
            writer.WriteEndObject();
        }

        /// <summary>
        /// Infers the data content type of a CloudEvent based on its data. This implementation
        /// infers a data content type of "application/json" for any non-binary data, and performs
        /// no inference for binary data.
        /// </summary>
        /// <param name="data">The CloudEvent to infer the data content from. Must not be null.</param>
        /// <returns>The inferred data content type, or null if no inference is performed.</returns>
        protected override string? InferDataContentType(object data) => data is byte[] ? null : JsonMediaType;

        /// <summary>
        /// Encodes structured mode data within a CloudEvent, writing it to the specified <see cref="JsonWriter"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This implementation follows the rules listed in the class remarks. Override this method
        /// to provide more specialized behavior, usually writing only <see cref="DataPropertyName"/> or
        /// <see cref="DataBase64PropertyName"/> properties.
        /// </para>
        /// </remarks>
        /// <param name="cloudEvent">The CloudEvent being encoded, which will have a non-null value for
        /// its <see cref="CloudEvent.Data"/> property.
        /// <param name="writer"/>The writer to serialize the data to. Will not be null.</param>
        protected virtual void EncodeStructuredModeData(CloudEvent cloudEvent, JsonWriter writer)
        {
            // Binary data is encoded using the data_base64 property, regardless of content type.
            // TODO: Support other forms of binary data, e.g. ReadOnlyMemory<byte>
            if (cloudEvent.Data is byte[] binary)
            {
                writer.WritePropertyName(DataBase64PropertyName);
                writer.WriteValue(Convert.ToBase64String(binary));
            }
            else
            {
                string? dataContentTypeText = GetOrInferDataContentType(cloudEvent);
                // This would only happen in a derived class which overrides GetOrInferDataContentType further...
                // This class infers application/json for anything other than byte arrays.
                if (dataContentTypeText is null)
                {
                    throw new ArgumentException("Data content type cannot be inferred");
                }
                ContentType dataContentType = new ContentType(dataContentTypeText);
                if (IsJsonMediaType(dataContentType.MediaType))
                {
                    writer.WritePropertyName(DataPropertyName);
                    Serializer.Serialize(writer, cloudEvent.Data);
                }
                else if (cloudEvent.Data is string text && dataContentType.MediaType.StartsWith("text/"))
                {
                    writer.WritePropertyName(DataPropertyName);
                    writer.WriteValue(text);
                }
                else
                {
                    // We assume CloudEvent.Data is not null due to the way this is called.
                    throw new ArgumentException($"{nameof(JsonEventFormatter)} cannot serialize data of type {cloudEvent.Data!.GetType()} with content type '{cloudEvent.DataContentType}'");
                }
            }
        }

        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeBinaryModeEventData(CloudEvent cloudEvent)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));

            if (cloudEvent.Data is null)
            {
                return Array.Empty<byte>();
            }
            // Binary data is left alone, regardless of the content type.
            // TODO: Support other forms of binary data, e.g. ReadOnlyMemory<byte>
            if (cloudEvent.Data is byte[] bytes)
            {
                return bytes;
            }
            ContentType contentType = new ContentType(cloudEvent.DataContentType ?? JsonMediaType);
            if (IsJsonMediaType(contentType.MediaType))
            {
                // TODO: Make this more efficient. We could write to a StreamWriter with a MemoryStream,
                // but then we end up with a BOM in most cases, which I suspect we don't want.
                // An alternative is to make sure that contentType.GetEncoding() always returns an encoding
                // without a preamble (or rewrite StreamWriter...)
                var stringWriter = new StringWriter();
                Serializer.Serialize(stringWriter, cloudEvent.Data);
                return MimeUtilities.GetEncoding(contentType).GetBytes(stringWriter.ToString());
            }
            if (contentType.MediaType.StartsWith("text/") && cloudEvent.Data is string text)
            {
                return MimeUtilities.GetEncoding(contentType).GetBytes(text);
            }
            throw new ArgumentException($"{nameof(JsonEventFormatter)} cannot serialize data of type {cloudEvent.Data.GetType()} with content type '{cloudEvent.DataContentType}'");
        }

        /// <inheritdoc />
        public override void DecodeBinaryModeEventData(ReadOnlyMemory<byte> body, CloudEvent cloudEvent)
        {
            Validation.CheckNotNull(cloudEvent, nameof(cloudEvent));

            ContentType contentType = new ContentType(cloudEvent.DataContentType ?? JsonMediaType);

            Encoding encoding = MimeUtilities.GetEncoding(contentType);

            if (IsJsonMediaType(contentType.MediaType))
            {
                if (body.Length > 0)
                {
                    var jsonReader = CreateJsonReader(BinaryDataUtilities.AsStream(body), encoding);
                    cloudEvent.Data = JToken.Load(jsonReader);
                }
                else
                {
                    cloudEvent.Data = null;
                }
            }
            else if (contentType.MediaType.StartsWith("text/") == true)
            {
                cloudEvent.Data = BinaryDataUtilities.GetString(body, encoding);
            }
            else
            {
                cloudEvent.Data = body.ToArray();
            }
        }

        /// <summary>
        /// Creates a <see cref="JsonReader"/> for the given stream. This may be overridden in derived classes to
        /// customize the JSON parsing process, subject to the constraints listed in the remarks.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default implementation always creates an instance of <see cref="JsonTextReader"/>, and derived classes
        /// may assume that (calling this implementation and casting the result before modifying it).
        /// </para>
        /// <para>
        /// Implementations should ensure that <see cref="JsonReader.DateParseHandling"/> is set to <see cref="DateParseHandling.None"/>,
        /// as timestamp parsing is performed in a CloudEvent-specific way, and Json.NET's own implementation can obscure that.
        /// </para>
        /// </remarks>
        /// <param name="stream">The stream to read from. Will not be null.</param>
        /// <param name="encoding">The expected text encoding. May be null, in which case UTF-8 should be assumed.</param>
        /// <returns>A JsonReader suitable for reading the </returns>
        protected virtual JsonReader CreateJsonReader(Stream stream, Encoding? encoding) =>
            new JsonTextReader(new StreamReader(stream, encoding ?? Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true))
            {
                DateParseHandling = DateParseHandling.None
            };

        /// <summary>
        /// Determines whether the given media type should be handled as JSON.
        /// The default implementation treats anything ending with "/json" or "+json"
        /// as JSON.
        /// </summary>
        /// <param name="mediaType">The media type to check for JSON. Will not be null.</param>
        /// <returns>Whether or not <paramref name="mediaType"/> indicates JSON data.</returns>
        protected virtual bool IsJsonMediaType(string mediaType) => mediaType.EndsWith("/json") || mediaType.EndsWith("+json");
    }

    /// <summary>
    /// CloudEvent formatter implementing the JSON Event Format, but with an expectation that
    /// any CloudEvent with a data payload can be converted to <typeparamref name="T" /> using
    /// the <see cref="JsonSerializer"/> associated with the formatter. The content type is ignored.
    /// </summary>
    /// <typeparam name="T">The type of data to serialize and deserialize.</typeparam>
    public class JsonEventFormatter<T> : JsonEventFormatter
    {
        /// <summary>
        /// Creates a JsonEventFormatter that uses a default <see cref="JsonSerializer"/>.
        /// </summary>
        public JsonEventFormatter()
        {
        }

        /// <summary>
        /// Creates a JsonEventFormatter that uses the specified <see cref="JsonSerializer"/>
        /// to serialize objects as JSON and to deserialize them to <typeparamref name="T"/> values.
        /// </summary>
        public JsonEventFormatter(JsonSerializer serializer) : base(serializer)
        {
        }

        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeBinaryModeEventData(CloudEvent cloudEvent)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));

            if (cloudEvent.Data is null)
            {
                return Array.Empty<byte>();
            }
            T data = (T) cloudEvent.Data;
            // TODO: Make this more efficient. (See base class implementation for a more detailed comment.)
            var stringWriter = new StringWriter();
            Serializer.Serialize(stringWriter, data);
            return Encoding.UTF8.GetBytes(stringWriter.ToString());
        }

        /// <inheritdoc />
        public override void DecodeBinaryModeEventData(ReadOnlyMemory<byte> body, CloudEvent cloudEvent)
        {
            Validation.CheckNotNull(cloudEvent, nameof(cloudEvent));

            if (body.Length == 0)
            {
                cloudEvent.Data = null;
                return;
            }
            using var jsonReader = CreateJsonReader(BinaryDataUtilities.AsStream(body), Encoding.UTF8);
            cloudEvent.Data = Serializer.Deserialize<T>(jsonReader);
        }

        /// <inheritdoc />
        protected override void EncodeStructuredModeData(CloudEvent cloudEvent, JsonWriter writer)
        {
            T data = (T) cloudEvent.Data;
            writer.WritePropertyName(DataPropertyName);
            Serializer.Serialize(writer, data);
        }

        /// <inheritdoc />
        protected override void DecodeStructuredModeDataProperty(JToken dataToken, CloudEvent cloudEvent) =>
            cloudEvent.Data = Serializer.Deserialize<T>(new JTokenReader(dataToken));

        // TODO: Consider decoding the base64 data as a byte array, then using DecodeBinaryModeData.
        /// <inheritdoc />
        protected override void DecodeStructuredModeDataBase64Property(JToken dataBase64Token, CloudEvent cloudEvent) =>
            throw new ArgumentException($"Data unexpectedly represented using '{DataBase64PropertyName}' within structured mode CloudEvent.");
    }
}
