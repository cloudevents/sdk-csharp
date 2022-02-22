using CloudNative.CloudEvents.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
#if NETSTANDARD2_1_OR_GREATER
using System.Threading.Tasks;
#endif
using System.Xml;
using System.Xml.Linq;

namespace CloudNative.CloudEvents.Xml
{
    /// <summary>
    /// 
    /// </summary>
    public class XmlEventFormatter : CloudEventFormatter
    {
        private const string JsonMediaType = "application/xml";
        private const string MediaTypeSuffix = "+xml";
        private static readonly string StructuredMediaType = MimeUtilities.MediaType + MediaTypeSuffix;
        private static readonly string BatchMediaType = MimeUtilities.BatchMediaType + MediaTypeSuffix;

        internal static XNamespace CloudEventsNamespace { get; } = "http://cloudevents.io/xmlformat/V1";
        internal static XNamespace XsiNamespace { get; } = "http://www.w3.org/2001/XMLSchema-instance";
        internal static XName BatchElementName { get; } = CloudEventsNamespace + "batch";
        internal static XName EventElementName { get; } = CloudEventsNamespace + "event";
        internal static XName DataElementName { get; } = CloudEventsNamespace + "data";
        internal static XName XsiTypeAttributeName { get; } = XsiNamespace + "type";
        internal static XName SpecVersionAttributeName { get; } = CloudEventsSpecVersion.SpecVersionAttribute.Name;
        internal static XName IsRefAttributeName { get; } = "isref";

        private static readonly Dictionary<string, CloudEventAttributeType> CloudEventAttributeTypesByXsiType = new Dictionary<string, CloudEventAttributeType>
        {
            { "xs:boolean", CloudEventAttributeType.Boolean },
            { "xs:int", CloudEventAttributeType.Integer },
            { "xs:string", CloudEventAttributeType.String },
            { "xs:base64Binary", CloudEventAttributeType.Binary },
            // FIXME: needs differentiating from URI
            { "xs:anyURI", CloudEventAttributeType.UriReference },
            { "xs:dateTime", CloudEventAttributeType.Timestamp }
        };

        private static readonly Dictionary<CloudEventAttributeType, string> XsiTypesByCloudEventAttributeType = new Dictionary<CloudEventAttributeType, string>
        {
            { CloudEventAttributeType.Boolean, "xs:boolean" },
            { CloudEventAttributeType.Integer, "xs:int" },
            { CloudEventAttributeType.String, "xs:string" },
            { CloudEventAttributeType.Binary, "xs:base64Binary" },
            // These are further differentiated via the "isref" attribute
            { CloudEventAttributeType.Uri, "xs:anyURI" },
            { CloudEventAttributeType.UriReference, "xs:anyURI" },
            { CloudEventAttributeType.Timestamp, "xs:dateTime" }
        };

#if NETSTANDARD2_1_OR_GREATER
        /// <inheritdoc />
        public override async Task<CloudEvent> DecodeStructuredModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(body, nameof(body));

            // TODO: What about the content type? What if it includes an encoding?
            // TODO: Cancellation tokens? (We don't have them *anywhere*. Oops.)
            var element = await XElement.LoadAsync(body, LoadOptions.PreserveWhitespace, default).ConfigureAwait(false);
            return DecodeEvent(element, extensionAttributes);
        }
#endif

        /// <inheritdoc />
        public override CloudEvent DecodeStructuredModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(body, nameof(body));

            // TODO: Try this without the load option.
            var element = XElement.Load(body, LoadOptions.PreserveWhitespace);
            return DecodeEvent(element, extensionAttributes);
        }

        /// <inheritdoc />
        public override CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            DecodeStructuredModeMessage(BinaryDataUtilities.AsStream(body), contentType, extensionAttributes);

#if NETSTANDARD2_1_OR_GREATER
        /// <inheritdoc />
        public override async Task<IReadOnlyList<CloudEvent>> DecodeBatchModeMessageAsync(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(body, nameof(body));

            var element = await XElement.LoadAsync(body, LoadOptions.PreserveWhitespace, default).ConfigureAwait(false);
            return DecodeBatch(element, extensionAttributes);
        }
