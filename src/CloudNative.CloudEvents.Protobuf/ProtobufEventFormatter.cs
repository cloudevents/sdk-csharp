// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using CloudNative.CloudEvents.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using static CloudNative.CloudEvents.V1.CloudEvent;
using static CloudNative.CloudEvents.V1.CloudEvent.Types;
using static CloudNative.CloudEvents.V1.CloudEvent.Types.CloudEventAttributeValue;

namespace CloudNative.CloudEvents.Protobuf
{
    // TODO: Derived type which expects to only receive protobuf message data with a particular message type,
    // so is able to unpack it. 

    /// <summary>
    /// Formatter that implements the Protobuf Event Format, using the Google.Protobuf library for serialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When encoding CloudEvents in structured mode, three kinds of data are supported, as indicated in the
    /// event format. Text is stored in the <see cref="V1.CloudEvent.TextData"/> field; binary data is stored
    /// in the <see cref="V1.CloudEvent.BinaryData"/> field; protobuf messages are stored in the
    /// <see cref="V1.CloudEvent.ProtoData"/> field. In the last case, the message is packed in an
    /// <see cref="Any"/> message, to preserve information about which message is encoded, unless the message
    /// is already an <see cref="Any"/> in which case it is stored directly. (This prevents "double-encoding"
    /// when a CloudEvent is decoded and then re-encoded.) Attempts to serialize CloudEvents with any other data type
    /// will fail. Derived classes can specialize all of this behavior by overriding
    /// <see cref="EncodeStructuredModeData(CloudEvent, V1.CloudEvent)"/>.
    /// </para>
    /// <para>
    /// When decoding CloudEvents in structured mode, text and binary data payloads are represented as strings and byte
    /// arrays respectively. Protobuf message payloads are represented using the <see cref="Any"/> wrapper, without
    /// attempting to "unpack" the message. This avoids any requirement for the underlying message type to be
    /// known by the application consuming the CloudEvent. (The data may be stored for later processing by another
    /// application with more awareness, for example.) Derived classes can specialize all of this behavior by
    /// overriding <see cref="DecodeStructuredModeData(V1.CloudEvent, CloudEvent)"/>.
    /// </para>
    /// <para>
    /// When encoding CloudEvent data in binary mode, this implementation only supports plain binary and text data.
    /// (Even text data is only supported when the <see cref="CloudEvent.DataContentType"/> begins with "text/".)
    /// While it might be expected that protobuf messages would be serialized into the binary mode data, there is
    /// no clear standard as to whether they should be directly serialized, or packed into an <see cref="Any"/>
    /// message first, and no standardized content type to use to distinguish these options. Users are encouraged
    /// to either use structured mode where possible, or explicitly encode the data as a byte array first. Derived
    /// classes can specialize this behavior by overriding <see cref="EncodeBinaryModeEventData(CloudEvent)"/>.
    /// </para>
    /// <para>
    /// When decoding CloudEvent data in binary mode, if the data content type begins with "text/" it is decoded as
    /// a string, otherwise it is left as a byte array. Derived classes can specialize this behavior by overriding
    /// <see cref="DecodeBinaryModeEventData(ReadOnlyMemory{byte}, CloudEvent)"/>.
    /// </para>
    /// </remarks>
    public class ProtobufEventFormatter : CloudEventFormatter
    {
        /// <summary>
        /// The default value for <see cref="TypeUrlPrefix"/>. This is the value used by Protobuf libraries
        /// when no prefix is specifically provided.
        /// </summary>
        public const string DefaultTypeUrlPrefix = "type.googleapis.com";

        private const string MediaTypeSuffix = "+protobuf";

        private static readonly string StructuredMediaType = MimeUtilities.MediaType + MediaTypeSuffix;
        private static readonly string BatchMediaType = MimeUtilities.BatchMediaType + MediaTypeSuffix;

        /// <summary>
        /// The type URL prefix this event formatter uses when packing messages into <see cref="Any"/>.
        /// The value is never null. Note: the type URL prefix is not used when the data within a CloudEvent
        /// is already an Any message, as the message is propagated directly.
        /// </summary>
        public string TypeUrlPrefix { get; }

