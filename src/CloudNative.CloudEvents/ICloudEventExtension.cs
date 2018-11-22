// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace CloudNative.CloudEvents
{
    public interface ICloudEventExtension
    {
        void Attach(CloudEvent cloudEvent);
        bool ValidateAndNormalize(string key, ref dynamic value);
    }
}