// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Auth.AccessControlPolicy;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Docker.DotNet;
using Docker.DotNet.Models;
using Xunit;

namespace CloudNative.CloudEvents.IntegrationTests.Aws
{
    public class AwsSqsSnsClientAdapter : IAsyncDisposable
    {
        private readonly ICollection<CreateQueueResponse> _createdQueues = new List<CreateQueueResponse>();
        private readonly ICollection<CreateTopicResponse> _createdTopics = new List<CreateTopicResponse>();
        private readonly IAmazonSimpleNotificationService _snsClient;
        private readonly IAmazonSQS _sqsClient;

        public AwsSqsSnsClientAdapter(AWSCredentials credentials, string serviceUrl)
        {
            _sqsClient = new AmazonSQSClient(
                credentials,
                new AmazonSQSConfig
                {
                    ServiceURL = serviceUrl
                });

            _snsClient = new AmazonSimpleNotificationServiceClient(
                credentials,
                new AmazonSimpleNotificationServiceConfig
                {
                    ServiceURL = serviceUrl
                });
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var queue in _createdQueues)
            {
                await _sqsClient.DeleteQueueAsync(queue.QueueUrl);
            }

            foreach (var topic in _createdTopics)
            {
                await _snsClient.DeleteTopicAsync(topic.TopicArn);
            }

            _sqsClient?.Dispose();
            _snsClient?.Dispose();
        }

        public async Task<(string QueueUrl, string TopicArn)> CreateTopicWithSubscribedQueueAsync(
            bool rawMessageDelivery,
            CancellationToken cancellationToken = default)
        {
            var queueName = $"CloudEventsTestQueue_{Guid.NewGuid():N}";
            var topicName = $"CloudEventsTestTopic_{Guid.NewGuid():N}";

            var queue = await CreateQueueAsync(queueName, cancellationToken);
            var topic = await CreateTopicAsync(topicName, cancellationToken);
            var queueArn = await GetQueueArnAsync(queue.QueueUrl, cancellationToken);
            await SubscribeSnsToSqsAsync(queueArn, topic.TopicArn, rawMessageDelivery, cancellationToken);
            await AllowPublicationFromSnsToSqsAsync(queueArn, queue.QueueUrl, topic.TopicArn, cancellationToken);
            return (queue.QueueUrl, topic.TopicArn);
        }

        public async Task<PublishResponse> SnsPublishMessageAsync(PublishRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _snsClient.PublishAsync(request, cancellationToken);

            // A little delay to make message available on sqs
            await Task.Delay(1000);
            return response;
        }

        public async Task<SendMessageResponse> SqsSendMessageAsync(SendMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            return await _sqsClient.SendMessageAsync(request, cancellationToken);
        }

