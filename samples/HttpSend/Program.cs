// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Http;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;

namespace HttpSend
{
    // This application uses the McMaster.Extensions.CommandLineUtils library for parsing the command
    // line and calling the application code. The [Option] attributes designate the parameters.
    class Program
    {
        [Option(Description = "CloudEvents 'source' (default: urn:example-com:mysource:abc)", LongName = "source",
            ShortName = "s")]
        private string Source { get; } = "urn:example-com:mysource:abc";

        [Option(Description = "CloudEvents 'type' (default: com.example.myevent)", LongName = "type", ShortName = "t")]
        private string Type { get; } = "com.example.myevent";

        [Required,Option(Description = "HTTP(S) address to send the event to", LongName = "url", ShortName = "u"),]
        private Uri Url { get; }

        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        private async Task OnExecuteAsync()
        {
            var cloudEvent = new CloudEvent
            {
                Type = Type,
                Source = new Uri(Source),
                DataContentType = MediaTypeNames.Application.Json,
                Data = JsonConvert.SerializeObject("hey there!")
            };

            var content = new CloudEventHttpContent(cloudEvent, ContentMode.Structured, new JsonEventFormatter());

            var httpClient = new HttpClient();
            // your application remains in charge of adding any further headers or 
            // other information required to authenticate/authorize or otherwise
            // dispatch the call at the server.
            var result = (await httpClient.PostAsync(this.Url, content));

            Console.WriteLine(result.StatusCode);
        }
    }
}