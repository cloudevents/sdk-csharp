// Copyright 2021 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading.Tasks;

namespace CloudNative.CloudEvents
{
    /// <summary>
    /// Utilities methods for dealing with binary data, converting between
    /// streams, arrays, Memory{T} etc.
    /// </summary>
    internal static class BinaryDataUtilities
    {
        internal async static Task<byte[]> ToByteArrayAsync(Stream stream)
        {
            // TODO: Optimize if it's already a MemoryStream?
            var memory = new MemoryStream();
            await stream.CopyToAsync(memory).ConfigureAwait(false);
            return memory.ToArray();
        }

        internal static byte[] ToByteArray(Stream stream)
        {
            var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }
}
