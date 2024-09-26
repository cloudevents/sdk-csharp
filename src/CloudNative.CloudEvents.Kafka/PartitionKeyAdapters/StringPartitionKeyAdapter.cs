namespace CloudNative.CloudEvents.Kafka.PartitionKeyAdapters
{
    /// <summary>
    /// Partion Key Adapter that skips handling the key.
    /// </summary>
    public class StringPartitionKeyAdapter : IPartitionKeyAdapter<string?>
    { 
        /// <inheritdoc/>
        public bool ConvertKeyToPartitionKeyAttributeValue(string? keyValue, out string? attributeValue)
        {
            attributeValue = keyValue;
            return true;
        }

        /// <inheritdoc/>
        public bool ConvertPartitionKeyAttributeValueToKey(string? attributeValue, out string? keyValue)
        {
            keyValue = attributeValue;
            return true;
        }
    }
}