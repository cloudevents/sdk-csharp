# Samples

This directory contains a sample ASP.NET Core application that exposes two endpoints for:

- Receiving a CloudEvent (`/api/events/receive`)
- Generating a CloudEvent (`/api/events/generate`)

## Run the sample

To run the sample, execute the `dotnet run` command in the `CloudNative.CloudEvents.AspNetCoreSample` directory.

```shell
dotnet run --framework net6.0
```

After running the web service using the command above, there are three strategies for sending requests to the web service.

### Using the `HttpSend` tool

The `HttpSend` project provides a CLI tool for sending requests to the `/api/events/receive` endpoint exposed by the service. To use the tool, navigate to the `HttpSend` directory and execute the following command:

```shell
dotnet run --framework net6.0 --url https://localhost:5001/api/events/receive
```

### Using the `.http` file

The [CloudNative.CloudEvents.AspNetCore.http file](./CloudNative.CloudEvents.AspNetCoreSample/CloudNative.CloudEvents.AspNetCoreSample.http) can be used to send requests to the web service. Native support for executing requests in `.http` file is provided in JetBrains Rider and Visual Studio. Support for sending requests in VS Code is provided via the [REST Client extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client).

### Using the `curl` command

Requests to the web service can also be run using the `curl` command line tool.

```shell
curl --request POST \
  --url https://localhost:5001/api/events/receive \
  --header 'ce-id: "c457b7c5-c038-4be9-98b9-938cb64a4fbf"' \
  --header 'ce-source: "urn:example-com:mysource:abc"' \
  --header 'ce-specversion: 1.0' \
  --header 'ce-type: "com.example.myevent"' \
  --header 'content-type: application/json' \
  --header 'user-agent: vscode-restclient' \
  --data '{"message": "Hello world!"}'
```
