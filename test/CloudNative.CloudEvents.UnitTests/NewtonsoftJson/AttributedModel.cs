// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace CloudNative.CloudEvents.NewtonsoftJson.UnitTests
{
    [CloudEventFormatter(typeof(JsonEventFormatter<AttributedModel>))]
    internal class AttributedModel
    {
        public const string JsonPropertyName = "customattribute";

        [JsonProperty(JsonPropertyName)]
        public string AttributedProperty { get; set; }
    }
}
