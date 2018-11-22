// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    /// <summary>
    /// ContentMode enumeration for transports
    /// </summary>
    public enum ContentMode
    {
        // Structured mode. The complete CloudEvent is contained in the transport body
        Structured,
        // Binary mode. The CloudEvent is projected onto the transport frame
        Binary
    }
}