using Amazon.SQS;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using StackExchange.Redis;
using Xunit;

namespace ProjectApp.Tests.Fixtures;

/// <summary>
/// Фикстура для интеграционных тестов — поднимает контейнеры Redis, MinIO и ElasticMQ
/// </summary>
public class ServiceFixture : IAsyncLifetime
{
    private IContainer _redis = null!;
    private IContainer _minio = null!;
    private IContainer _elasticMq = null!;

    public WebApplicationFactory<Program> ApiFactory { get; private set; } = null!;
    public IAmazonSQS SqsClient { get; private set; } = null!;
    public IMinioClient MinioClient { get; private set; } = null!;
    public IConnectionMultiplexer RedisConnection { get; private set; } = null!;

    public string SqsServiceUrl => $"http://localhost:{_elasticMq.GetMappedPublicPort(9324)}";
    public string MinioEndpoint => $"localhost:{_minio.GetMappedPublicPort(9000)}";
    public string RedisConnectionString => $"localhost:{_redis.GetMappedPublicPort(6379)},abortConnect=false";

    public async Task InitializeAsync()
    {
        _redis = new ContainerBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
            .Build();

        _minio = new ContainerBuilder()
            .WithImage("minio/minio")
            .WithCommand("server", "/data")
            .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
            .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
            .WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9000))
            .Build();

        _elasticMq = new ContainerBuilder()
            .WithImage("softwaremill/elasticmq-native")
            .WithPortBinding(9324, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9324))
            .Build();

        await Task.WhenAll(
            _redis.StartAsync(),
            _minio.StartAsync(),
            _elasticMq.StartAsync());

        SqsClient = new AmazonSQSClient("test", "test", new AmazonSQSConfig
        {
            ServiceURL = SqsServiceUrl
        });

        MinioClient = new Minio.MinioClient()
            .WithEndpoint(MinioEndpoint)
            .WithCredentials("minioadmin", "minioadmin")
            .WithSSL(false)
            .Build();

        RedisConnection = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);

        var redisConnStr = RedisConnectionString;
        var sqsUrl = SqsServiceUrl;

        ApiFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:cache"] = redisConnStr,
                        ["Sqs:ServiceUrl"] = sqsUrl,
                        ["Sqs:QueueName"] = "vehicle-queue"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IConnectionMultiplexer>(
                        ConnectionMultiplexer.Connect(redisConnStr));
                });
            });
    }

    /// <summary>
    /// Сброс состояния между тестами: очистка Redis и SQS
    /// </summary>
    public async Task ResetAsync()
    {
        var db = RedisConnection.GetDatabase();
        await db.ExecuteAsync("FLUSHDB");

        try
        {
            var queueUrl = (await SqsClient.CreateQueueAsync("vehicle-queue")).QueueUrl;
            await SqsClient.PurgeQueueAsync(new Amazon.SQS.Model.PurgeQueueRequest
            {
                QueueUrl = queueUrl
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to purge SQS queue during reset: {ex.Message}");
        }
    }

    public async Task DisposeAsync()
    {
        RedisConnection?.Dispose();
        ApiFactory?.Dispose();

        await Task.WhenAll(
            _redis.DisposeAsync().AsTask(),
            _minio.DisposeAsync().AsTask(),
            _elasticMq.DisposeAsync().AsTask());
    }
}
