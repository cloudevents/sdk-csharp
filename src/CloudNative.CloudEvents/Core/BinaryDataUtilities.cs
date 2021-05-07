// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents.Core
{
    /// <summary>
    /// Utilities methods for dealing with binary data, converting between
    /// streams, arrays, Memory{T} etc.
    /// </summary>
    public static class BinaryDataUtilities
    {
        /// <summary>
        /// Asynchronously consumes the remaining content of the given stream, returning
        /// it as a byte array.
        /// </summary>
        /// <param name="stream">The stream to read from. Must not be null.</param>
        /// <returns>The content of the stream (from its original position), as a byte array.</returns>
        public async static Task<byte[]> ToByteArrayAsync(Stream stream)
        {
            Validation.CheckNotNull(stream, nameof(stream));
            // TODO: Optimize if it's already a MemoryStream?
            var memory = new MemoryStream();
            await stream.CopyToAsync(memory).ConfigureAwait(false);
            return memory.ToArray();
        }

        /// <summary>
        /// Consumes the remaining content of the given stream, returning
        /// it as a byte array.
        /// </summary>
        /// <param name="stream">The stream to read from. Must not be null.</param>
        /// <returns>The content of the stream (from its original position), as a byte array.</returns>
        public static byte[] ToByteArray(Stream stream)
        {
            Validation.CheckNotNull(stream, nameof(stream));
            var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }
}