        public async Task<ReceiveMessageResponse> SqsReceiveMessageAsync(string queueUrl,
            CancellationToken cancellationToken = default)
        {
            return await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MessageAttributeNames = new List<string> {"All"}
            }, cancellationToken);
        }

        private async Task<CreateQueueResponse> CreateQueueAsync(string queueName,
            CancellationToken cancellationToken = default)
        {
            var response = await _sqsClient.CreateQueueAsync(new CreateQueueRequest
                {
                    QueueName = queueName
                },
                cancellationToken);

            _createdQueues.Add(response);
            return response;
        }

        private async Task<CreateTopicResponse> CreateTopicAsync(string topicName,
            CancellationToken cancellationToken = default)
        {
            var response = await _snsClient.CreateTopicAsync(new CreateTopicRequest
                {
                    Name = topicName
                },
                cancellationToken);

            _createdTopics.Add(response);
            return response;
        }

        private async Task<string> GetQueueArnAsync(string queueUrl, CancellationToken cancellationToken)
        {
            var response = await _sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
            {
                AttributeNames = new List<string> {"QueueArn"},
                QueueUrl = queueUrl
            }, cancellationToken);

            return response.QueueARN;
        }

        private async Task<SubscribeResponse> SubscribeSnsToSqsAsync(
            string queueArn,
            string topicArn,
            bool rawMessageDelivery,
            CancellationToken cancellationToken)
        {
            var subscribeRequest = new SubscribeRequest
            {
                TopicArn = topicArn,
                Protocol = "sqs",
                Endpoint = queueArn,
                Attributes = new Dictionary<string, string>
                {
                    {"RawMessageDelivery", rawMessageDelivery.ToString()}
                }
            };

            return await _snsClient.SubscribeAsync(subscribeRequest, cancellationToken);
        }

        private async Task AllowPublicationFromSnsToSqsAsync(string queueArn, string queueUrl, string topicArn,
            CancellationToken cancellationToken)
        {
            var sqsPolicy = new Policy()
                .WithStatements(
                    new Statement(Statement.StatementEffect.Allow)
                        .WithPrincipals(new Principal(Principal.SERVICE_PROVIDER, "sns.amazonaws.com"))
                        .WithResources(new Resource(queueArn))
                        .WithConditions(ConditionFactory.NewCondition(ConditionFactory.ArnComparisonType.ArnEquals,
                            "aws:SourceArn", topicArn))
                        .WithActionIdentifiers(new ActionIdentifier("sqs:SendMessage")));

            var setQueueAttributesRequest = new SetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                Attributes = new Dictionary<string, string>
                {
                    {"Policy", sqsPolicy.ToJson()}
                }
            };

            await _sqsClient.SetQueueAttributesAsync(setQueueAttributesRequest, cancellationToken);
        }
    }


    [CollectionDefinition("LocalStack", DisableParallelization = false)]
    public class LocalStackTestsCollection : ICollectionFixture<LocalStackTestCollectionFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class LocalStackTestCollectionFixture : IAsyncLifetime
    {
        internal LocalStackContainer LocalStackContainer;

        /// <summary>
        ///     SetUp for tests collection. Runs once - before the first test in collection.
        /// </summary>
        public async Task InitializeAsync()
        {
            LocalStackContainer = await LocalStackContainer.StartAsync();
        }

        /// <summary>
        ///     TearDown for tests collection. Runs once - after the last test in collection.
        /// </summary>
        public async Task DisposeAsync()
        {
            if (LocalStackContainer != null)
            {
                await LocalStackContainer.DisposeAsync();
                LocalStackContainer = null;
            }
        }
    }

    internal class LocalStackContainer : IAsyncDisposable, IDisposable
    {
        private readonly string _containerId;

        public readonly string ServiceUrl;
        private DockerClient _dockerClient;

        public LocalStackContainer(DockerClient dockerClient, string containerId, string serviceUrl)
        {
            _dockerClient = dockerClient;
            _containerId = containerId;
            ServiceUrl = serviceUrl;
        }

        public AWSCredentials AwsCredentials => new BasicAWSCredentials("test", "test");

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public static async Task<LocalStackContainer> StartAsync(CancellationToken cancellationToken = default)
        {
            // @SEE: https://github.com/Microsoft/Docker.DotNet#usage
            var uri = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "npipe://./pipe/docker_engine"
                : "unix:///var/run/docker.sock";

            var imageName = "localstack/localstack:0.13";
            var freeTcpPort = GetFreeTcpPort();
            var containerConfig = new CreateContainerParameters
            {
                Image = imageName,
                Hostname = "localhost",
                Name = $"local-stack-{Guid.NewGuid():N}",
                Env = new List<string>
                {
                    "SERVICES=sns,sqs"
                },
                HostConfig = new HostConfig
                {
                    AutoRemove = true,
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {
                            "4566/tcp",
                            new List<PortBinding>
                            {
                                new PortBinding
                                {
                                    HostIP = "0.0.0.0",
                                    HostPort = freeTcpPort.ToString(CultureInfo.InvariantCulture)
                                }
                            }
                        }
                    }
                }
            };

            var dockerClient = new DockerClientConfiguration(new Uri(uri)).CreateClient();

            await dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = imageName
                },
                new AuthConfig(),
                new Progress<JSONMessage>(),
                cancellationToken);

            var container = await dockerClient.Containers.CreateContainerAsync(
                containerConfig,
                cancellationToken);

            await dockerClient.Containers.StartContainerAsync(
                container.ID,
                new ContainerStartParameters(),
                cancellationToken);

            var serviceUrl = $"http://{containerConfig.Hostname}:{freeTcpPort}";
            return new LocalStackContainer(dockerClient, container.ID, serviceUrl);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dockerClient.Containers.StopContainerAsync(_containerId, new ContainerStopParameters()).GetAwaiter()
                    .GetResult();
                _dockerClient?.Dispose();
                _dockerClient = null;
            }
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_dockerClient != null)
            {
                await _dockerClient.Containers.StopContainerAsync(_containerId, new ContainerStopParameters());
                _dockerClient.Dispose();
            }

            _dockerClient = null;
        }

        private static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}