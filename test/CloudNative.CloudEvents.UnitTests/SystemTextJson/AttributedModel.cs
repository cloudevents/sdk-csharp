// Copyright 2021 Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace CloudNative.CloudEvents.SystemTextJson.UnitTests
{
    [CloudEventFormatter(typeof(JsonEventFormatter<AttributedModel>))]
    internal class AttributedModel
    {
        public const string JsonPropertyName = "customattribute";

        [JsonPropertyName(JsonPropertyName)]
        public string? AttributedProperty { get; set; }
    }
}