#endif

        /// <inheritdoc />
        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(Stream body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            Validation.CheckNotNull(body, nameof(body));

            var element = XElement.Load(body, LoadOptions.PreserveWhitespace);
            return DecodeBatch(element, extensionAttributes);
        }

        /// <inheritdoc />
        public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> body, ContentType? contentType, IEnumerable<CloudEventAttribute>? extensionAttributes) =>
            DecodeBatchModeMessage(BinaryDataUtilities.AsStream(body), contentType, extensionAttributes);

        // Visible for testing
        internal IReadOnlyList<CloudEvent> DecodeBatch(XElement batchElement, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            if (batchElement.Name != BatchElementName)
            {
                throw new ArgumentException("Incorrect element name for CloudEvent batch.");
            }

            var firstBadAttribute = batchElement.Attributes().FirstOrDefault();
            if (firstBadAttribute is object)
            {
                throw new ArgumentException($"Invalid attribute within <ce:batch> element: '{firstBadAttribute.Name}'");
            }

            List<CloudEvent> batch = new List<CloudEvent>();
            foreach (var node in batchElement.Nodes())
            {
                if (node is XText text)
                {
                    if (!string.IsNullOrWhiteSpace(text.Value))
                    {
                        throw new ArgumentException("Unexpected non-whitespace text node in <ce:batch>");
                    }
                }
                if (!(node is XElement child))
                {
                    continue;
                }
                batch.Add(DecodeEvent(child, extensionAttributes));
            }
            return batch;
        }

        // Internal for testing
        internal CloudEvent DecodeEvent(XElement eventElement, IEnumerable<CloudEventAttribute>? extensionAttributes)
        {
            if (eventElement.Name != EventElementName)
            {
                throw new ArgumentException("Incorrect element name for CloudEvent.");
            }

            var firstBadAttribute = eventElement.Attributes().FirstOrDefault(attr => attr.Name != SpecVersionAttributeName);
            if (firstBadAttribute is object)
            {
                throw new ArgumentException($"Invalid attribute within <ce:event> element: '{firstBadAttribute.Name}'");
            }

            if (!(eventElement.Attribute(SpecVersionAttributeName)?.Value is string specVersionToken))
            {
                throw new ArgumentException($"<ce:event> element is missing attribute {SpecVersionAttributeName}");
            }
            var specVersion = CloudEventsSpecVersion.FromVersionId(specVersionToken)
                ?? throw new ArgumentException($"Unsupported CloudEvents spec version '{specVersionToken}'");

            var cloudEvent = new CloudEvent(specVersion, extensionAttributes);
            PopulateAttributesFromStructuredEvent(cloudEvent, eventElement);
            XElement? dataElement = PopulateAttributesFromStructuredEvent(cloudEvent, eventElement);
            if (dataElement is object)
            {
                DecodeStructuredModeDataElement(dataElement, cloudEvent);
            }
            // "body" is always the parameter from the public method. It's annoying not to be able to use
            // nameof here, but this will give the appropriate result.
            return Validation.CheckCloudEventArgument(cloudEvent, "body");
        }

        /// <summary>
        /// Populates the attributes from the given element, and returns the data element, if it's present,
        /// for further processing.
        /// </summary>
        private XElement? PopulateAttributesFromStructuredEvent(CloudEvent cloudEvent, XElement eventElement)
        {
            XElement? data = null;
            var seenElementNames = new HashSet<string>();
            foreach (var node in eventElement.Nodes())
            {
                if (node is XText text)
                {
                    if (!string.IsNullOrWhiteSpace(text.Value))
                    {
                        throw new ArgumentException("Unexpected non-whitespace text node in <ce:event>");
                    }
                }
                if (!(node is XElement child))
                {
                    continue;
                }
                var name = child.Name;
                if (name.Namespace != CloudEventsNamespace)
                {
                    throw new ArgumentException("Elements within <ce:event> must be in the CloudEvents namespace");
                }

                var localName = name.LocalName;
                if (!seenElementNames.Add(localName))
                {
                    throw new ArgumentException($"Duplicate element within <ce:event>: {localName}");
                }

                // Skip the data element, which will be handled later
                if (name == DataElementName)
                {
                    data = child;
                    continue;
                }
                if (child.HasElements)
                {
                    throw new ArgumentException("XML elements representing CloudEvent attributes must not have child elements");
                }

                var firstBadAttribute = child.Attributes().FirstOrDefault(attr => attr.Name != XsiTypeAttributeName && attr.Name != IsRefAttributeName);
                if (firstBadAttribute is object)
                {
                    throw new ArgumentException($"Invalid attribute within child of <ce:event>: '{firstBadAttribute.Name}'");
                }

                string value = child.Value;
                var cloudEventAttribute = cloudEvent.GetAttribute(localName);
                var attributeXsiType = child.Attribute(XsiTypeAttributeName)?.Value;
                var attributeIsRef = child.Attribute(IsRefAttributeName)?.Value;
                var cloudEventAttributeType = CloudEventAttributeTypesByXsiType.GetValueOrDefault(attributeXsiType ?? "");
                if (attributeXsiType is object && cloudEventAttributeType is null)
                {
                    throw new ArgumentException($"Unknown xsi:type '{attributeXsiType}' for element '{localName}'");
                }
                // TODO: Much more detail about this when we have a spec
                if (attributeIsRef == "false" && cloudEventAttributeType == CloudEventAttributeType.UriReference)
                {
                    cloudEventAttributeType = CloudEventAttributeType.Uri;
                }                

                // Extension attribute
                if (cloudEventAttribute is null || cloudEventAttribute.IsExtension)
                {
                    if (attributeXsiType is null)
                    {
                        throw new ArgumentException($"Element '{localName}' representing a CloudEvent extension attribute does not specify its type.");
                    }
                    if (cloudEventAttribute is null)
                    {
                        // We know that cloudEventAttributeType is non-null at this point, as attributeXsiType is non-null, and we
                        // passed the earlier check.
                        cloudEventAttribute = CloudEventAttribute.CreateExtension(localName, cloudEventAttributeType!);
                    }
                    else if (cloudEventAttribute.Type != cloudEventAttributeType)
                    {
                        throw new ArgumentException($"Element '{localName}' specifies xsi:type '{attributeXsiType}', but an existing extension attribute exists with type '{cloudEventAttribute.Type}'");
                    }
                }
                // Known attributes (required or optional) don't have to have a type, but if one is specified it should match.
                else if (attributeXsiType is object)
                {
                    if (cloudEventAttribute.Type != cloudEventAttributeType)
                    {
                        throw new ArgumentException($"Element '{localName}' specifies xsi:type '{attributeXsiType}', but the attribute is known to have type '{cloudEventAttribute.Type}'");
                    }
                }
                // TODO: Is the XML format of the string always the same as the CloudEvent one? I suspect so, but it may be worth validating that, particularly for timestamps.
                // Note: we *could* infer an extension type of integer and Boolean, but not other extension types.
                // (We don't want to assume that everything that looks like a timestamp is a timestamp, etc.)
                // Stick to strings for consistency.
                cloudEvent[cloudEventAttribute] = cloudEventAttribute.Parse(value);
            }
            return data;
        }

        /// <summary>
        /// Decodes the "data" property provided within a structured-mode message,
        /// populating the <see cref="CloudEvent.Data"/> property accordingly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// FIXME
        /// </para>
        /// <para>
        /// Override this method to provide more specialized conversions.
        /// </para>
        /// </remarks>
        /// <param name="dataElement">The "data" property value within the structured-mode message. Will not be null, and will
        /// not have a null token type.</param>
        /// <param name="cloudEvent">The event being decoded. This should not be modified except to
        /// populate the <see cref="CloudEvent.Data"/> property, but may be used to provide extra
        /// information such as the data content type. Will not be null, and the <see cref="CloudEvent.DataContentType"/>
        /// property will be non-null.</param>
        /// <returns>The data to populate in the <see cref="CloudEvent.Data"/> property.</returns>
        protected virtual void DecodeStructuredModeDataElement(XElement dataElement, CloudEvent cloudEvent)
        {
            string? xsiType = dataElement.Attribute(XsiTypeAttributeName)?.Value;
            if (xsiType is null)
            {
                throw new ArgumentException("<ce:data> element within <ce:event> element must specify an xsi:type attribute");
            }
            // TODO: More validation, e.g. no elements in an xs:string value.
            switch (xsiType)
            {
                case "xs:string":
                    if (dataElement.Elements().Any())
                    {
                        throw new ArgumentException("<ce:data> element with xsi:type of xs:string must not have child elements");
                    }
                    cloudEvent.Data = dataElement.Value;
                    break;
                case "xs:any":
                    foreach (var node in dataElement.Nodes())
                    {
                        if (node is XText text)
                        {
                            if (!string.IsNullOrWhiteSpace(text.Value))
                            {
                                throw new ArgumentException("<ce:data> element with xsi:type of xs:any must not contain non-whitespace text nodes");
                            }
                        }
                    }
                    if (dataElement.Elements().Count() != 1)
                    {
                        throw new ArgumentException("<ce:data> element with xsi:type of xs:any must have exactly one child element");
                    }
                    cloudEvent.Data = dataElement.Elements().Single();
                    break;
                case "xs:base64Binary":
                    if (dataElement.Elements().Any())
                    {
                        throw new ArgumentException("<ce:data> element with xsi:type of xs:base64Binary must not have child elements");
                    }
                    cloudEvent.Data = Convert.FromBase64String(dataElement.Value);
                    break;
                default:
                    throw new ArgumentException($"Unexpected xsi:type for <ce:data> element: '{xsiType}'");
            }
        }

        /// <inheritdoc />
        public override void DecodeBinaryModeEventData(ReadOnlyMemory<byte> body, CloudEvent cloudEvent)
        {
            throw new NotImplementedException();
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
            // TODO: XmlWriterSettings?
            var writer = XmlWriter.Create(stream);
            WriteCloudEventForBatchOrStructuredMode(writer, cloudEvent);
            writer.Flush();
            return stream.ToArray();
        }

        private void WriteCloudEventForBatchOrStructuredMode(XmlWriter writer, CloudEvent cloudEvent)
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));

            // TODO: To we actually want to hard-code "ce" here?
            WriteStartElement(writer, EventElementName, "ce");
            WriteAttributeString(writer, SpecVersionAttributeName, cloudEvent.SpecVersion.VersionId);

            var attributes = cloudEvent.GetPopulatedAttributes();
            foreach (var keyValuePair in attributes)
            {
                var attribute = keyValuePair.Key;
                var value = keyValuePair.Value;
                writer.WriteStartElement(attribute.Name, CloudEventsNamespace.NamespaceName);
                if (attribute.IsExtension)
                {
                    WriteAttributeString(writer, XsiTypeAttributeName, XsiTypesByCloudEventAttributeType[attribute.Type]);
                    if (attribute.Type == CloudEventAttributeType.Uri)
                    {
                        WriteAttributeString(writer, IsRefAttributeName, "false");
                    }
                }
                writer.WriteString(attribute.Format(value));
                writer.WriteEndElement();
            }

            if (cloudEvent.Data is object)
            {
                EncodeStructuredModeData(cloudEvent, writer);
            }
            writer.WriteEndElement();
        }

        /// <summary>
        /// Encodes structured mode data within a CloudEvent, writing it to the specified <see cref="XmlWriter"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// </para>
        /// </remarks>
        /// <param name="cloudEvent">The CloudEvent being encoded, which will have a non-null value for
        /// its <see cref="CloudEvent.Data"/> property.
        /// <param name="writer"/>The writer to serialize the data to. Will not be null.</param>
        protected virtual void EncodeStructuredModeData(CloudEvent cloudEvent, XmlWriter writer)
        {
            WriteStartElement(writer, DataElementName);
            if (cloudEvent.Data is string text)
            {
                WriteAttributeString(writer, XsiTypeAttributeName, "xs:string");
                writer.WriteString(text);
            }
            else if (cloudEvent.Data is byte[] binary)
            {
                WriteAttributeString(writer, XsiTypeAttributeName, "xs:base64Binary");
                writer.WriteBase64(binary, 0, binary.Length);
            }
            else if (cloudEvent.Data is XElement element)
            {
                WriteAttributeString(writer, XsiTypeAttributeName, "xs:any");
                element.WriteTo(writer);
            }
            writer.WriteEndElement();
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
            // TODO: XmlWriterSettings?
            var writer = XmlWriter.Create(stream);
            // TODO: Configurable prefix?
            WriteStartElement(writer, BatchElementName, "ce");
            foreach (var cloudEvent in cloudEvents)
            {
                WriteCloudEventForBatchOrStructuredMode(writer, cloudEvent);
            }
            writer.WriteEndElement();
            writer.Flush();
            return stream.ToArray();
        }

        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeBinaryModeEventData(CloudEvent cloudEvent)
        {
            throw new NotImplementedException();
        }

        // Convenience methods 
        private static void WriteAttributeString(XmlWriter writer, XName name, string value) =>
            writer.WriteAttributeString(name.LocalName, name.NamespaceName, value);

        private static void WriteStartElement(XmlWriter writer, XName name, string? prefix = null) =>
            writer.WriteStartElement(prefix, name.LocalName, name.NamespaceName);
    }
}
