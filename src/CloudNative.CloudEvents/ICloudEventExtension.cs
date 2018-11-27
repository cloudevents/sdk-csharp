// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    using System;

    /// <summary>
    /// Implemented for extension objects that reflect CloudEvent extension specifications.
    /// </summary>
    public interface ICloudEventExtension
    {
        /// <summary>
        /// Attaches this extension instance to the given CloudEvent 
        /// </summary>
        /// <param name="cloudEvent"></param>
        void Attach(CloudEvent cloudEvent);
        /// <summary>
        /// Validates the given attribute value and normalizes it if needed.
        /// Normalization may include changing the data type.
        /// </summary>
        /// <param name="key">Attribute name</param>
        /// <param name="value">Attribute value</param>
        /// <returns>true if the attribute is handled by this extension</returns>
        bool ValidateAndNormalize(string key, ref dynamic value);
        /// <summary>
        /// Returns the CLR data type for the given attribute or NULL when
        /// the attribute is not handled by this extension,
        /// </summary>
        /// <param name="name">Attribute name</param>
        /// <returns>CLR type</returns>
        Type GetAttributeType(string name);
    }
}