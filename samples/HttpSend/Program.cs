// Copyright (c) Cloud Native Foundation. 
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace HttpSend
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Net.Http;
    using System.Net.Mime;
    using System.Threading.Tasks;
    using CloudNative.CloudEvents;
    using McMaster.Extensions.CommandLineUtils;
    using Newtonsoft.Json;

    class Program
    {
        [Option(Description = "CloudEvents 'source' (default: urn:example-com:mysource:abc)", LongName = "source",
            ShortName = "s")]
        string Source { get; } = "urn:example-com:mysource:abc";

        [Option(Description = "CloudEvents 'type' (default: com.example.myevent)", LongName = "type", ShortName = "t")]
        string Type { get; } = "com.example.myevent";

        [Required,Option(Description = "HTTP(S) address to send the event to", LongName = "url", ShortName = "u"),]
        Uri Url { get; }

        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        async Task OnExecuteAsync()
        {
            var cloudEvent = new CloudEvent(this.Type, new Uri(this.Source))
            {
                ContentType = new ContentType(MediaTypeNames.Application.Json),
                Data = JsonConvert.SerializeObject("hey there!")
            };

            var content = new CloudEventContent(cloudEvent, ContentMode.Structured, new JsonEventFormatter());

            var httpClient = new HttpClient();
            var result = (await httpClient.PostAsync(this.Url, content));

            Console.WriteLine(result.StatusCode);
        }
    }
}