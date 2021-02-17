// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

namespace CloudNative.CloudEvents.UnitTests
{
    /// <summary>
    /// Extension methods for CloudEventFormatters to simplify testing.
    /// Often in tests we have structured mode data as strings, and usually the content type isn't important,
    /// so it's useful to be able to just decode that string directly.
    /// </summary>
    internal static class CloudEventFormatterExtensions
    {
        internal static CloudEvent DecodeStructuredModeText(this CloudEventFormatter eventFormatter, string text) =>
            eventFormatter.DecodeStructuredModeMessage(Encoding.UTF8.GetBytes(text), contentType: null, extensionAttributes: null);

        internal static CloudEvent DecodeStructuredModeText(this CloudEventFormatter eventFormatter, string text, IEnumerable<CloudEventAttribute> extensionAttributes) =>
            eventFormatter.DecodeStructuredModeMessage(Encoding.UTF8.GetBytes(text), contentType: null, extensionAttributes);
    }
}
