using System;
using RabbitMQ.Client;

namespace CloudNative.CloudEvents.RabbitMq
{
    /// <summary>
    /// Wraps the data needed to publish a message to RabbitMQ.
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="properties">The properties of the message</param>
        /// <param name="body">The actual message body / payload</param>
        public Message(IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            Properties = properties;
            Body = body;
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="properties">The properties of the message</param>
        /// <param name="body">The actual message body / payload</param>
        public Message(IBasicProperties properties,byte[] body)
        {
            Properties = properties;
            Body = new ReadOnlyMemory<byte>(body);
        }

        /// <summary>
        /// Stores the properties of the message.
        /// </summary>
        public IBasicProperties Properties { get; }

        /// <summary>
        /// The actual message body / payload.
        /// </summary>
        public ReadOnlyMemory<byte> Body { get; }
    }
}