        private static readonly Dictionary<AttrOneofCase, CloudEventAttributeType> protoToCloudEventAttributeType =
            new Dictionary<AttrOneofCase, CloudEventAttributeType>
            {
                { AttrOneofCase.CeBoolean, CloudEventAttributeType.Boolean },
                { AttrOneofCase.CeBytes, CloudEventAttributeType.Binary },
                { AttrOneofCase.CeInteger, CloudEventAttributeType.Integer },
                { AttrOneofCase.CeString, CloudEventAttributeType.String },
                { AttrOneofCase.CeTimestamp, CloudEventAttributeType.Timestamp },
                { AttrOneofCase.CeUri, CloudEventAttributeType.Uri },
                { AttrOneofCase.CeUriRef, CloudEventAttributeType.UriReference }
            };

        /// <summary>
        /// Constructs an instance of the formatter, using a type URL prefix of
        /// "type.googleapis.com" (the default for <see cref="Any.Pack(IMessage)"/>).
        /// </summary>
        public ProtobufEventFormatter() : this(DefaultTypeUrlPrefix)
        {
        }

        /// <summary>
        /// Constructs an instance of the formatter, using the specified type URL prefix
        /// when packing messages.
        /// </summary>
        /// <param name="typeUrlPrefix">The type URL prefix to use when packing messages
        /// into <see cref="Any"/>. Must not be null.</param>
        public ProtobufEventFormatter(string typeUrlPrefix)
        {
            TypeUrlPrefix = Validation.CheckNotNull(typeUrlPrefix, nameof(typeUrlPrefix));
        }

        /// <inheritdoc />
        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            DecodeBatchModeMessage(BinaryDataUtilities.AsStream(body), contentType, extensionAttributes);

        /// <inheritdoc />
        public override void DecodeBinaryModeEventData(ReadOnlyMemory<byte> body, CloudEvent cloudEvent)
        {
            Validation.CheckNotNull(cloudEvent, nameof(cloudEvent));
            if (cloudEvent.DataContentType is string dataContentType && dataContentType.StartsWith("text/"))
            {
                Encoding encoding = MimeUtilities.GetEncoding(new ContentType(dataContentType));
                cloudEvent.Data = BinaryDataUtilities.GetString(body, encoding);
            }
            else
            {
                cloudEvent.Data = body.ToArray();
            }
        }

        /// <inheritdoc />
        public override CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            DecodeStructuredModeMessage(BinaryDataUtilities.AsStream(body), contentType, extensionAttributes);

        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeBatchModeMessage(IEnumerable<CloudEvent> cloudEvents, out ContentType contentType)
        {
            Validation.CheckNotNull(cloudEvents, nameof(cloudEvents));

            contentType = new ContentType(BatchMediaType)
            {
                CharSet = Encoding.UTF8.WebName
            };

            var batch = new CloudEventBatch
            {
                Events = { cloudEvents.Select(cloudEvent => ConvertToProto(cloudEvent, nameof(cloudEvents))) }
            };
            return batch.ToByteArray();
        }

