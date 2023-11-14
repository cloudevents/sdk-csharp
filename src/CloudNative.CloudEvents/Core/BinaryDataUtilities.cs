// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
        /// it as a read-only memory segment.
        /// </summary>
        /// <param name="stream">The stream to read from. Must not be null.</param>
        /// <returns>The content of the stream (from its original position), as a read-only memory segment.</returns>
        public async static Task<ReadOnlyMemory<byte>> ToReadOnlyMemoryAsync(Stream stream)
        {
            Validation.CheckNotNull(stream, nameof(stream));
            // TODO: Optimize if it's already a MemoryStream? Will only work in some cases,
            // and is most likely to occur in tests, where the efficiency doesn't matter as much.
            var memory = new MemoryStream();
            await stream.CopyToAsync(memory).ConfigureAwait(false);
            // It's safe to use memory.GetBuffer() and memory.Position here, as this is a stream
            // we've created using the parameterless constructor.
            var buffer = memory.GetBuffer();
            return new ReadOnlyMemory<byte>(buffer, 0, (int) memory.Position);
        }

        /// <summary>
        /// Consumes the remaining content of the given stream, returning
        /// it as a read-only memory segment.
        /// </summary>
        /// <param name="stream">The stream to read from. Must not be null.</param>
        /// <returns>The content of the stream (from its original position), as a read-only memory segment.</returns>
        public static ReadOnlyMemory<byte> ToReadOnlyMemory(Stream stream)
        {
            Validation.CheckNotNull(stream, nameof(stream));
            // TODO: Optimize if it's already a MemoryStream? Will only work in some cases,
            // and is most likely to occur in tests, where the efficiency doesn't matter as much.
            var memory = new MemoryStream();
            stream.CopyTo(memory);
            // It's safe to use memory.GetBuffer() and memory.Position here, as this is a stream
            // we've created using the parameterless constructor.
            var buffer = memory.GetBuffer();
            return new ReadOnlyMemory<byte>(buffer, 0, (int) memory.Position);
        }

        /// <summary>
        /// Returns a read-only <see cref="MemoryStream"/> view over the given memory where
        /// possible, or over a copy of the data if the memory cannot be read as an array segment.
        /// This method should be used with care, due to the "sometimes shared, sometimes not"
        /// nature of the result.
        /// </summary>
        /// <param name="memory">The memory to create a stream view over.</param>
        /// <returns>A read-only stream view over <paramref name="memory"/>.</returns>
        public static MemoryStream AsStream(ReadOnlyMemory<byte> memory)
        {
            var segment = GetArraySegment(memory);
            return new MemoryStream(segment.Array, segment.Offset, segment.Count, false);
        }

        /// <summary>
        /// Decodes the given memory as a string, using the specified encoding.
        /// </summary>
        /// <param name="memory">The memory to decode.</param>
        /// <param name="encoding">The encoding to use. Must not be null.</param>
        public static string GetString(ReadOnlyMemory<byte> memory, Encoding encoding)
        {
            Validation.CheckNotNull(encoding, nameof(encoding));

            // TODO: If we introduce an additional netstandard2.1 target, we can use encoding.GetString(memory.Span)
            var segment = GetArraySegment(memory);
            return encoding.GetString(segment.Array, segment.Offset, segment.Count);
        }

        /// <summary>
        /// Copies the given memory to a stream, asynchronously.
        /// </summary>
        /// <param name="source">The source memory to copy from.</param>
        /// <param name="destination">The stream to copy to. Must not be null.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CopyToStreamAsync(ReadOnlyMemory<byte> source, Stream destination)
        {
            Validation.CheckNotNull(destination, nameof(destination));
            var segment = GetArraySegment(source);
            await destination.WriteAsync(segment.Array, segment.Offset, segment.Count).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns the data from <paramref name="memory"/> as a byte array, return the underlying array
        /// if there is one, or creating a copy otherwise. This method should be used with care, due to the
        /// "sometimes shared, sometimes not" nature of the result. (It is generally safe to use this with the result
        /// of encoding a CloudEvent, assuming the same memory is not used elsewhere.)
        /// </summary>
        /// <param name="memory">The memory to obtain the data from.</param>
        /// <returns>The data in <paramref name="memory"/> as an array.</returns>
        public static byte[] AsArray(ReadOnlyMemory<byte> memory)
        {
            var segment = GetArraySegment(memory);
            // We probably don't actually need to check the offset: if the count is the same as the length,
            // I can't see how the offset can be non-zero. But it doesn't *hurt* as a check.
            return segment.Offset == 0 && segment.Count == segment.Array.Length
                ? segment.Array
                : memory.ToArray();
        }

        private static ArraySegment<byte> GetArraySegment(ReadOnlyMemory<byte> memory) =>
            MemoryMarshal.TryGetArray(memory, out var segment)
                ? segment
                : new ArraySegment<byte>(memory.ToArray());
    }
}
