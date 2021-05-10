// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.SystemTextJson
{
    /// <summary>
    /// Formatter that implements the JSON Event Format, using System.Text.Json for JSON serialization and deserialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When encoding CloudEvent data, the behavior of this implementation depends on the data
    /// content type of the CloudEvent and the type of the <see cref="CloudEvent.Data"/> property value,
    /// following the rules below. Derived classes can specialize this behavior by overriding
    /// <see cref="EncodeStructuredModeData(CloudEvent, Utf8JsonWriter)"/> or <see cref="EncodeBinaryModeEventData(CloudEvent)"/>.
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// If the data value is null, the content is empty for a binary mode message, and neither the "data"
    /// nor "data_base64" property is populated in a structured mode message.
    /// </description></item>
    /// <item><description>
    /// If the data content type is absent or has a media type of "application/json", the data is encoded as JSON,
    /// using the <see cref="JsonSerializerOptions"/> passed into the constructor, or the default options.
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
    /// When decoding CloudEvent data, this implementation uses the following rules:
    /// </para>
    /// <para>
    /// In a structured mode message, any data is either binary data within the "data_base64" property value,
    /// or is a JSON token as the "data" property value. Binary data is represented as a byte array.
    /// A JSON token is decoded as a string if is just a string value and the data content type is specified
    /// and has a media type beginning with "text/". A JSON token representing the null value always
    /// leads to a null data result. In any other situation, the JSON token is preserved as a <see cref="JsonElement"/>
    /// that can be used for further deserialization (e.g. to a specific CLR type). This behavior can be modified
    /// by overriding <see cref="DecodeStructuredModeDataBase64Property(JsonElement, CloudEvent)"/> and
    /// <see cref="DecodeStructuredModeDataProperty(JsonElement, CloudEvent)"/>.
    /// </para>
    /// <para>
    /// In a binary mode message, the data is parsed based on the content type of the message. When the content
    /// type is absent or has a media type of "application/json", the data is parsed as JSON, with the result as
    /// a <see cref="JsonElement"/> (or null if the data is empty). When the content type has a media type beginning
    /// with "text/", the data is parsed as a string. In all other cases, the data is left as a byte array.
    /// This behavior can be specialized by overriding <see cref="DecodeBinaryModeEventData(byte[], CloudEvent)"/>.
    /// </para>
    /// </remarks>
    public class JsonEventFormatter : CloudEventFormatter
    {
        private const string JsonMediaType = "application/json";
        private const string MediaTypeSuffix = "+json";

        private static readonly string StructuredMediaType = CloudEvent.MediaType + MediaTypeSuffix;
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
        /// The options to use when serializing objects to JSON.
        /// </summary>
        protected JsonSerializerOptions SerializerOptions { get; }

        /// <summary>
        /// The options to use when parsing JSON documents.
        /// </summary>
        protected JsonDocumentOptions DocumentOptions { get; }

        /// <summary>
        /// Creates a JsonEventFormatter that uses a default <see cref="JsonSerializer"/>.
        /// </summary>
        public JsonEventFormatter() : this(null, default)
        {
        }

        /// <summary>
        /// Creates a JsonEventFormatter that uses the specified <see cref="JsonSerializer"/>
        /// to serialize objects as JSON.
        /// </summary>
        /// <param name="serializerOptions">The options to use when serializing objects to JSON. May be null.</param>
        /// <param name="documentOptions">The options to use when parsing JSON documents.</param>
        public JsonEventFormatter(JsonSerializerOptions serializerOptions, JsonDocumentOptions documentOptions)
        {
            SerializerOptions = serializerOptions;
            DocumentOptions = documentOptions;
        }

        /// <inheritdoc />
        public override async Task<CloudEvent> DecodeStructuredModeMessageAsync(Stream body, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes) =>
            await DecodeStructuredModeMessageImpl(body, contentType, extensionAttributes, true).ConfigureAwait(false);

        /// <inheritdoc />
        public override CloudEvent DecodeStructuredModeMessage(Stream body, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes) =>
            DecodeStructuredModeMessageImpl(body, contentType, extensionAttributes, false).GetAwaiter().GetResult();

        public override CloudEvent DecodeStructuredModeMessage(byte[] body, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes) =>
            DecodeStructuredModeMessageImpl(new MemoryStream(body), contentType, extensionAttributes, false).GetAwaiter().GetResult();

        private async Task<CloudEvent> DecodeStructuredModeMessageImpl(Stream data, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes, bool async)
        {
            Validation.CheckNotNull(data, nameof(data));
            JsonDocument document = await ReadDocumentAsync(data, contentType, async).ConfigureAwait(false);
            using (document)
            {
                return DecodeJsonElement(document.RootElement, extensionAttributes, nameof(data));
            }
        }

        /// <inheritdoc />
        public override Task<IReadOnlyList<CloudEvent>> DecodeBatchModeMessageAsync(Stream body, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes) =>
            DecodeBatchModeMessageImpl(body, contentType, extensionAttributes, true);

        /// <inheritdoc />
        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(Stream body, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes) =>
            DecodeBatchModeMessageImpl(body, contentType, extensionAttributes, false).GetAwaiter().GetResult();

        /// <inheritdoc />
        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(byte[] body, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes) =>
            DecodeBatchModeMessageImpl(new MemoryStream(body), contentType, extensionAttributes, false).GetAwaiter().GetResult();

        private async Task<IReadOnlyList<CloudEvent>> DecodeBatchModeMessageImpl(Stream data, ContentType contentType, IEnumerable<CloudEventAttribute> extensionAttributes, bool async)
        {
            Validation.CheckNotNull(data, nameof(data));
            var document = await ReadDocumentAsync(data, contentType, async).ConfigureAwait(false);
            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Array)
                {
                    throw new ArgumentException($"Cannot decode JSON element of kind '{root.ValueKind}' as batch CloudEvent");
                }
                // Avoiding LINQ to avoid extraneous allocations etc.
                List<CloudEvent> events = new List<CloudEvent>(root.GetArrayLength());
                foreach (var element in root.EnumerateArray())
                {
                    events.Add(DecodeJsonElement(element, extensionAttributes, nameof(data)));
                }
                return events;
            }
        }

        private async Task<JsonDocument> ReadDocumentAsync(Stream data, ContentType contentType, bool async)
        {
            var encoding = MimeUtilities.GetEncoding(contentType);
            if (encoding is UTF8Encoding)
            {
                return async
                    ? await JsonDocument.ParseAsync(data, DocumentOptions).ConfigureAwait(false)
                    : JsonDocument.Parse(data, DocumentOptions);
            }
            else
            {
                using var reader = new StreamReader(data, encoding);
                var json = async
                    ? await reader.ReadToEndAsync().ConfigureAwait(false)
                    : reader.ReadToEnd();
                return JsonDocument.Parse(json, DocumentOptions);
            }
        }

        private CloudEvent DecodeJsonElement(JsonElement element, IEnumerable<CloudEventAttribute> extensionAttributes, string paramName)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException($"Cannot decode JSON element of kind '{element.ValueKind}' as CloudEvent");
            }

            if (!element.TryGetProperty(CloudEventsSpecVersion.SpecVersionAttribute.Name, out var specVersionProperty)
                || specVersionProperty.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException($"Structured mode content does not represent a CloudEvent");
            }
            var specVersion = CloudEventsSpecVersion.FromVersionId(specVersionProperty.GetString())
                ?? throw new ArgumentException($"Unsupported CloudEvents spec version '{specVersionProperty.GetString()}'");

            var cloudEvent = new CloudEvent(specVersion, extensionAttributes);
            PopulateAttributesFromStructuredEvent(cloudEvent, element);
            PopulateDataFromStructuredEvent(cloudEvent, element);
            return Validation.CheckCloudEventArgument(cloudEvent, paramName);
        }

        private void PopulateAttributesFromStructuredEvent(CloudEvent cloudEvent, JsonElement element)
        {
            foreach (var jsonProperty in element.EnumerateObject())
            {
                var name = jsonProperty.Name;
                var value = jsonProperty.Value;

                // Skip the spec version attribute, which we've already taken account of.
                // Data is handled later, when everything else (importantly, the data content type)
                // has been populated.
                if (name == CloudEventsSpecVersion.SpecVersionAttribute.Name ||
                    name == DataBase64PropertyName ||
                    name == DataPropertyName)
                {
                    continue;
                }

                // For non-extension attributes, validate that the token type is as expected.
                // We're more forgiving for extension attributes: if an integer-typed extension attribute
                // has a value of "10" (i.e. as a string), that's fine. (If it has a value of "garbage",
                // that will throw in SetAttributeFromString.)
                ValidateTokenTypeForAttribute(cloudEvent.GetAttribute(name), value.ValueKind);

                // TODO: This currently performs more conversions than it really should, in the cause of simplicity.
                // We basically need a matrix of "attribute type vs token type" but that's rather complicated.

                string attributeValue = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.True => CloudEventAttributeType.Boolean.Format(true),
                    JsonValueKind.False => CloudEventAttributeType.Boolean.Format(false),
                    JsonValueKind.Null => null,
                    // Note: this will fail if the value isn't an integer, or is out of range for Int32.
                    JsonValueKind.Number => CloudEventAttributeType.Integer.Format(value.GetInt32()),
                    _ => throw new ArgumentException($"Invalid token type '{value.ValueKind}' for CloudEvent attribute")
                };
                if (attributeValue is null)
                {
                    continue;
                }
                // Note: we *could* infer an extension type of integer and Boolean, but not other extension types.
                // (We don't want to assume that everything that looks like a timestamp is a timestamp, etc.)
                // Stick to strings for consistency.
                cloudEvent.SetAttributeFromString(name, attributeValue);
            }
        }

        private void ValidateTokenTypeForAttribute(CloudEventAttribute attribute, JsonValueKind valueKind)
        {
            // We can't validate unknown attributes, don't check for extension attributes,
            // and null values will be ignored anyway.
            if (attribute is null || attribute.IsExtension || valueKind == JsonValueKind.Null)
            {
                return;
            }

            // This is deliberately written so that if a new attribute type is added without this being updated, we "fail valid".
            // (That should only happen in major versions anyway, but it's worth being somewhat forgiving here.)
            var valid = CloudEventAttributeTypes.GetOrdinal(attribute.Type) switch
            {
                CloudEventAttributeTypeOrdinal.Binary => valueKind == JsonValueKind.String,
                CloudEventAttributeTypeOrdinal.Boolean => valueKind == JsonValueKind.True || valueKind == JsonValueKind.False,
                CloudEventAttributeTypeOrdinal.Integer => valueKind == JsonValueKind.Number,
                CloudEventAttributeTypeOrdinal.String => valueKind == JsonValueKind.String,
                CloudEventAttributeTypeOrdinal.Timestamp => valueKind == JsonValueKind.String,
                CloudEventAttributeTypeOrdinal.Uri => valueKind == JsonValueKind.String,
                CloudEventAttributeTypeOrdinal.UriReference => valueKind == JsonValueKind.String,
                _ => true
            };
            if (!valid)
            {
                throw new ArgumentException($"Invalid token type '{valueKind}' for CloudEvent attribute '{attribute.Name}' with type '{attribute.Type}'");
            }
        }

        private void PopulateDataFromStructuredEvent(CloudEvent cloudEvent, JsonElement element)
        {
            // Fetch data and data_base64 tokens, and treat null as missing.
            element.TryGetProperty(DataPropertyName, out var dataElement);
            element.TryGetProperty(DataBase64PropertyName, out var dataBase64Element);

            bool dataPresent = dataElement.ValueKind != JsonValueKind.Null && dataElement.ValueKind != JsonValueKind.Undefined;
            bool dataBase64Present = dataBase64Element.ValueKind != JsonValueKind.Null && dataBase64Element.ValueKind != JsonValueKind.Undefined;

            // If we don't have any data, we're done.
            if (!dataPresent && !dataBase64Present)
            {
                return;
            }
            // We can't handle both properties being set.
            if (dataPresent && dataBase64Present)
            {
                throw new ArgumentException($"Structured mode content cannot contain both '{DataPropertyName}' and '{DataBase64PropertyName}' properties.");
            }
            // Okay, we have exactly one non-null data/data_base64 property.
            // Decode it, potentially using overridden methods for specialization.
            if (dataBase64Present)
            {
                DecodeStructuredModeDataBase64Property(dataBase64Element, cloudEvent);
            }
            else
            {
                DecodeStructuredModeDataProperty(dataElement, cloudEvent);
            }
        }

        /// <summary>
        /// Decodes the "data_base64" property provided within a structured-mode message,
        /// populating the <see cref="CloudEvent.Data"/> property accordingly.
        /// </summary>
        /// <param name="cloudEvent"></param>
        /// <remarks>
        /// <para>
        /// This implementation converts JSON string tokens to byte arrays, and fails for any other token type.
        /// </para>
        /// <para>
        /// Override this method to provide more specialized conversions.
        /// </para>
        /// </remarks>
        /// <param name="dataBase64Element">The "data_base64" property value within the structured-mode message. Will not be null, and will
        /// not have a null token type.</param>
        /// <param name="cloudEvent">The event being decoded. This should not be modified except to
        /// populate the <see cref="CloudEvent.Data"/> property, but may be used to provide extra
        /// information such as the data content type. Will not be null.</param>
        /// <returns>The data to populate in the <see cref="CloudEvent.Data"/> property.</returns>
        protected virtual void DecodeStructuredModeDataBase64Property(JsonElement dataBase64Element, CloudEvent cloudEvent)
        {
            if (dataBase64Element.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException($"Structured mode property '{DataBase64PropertyName}' must be a string, when present.");
            }
            cloudEvent.Data = dataBase64Element.GetBytesFromBase64();
        }

        /// <summary>
        /// Decodes the "data" property provided within a structured-mode message,
        /// populating the <see cref="CloudEvent.Data"/> property accordingly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This implementation converts JSON string tokens to strings when the content type suggests
        /// that's appropriate, but otherwise returns the token directly.
        /// </para>
        /// <para>
        /// Override this method to provide more specialized conversions.
        /// </para>
        /// </remarks>
        /// <param name="dataElement">The "data" property value within the structured-mode message. Will not be null, and will
        /// not have a null token type.</param>
        /// <param name="cloudEvent">The event being decoded. This should not be modified except to
        /// populate the <see cref="CloudEvent.Data"/> property, but may be used to provide extra
        /// information such as the data content type. Will not be null.</param>
        /// <returns>The data to populate in the <see cref="CloudEvent.Data"/> property.</returns>
        protected virtual void DecodeStructuredModeDataProperty(JsonElement dataElement, CloudEvent cloudEvent) =>
            cloudEvent.Data = dataElement.ValueKind == JsonValueKind.String && cloudEvent.DataContentType?.StartsWith("text/") == true
                ? dataElement.GetString()
                : (object) dataElement.Clone(); // Deliberately cast to object to provide the conditional operator expression type.

        /// <inheritdoc />
        public override byte[] EncodeBatchModeMessage(IEnumerable<CloudEvent> cloudEvents, out ContentType contentType)
        {
            Validation.CheckNotNull(cloudEvents, nameof(cloudEvents));

            contentType = new ContentType(BatchMediaType)
            {
                CharSet = Encoding.UTF8.WebName
            };

            var stream = new MemoryStream();
            var writer = new Utf8JsonWriter(stream);
            writer.WriteStartArray();
            foreach (var cloudEvent in cloudEvents)
            {
                WriteCloudEventForBatchOrStructuredMode(writer, cloudEvent);
            }
            writer.WriteEndArray();
            writer.Flush();
            return stream.ToArray();
        }

        /// <inheritdoc />
        public override byte[] EncodeStructuredModeMessage(CloudEvent cloudEvent, out ContentType contentType)
        {
            contentType = new ContentType(StructuredMediaType)
            {
                CharSet = Encoding.UTF8.WebName
            };

            var stream = new MemoryStream();
            var writer = new Utf8JsonWriter(stream);
            WriteCloudEventForBatchOrStructuredMode(writer, cloudEvent);
            writer.Flush();
            return stream.ToArray();
        }

        private void WriteCloudEventForBatchOrStructuredMode(Utf8JsonWriter writer, CloudEvent cloudEvent)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));

            writer.WriteStartObject();
            writer.WritePropertyName(CloudEventsSpecVersion.SpecVersionAttribute.Name);
            writer.WriteStringValue(cloudEvent.SpecVersion.VersionId);
            var attributes = cloudEvent.GetPopulatedAttributes();
            foreach (var keyValuePair in attributes)
            {
                var attribute = keyValuePair.Key;
                var value = keyValuePair.Value;
                writer.WritePropertyName(attribute.Name);
                switch (CloudEventAttributeTypes.GetOrdinal(attribute.Type))
                {
                    case CloudEventAttributeTypeOrdinal.Integer:
                        writer.WriteNumberValue((int) value);
                        break;
                    case CloudEventAttributeTypeOrdinal.Boolean:
                        writer.WriteBooleanValue((bool) value);
                        break;
                    default:
                        writer.WriteStringValue(attribute.Type.Format(value));
                        break;

                }
            }

            if (cloudEvent.Data is object)
            {
                EncodeStructuredModeData(cloudEvent, writer);
            }
            writer.WriteEndObject();
        }

        /// <summary>
        /// Encodes structured mode data within a CloudEvent, writing it to the specified <see cref="JsonWriter"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This implementation follows the rules listed in the class remarks. Override this method
        /// to provide more specialized behavior, writing only <see cref="DataPropertyName"/> or
        /// <see cref="DataBase64PropertyName"/> properties.
        /// </para>
        /// </remarks>
        /// <param name="cloudEvent">The CloudEvent being encoded, which will have a non-null value for
        /// its <see cref="CloudEvent.Data"/> property.
        /// <paramref name="writer"/>The writer to serialize the data to. Will not be null.</param>
        /// <see cref="CloudEvent.Data"/>.</param>
        protected virtual void EncodeStructuredModeData(CloudEvent cloudEvent, Utf8JsonWriter writer)
        {
            ContentType dataContentType = new ContentType(cloudEvent.DataContentType ?? JsonMediaType);
            if (dataContentType.MediaType == JsonMediaType)
            {
                writer.WritePropertyName(DataPropertyName);
                JsonSerializer.Serialize(writer, cloudEvent.Data, SerializerOptions);
            }
            else if (cloudEvent.Data is string text && dataContentType.MediaType.StartsWith("text/"))
            {
                writer.WritePropertyName(DataPropertyName);
                writer.WriteStringValue(text);
            }
            else if (cloudEvent.Data is byte[] binary)
            {
                writer.WritePropertyName(DataBase64PropertyName);
                writer.WriteStringValue(Convert.ToBase64String(binary));
            }
            else
            {
                throw new ArgumentException($"{nameof(JsonEventFormatter)} cannot serialize data of type {cloudEvent.Data.GetType()} with content type '{cloudEvent.DataContentType}'");
            }
        }

        /// <inheritdoc />
        public override byte[] EncodeBinaryModeEventData(CloudEvent cloudEvent)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));

            if (cloudEvent.Data is null)
            {
                return Array.Empty<byte>();
            }
            ContentType contentType = new ContentType(cloudEvent.DataContentType ?? JsonMediaType);
            if (contentType.MediaType == JsonMediaType)
            {
                var encoding = MimeUtilities.GetEncoding(contentType);
                if (encoding is UTF8Encoding)
                {
                    return JsonSerializer.SerializeToUtf8Bytes(cloudEvent.Data, SerializerOptions);
                }
                else
                {
                    return MimeUtilities.GetEncoding(contentType).GetBytes(JsonSerializer.Serialize(cloudEvent.Data, SerializerOptions));
                }
            }
            if (contentType.MediaType.StartsWith("text/") && cloudEvent.Data is string text)
            {
                return MimeUtilities.GetEncoding(contentType).GetBytes(text);
            }
            if (cloudEvent.Data is byte[] bytes)
            {
                return bytes;
            }
            throw new ArgumentException($"{nameof(JsonEventFormatter)} cannot serialize data of type {cloudEvent.Data.GetType()} with content type '{cloudEvent.DataContentType}'");
        }

        /// <inheritdoc />
        public override void DecodeBinaryModeEventData(byte[] body, CloudEvent cloudEvent)
        {
            Validation.CheckNotNull(body, nameof(body));
            Validation.CheckNotNull(cloudEvent, nameof(cloudEvent));

            ContentType contentType = new ContentType(cloudEvent.DataContentType ?? JsonMediaType);

            Encoding encoding = MimeUtilities.GetEncoding(contentType);

            if (contentType.MediaType == JsonMediaType)
            {
                if (body.Length > 0)
                {
                    using JsonDocument document = encoding is UTF8Encoding
                        ? JsonDocument.Parse(body, DocumentOptions)
                        : JsonDocument.Parse(encoding.GetString(body), DocumentOptions);
                    // We have to clone the data so that we can dispose of the JsonDocument.
                    cloudEvent.Data = document.RootElement.Clone();
                }
                else
                {
                    cloudEvent.Data = null;
                }
            }
            else if (contentType.MediaType.StartsWith("text/") == true)
            {
                cloudEvent.Data = encoding.GetString(body);
            }
            else
            {
                cloudEvent.Data = body;
            }
        }
    }
}