        // TODO: Put the boiler-plate code here into CloudEventFormatter

        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeBinaryModeEventData(CloudEvent cloudEvent)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));

            if (cloudEvent.Data is null)
            {
                return Array.Empty<byte>();
            }
            if (cloudEvent.DataContentType is string dataContentType && dataContentType.StartsWith("text/") && cloudEvent.Data is string text)
            {
                ContentType contentType = new ContentType(dataContentType);
                return MimeUtilities.GetEncoding(contentType).GetBytes(text);
            }
            if (cloudEvent.Data is byte[] bytes)
            {
                return bytes;
            }
            throw new ArgumentException($"{nameof(ProtobufEventFormatter)} cannot serialize data of type {cloudEvent.Data.GetType()} with content type '{cloudEvent.DataContentType}'");
        }

        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeStructuredModeMessage(CloudEvent cloudEvent, out ContentType contentType)
        {
            var proto = ConvertToProto(cloudEvent, nameof(cloudEvent));
            contentType = new ContentType(StructuredMediaType)
            {
                CharSet = Encoding.UTF8.WebName
            };
            return proto.ToByteArray();
        }

        /// <inheritdoc />
        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(body, nameof(body));
            var batchProto = CloudEventBatch.Parser.ParseFrom(body);
            return batchProto.Events.Select(proto => ConvertFromProto(proto, extensionAttributes, nameof(body))).ToList();
        }

        /// <inheritdoc />
        public override CloudEvent DecodeStructuredModeMessage(Stream messageBody, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(messageBody, nameof(messageBody));
            return ConvertFromProto(V1.CloudEvent.Parser.ParseFrom(messageBody), extensionAttributes, nameof(messageBody));
        }

        /// <summary>
        /// Converts the given protobuf representation of a CloudEvent into an SDK representation.
        /// </summary>
        /// <param name="proto">The protobuf representation of a CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when populating the CloudEvent. May be null.</param>
        /// <returns>The SDK representation of the CloudEvent.</returns>
        public CloudEvent ConvertFromProto(V1.CloudEvent proto, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            ConvertFromProto(Validation.CheckNotNull(proto, nameof(proto)), extensionAttributes, nameof(proto));

        private CloudEvent ConvertFromProto(V1.CloudEvent proto, IEnumerable<CloudEventAttribute>? extensionAttributes, string paramName)
        {
            var specVersion = CloudEventsSpecVersion.FromVersionId(proto.SpecVersion)
                ?? throw new ArgumentException($"Unsupported CloudEvents spec version '{proto.SpecVersion}'", paramName);

            var cloudEvent = new CloudEvent(specVersion, extensionAttributes)
            {
                Id = proto.Id,
                Source = (Uri) specVersion.SourceAttribute.Parse(proto.Source),
                Type = proto.Type
            };
            foreach (var pair in proto.Attributes)
            {
                if (!protoToCloudEventAttributeType.TryGetValue(pair.Value.AttrCase, out var attrTypeFromProto))
                {
                    // Note: impossible to cover in tests
                    throw new ArgumentException($"Unhandled protobuf attribute case: {pair.Value.AttrCase}", paramName);
                }
                
                // If we've already got an extension attribute specified for this name,
                // we validate against it and require the value in the proto to have the right
                // type. Otherwise, we create a new extension attribute of the correct type.
                var attr = cloudEvent.GetAttribute(pair.Key);
                if (attr is null)
                {
                    attr = CloudEventAttribute.CreateExtension(pair.Key, attrTypeFromProto);
                }
                // Note: if CloudEvents spec version 2.0 contains different required attributes, we may want to
                // change exactly how this is specified. For the moment, this is the simplest way of implementing the requirement.
                else if (attr.IsRequired)
                {
                    // The required attributes are all specified as proto fields.
                    // They can't appear in the Attributes repeated field as well.
                    throw new ArgumentException(
                        $"Attribute '{attr.Name}' is a required attribute, and must only be specified via the top-level proto field.");
                }
                else if (attr.Type != attrTypeFromProto)
                {
                    // This prevents any type changes, even those which might validate correctly
                    // otherwise (e.g. between Uri and UriRef).
                    throw new ArgumentException(
                        $"Attribute '{attr.Name}' was specified with type '{attr.Type}', but has type '{attrTypeFromProto}' in the protobuf representation.");
                }
                
                // Note: the indexer performs validation.
                cloudEvent[attr] = pair.Value.AttrCase switch
                {
                    AttrOneofCase.CeBoolean => pair.Value.CeBoolean,
                    AttrOneofCase.CeBytes => pair.Value.CeBytes.ToByteArray(),
                    AttrOneofCase.CeInteger => pair.Value.CeInteger,
                    AttrOneofCase.CeString => pair.Value.CeString,
                    AttrOneofCase.CeTimestamp => pair.Value.CeTimestamp.ToDateTimeOffset(),
                    AttrOneofCase.CeUri => CloudEventAttributeType.Uri.Parse(pair.Value.CeUri),
                    AttrOneofCase.CeUriRef => CloudEventAttributeType.UriReference.Parse(pair.Value.CeUriRef),
                    _ => throw new ArgumentException($"Unhandled protobuf attribute case: {pair.Value.AttrCase}")
                };
            }

            DecodeStructuredModeData(proto, cloudEvent);

            return Validation.CheckCloudEventArgument(cloudEvent, paramName);
        }

        /// <summary>
        /// Decodes the "data" property provided within a structured-mode message,
        /// populating the <see cref="CloudEvents.CloudEvent.Data"/> property accordingly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This implementation simply converts binary data to a byte array, leaves proto data
        /// as an <see cref="Google.Protobuf.WellKnownTypes.Any"/>, and converts text data to a string.
        /// </para>
        /// <para>
        /// Override this method to provide more specialized conversions, such as to use <see cref="ByteString"/>
        /// instead of a byte array, or to "unwrap" the proto data to generated code.
        /// </para>
        /// </remarks>
        /// <param name="proto">The protobuf representation of the CloudEvent. Will not be null.</param>
        /// <param name="cloudEvent">The event being decoded. This should not be modified except to
        /// populate the <see cref="CloudEvents.CloudEvent.Data"/> property, but may be used to provide extra
        /// information such as the data content type. Will not be null.</param>
        /// <returns>The data to populate in the <see cref="CloudEvents.CloudEvent.Data"/> property.</returns>
        protected virtual void DecodeStructuredModeData(V1.CloudEvent proto, CloudEvent cloudEvent) =>
            cloudEvent.Data = proto.DataCase switch
            {
                DataOneofCase.BinaryData => proto.BinaryData.ToByteArray(),
                DataOneofCase.ProtoData => proto.ProtoData,
                DataOneofCase.TextData => proto.TextData,
                DataOneofCase.None => null,
                // Note: impossible to cover in tests
                _ => throw new ArgumentException($"Unhandled protobuf data case: {proto.DataCase}")
            };

        /// <summary>
        /// Encodes structured (or batch) mode data within a CloudEvent, storing it in the specified <see cref="CloudEvents.CloudEvent"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent being encoded, which will have a non-null value for
        /// its <see cref="CloudEvents.CloudEvent.Data"/> property.</param>
        /// <param name="proto">The protobuf representation of the CloudEvent, which will be non-null.</param>
        protected virtual void EncodeStructuredModeData(CloudEvent cloudEvent, V1.CloudEvent proto)
        {
            switch (cloudEvent.Data)
            {
                case IMessage message:
                    proto.ProtoData = message is Any any ? any : Any.Pack(message, TypeUrlPrefix);
                    break;
                case string text:
                    proto.TextData = text;
                    break;
                case byte[] binary:
                    proto.BinaryData = ByteString.CopyFrom(binary);
                    break;
                default:
                    throw new ArgumentException($"{nameof(ProtobufEventFormatter)} cannot serialize data of type {cloudEvent.Data!.GetType()}");
            }
        }

        /// <summary>
        /// Converts the given SDK representation of a CloudEvent to a protobuf representation.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <returns>The protobuf representation of the CloudEvent.</returns>
        public V1.CloudEvent ConvertToProto(CloudEvent cloudEvent) => ConvertToProto(cloudEvent, nameof(cloudEvent));

        private V1.CloudEvent ConvertToProto(CloudEvent cloudEvent, string paramName)
        {
            Validation.CheckCloudEventArgument(cloudEvent, paramName);
            var specVersion = cloudEvent.SpecVersion;
            var proto = new V1.CloudEvent
            {
                Id = cloudEvent.Id,
                // Source is a required attribute, and we've validated the CloudEvent,
                // so it really should be non-null.
                Source = specVersion.SourceAttribute.Format(cloudEvent.Source!),
                Type = cloudEvent.Type,
                SpecVersion = cloudEvent.SpecVersion.VersionId
            };

            foreach (var pair in cloudEvent.GetPopulatedAttributes())
            {
                var attr = pair.Key;
                // Skip attributes already handled above.
                if (attr == specVersion.IdAttribute ||
                    attr == specVersion.SourceAttribute ||
                    attr == specVersion.TypeAttribute)
                {
                    continue;
                }

                var value = new CloudEventAttributeValue();
                switch (CloudEventAttributeTypes.GetOrdinal(attr.Type))
                {
                    case CloudEventAttributeTypeOrdinal.Binary:
                        value.CeBytes = ByteString.CopyFrom((byte[]) pair.Value);
                        break;
                    case CloudEventAttributeTypeOrdinal.Boolean:
                        value.CeBoolean = (bool) pair.Value;
                        break;
                    case CloudEventAttributeTypeOrdinal.Integer:
                        value.CeInteger = (int) pair.Value;
                        break;
                    case CloudEventAttributeTypeOrdinal.String:
                        value.CeString = (string) pair.Value;
                        break;
                    case CloudEventAttributeTypeOrdinal.Timestamp:
                        value.CeTimestamp = Timestamp.FromDateTimeOffset((DateTimeOffset) pair.Value);
                        break;
                    case CloudEventAttributeTypeOrdinal.Uri:
                        value.CeUri = attr.Format(pair.Value);
                        break;
                    case CloudEventAttributeTypeOrdinal.UriReference:
                        value.CeUriRef = attr.Format(pair.Value);
                        break;
                    default:
                        // Note: impossible to cover in tests
                        throw new ArgumentException($"Unhandled attribute type: {attr.Type}");
                }
                proto.Attributes.Add(attr.Name, value);
            }

            if (cloudEvent.Data is object)
            {
                EncodeStructuredModeData(cloudEvent, proto);
            }

            return proto;
        }
    }
}
