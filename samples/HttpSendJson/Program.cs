// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Http;
using CloudNative.CloudEvents.SystemTextJson;
using DocoptNet;
using System.Net.Mime;
using static System.Console;

// This application uses the docopt.net library for parsing the command
// line and calling the application code.
ProgramArguments programArguments = new();
var result = await ProgramArguments.CreateParserWithVersion()
.Parse(args)
.Match(RunAsync,
    result => { WriteLine(result.Help); return Task.FromResult(1); },
    result => { WriteLine(result.Version); return Task.FromResult(0); },
    result => { Error.WriteLine(result.Usage); return Task.FromResult(1); });
return result;

static async Task<int> RunAsync(ProgramArguments args)
{
    var cloudEvent = new CloudEvent
    {
        Id = Guid.NewGuid().ToString(),
        Type = args.OptType,
        Source = new Uri(args.OptSource),
        DataContentType = MediaTypeNames.Application.Json,
        Data = System.Text.Json.JsonSerializer.Serialize("hey there!", GeneratedJsonContext.Default.String)
    };

    var content = cloudEvent.ToHttpContent(ContentMode.Structured, new JsonEventFormatter(GeneratedJsonContext.Default));

    var httpClient = new HttpClient();
    // Your application remains in charge of adding any further headers or
    // other information required to authenticate/authorize or otherwise
    // dispatch the call at the server.
    var result = await httpClient.PostAsync(args.OptUrl, content);

    WriteLine(result.StatusCode);
    return 0;
}

[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
internal partial class GeneratedJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}

[DocoptArguments]
partial class ProgramArguments
{
    const string Help = @"HttpSendJson.

    Usage:
      HttpSendJson --url=URL [--type=TYPE] [--source=SOURCE]
      HttpSendJson (-h | --help)
      HttpSendJson --version

    Options:
      --url=URL                 HTTP(S) address to send the event to.
      --type=TYPE               CloudEvents 'type' [default: com.example.myevent].
      --source=SOURCE           CloudEvents 'source' [default: urn:example-com:mysource:abc].
      -h --help                 Show this screen.
      --version                 Show version.
";
    public static string Version => $"producer {typeof(ProgramArguments).Assembly.GetName().Version}";
    public static IParser<ProgramArguments> CreateParserWithVersion() => CreateParser().WithVersion(Version);
}