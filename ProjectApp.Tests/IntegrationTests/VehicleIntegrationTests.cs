using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Amazon.SQS.Model;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Minio.DataModel.Args;
using ProjectApp.Domain.Entities;
using ProjectApp.Tests.Fixtures;
using Xunit;

namespace ProjectApp.Tests.IntegrationTests;

/// <summary>
/// Интеграционные тесты для проверки совместной работы сервисов
/// </summary>
public class VehicleIntegrationTests : IClassFixture<ServiceFixture>, IAsyncLifetime
{
    private readonly ServiceFixture _fixture;
    private readonly HttpClient _httpClient;

    public VehicleIntegrationTests(ServiceFixture fixture)
    {
        _fixture = fixture;
        _httpClient = fixture.ApiFactory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetVehicle_ReturnsValidData()
    {
        var response = await _httpClient.GetAsync("/api/vehicle/1");

        response.EnsureSuccessStatusCode();
        var vehicle = await response.Content.ReadFromJsonAsync<Vehicle>();

        Assert.NotNull(vehicle);
        Assert.Equal(1, vehicle.Id);
        Assert.False(string.IsNullOrEmpty(vehicle.Vin));
        Assert.False(string.IsNullOrEmpty(vehicle.Brand));
        Assert.False(string.IsNullOrEmpty(vehicle.Model));
        Assert.InRange(vehicle.Year, 1984, DateTime.Now.Year);
        Assert.True(vehicle.Mileage >= 0);
    }

    [Fact]
    public async Task GetVehicle_SecondRequest_ReturnsCachedData()
    {
        var first = await _httpClient.GetFromJsonAsync<Vehicle>("/api/vehicle/42");
        var second = await _httpClient.GetFromJsonAsync<Vehicle>("/api/vehicle/42");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Vin, second.Vin);
        Assert.Equal(first.Brand, second.Brand);
        Assert.Equal(first.Model, second.Model);
        Assert.Equal(first.Year, second.Year);
        Assert.Equal(first.Mileage, second.Mileage);
    }

    [Fact]
    public async Task GetVehicle_PublishesMessageToSqs()
    {
        await _httpClient.GetAsync("/api/vehicle/7");

        await Task.Delay(700);

        var queueUrl = (await _fixture.SqsClient.CreateQueueAsync("vehicle-queue")).QueueUrl;

        var messages = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 5
        });

        Assert.NotEmpty(messages.Messages);

        var vehicle = JsonSerializer.Deserialize<Vehicle>(messages.Messages[0].Body);
        Assert.NotNull(vehicle);
        Assert.Equal(7, vehicle.Id);
    }

    [Fact]
    public async Task MinioStorage_SavesAndRetrievesFile()
    {
        var minio = _fixture.MinioClient;
        var bucketName = "test-vehicles";

        var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
        if (!exists)
            await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));

        var vehicle = new Vehicle
        {
            Id = 99,
            Vin = "TEST12345678VIN01",
            Brand = "Toyota",
            Model = "Camry",
            Year = 2020,
            BodyType = "Седан",
            FuelType = "Бензин",
            Color = "white",
            Mileage = 15000,
            LastServiceDate = new DateOnly(2023, 6, 15)
        };

        var json = JsonSerializer.Serialize(vehicle);
        var bytes = Encoding.UTF8.GetBytes(json);
        using var uploadStream = new MemoryStream(bytes);

        await minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject("vehicle-99.json")
            .WithStreamData(uploadStream)
            .WithObjectSize(uploadStream.Length)
            .WithContentType("application/json"));

        using var downloadStream = new MemoryStream();
        await minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject("vehicle-99.json")
            .WithCallbackStream(s => s.CopyTo(downloadStream)));

        downloadStream.Position = 0;
        var loaded = await JsonSerializer.DeserializeAsync<Vehicle>(downloadStream);

        Assert.NotNull(loaded);
        Assert.Equal(vehicle.Id, loaded.Id);
        Assert.Equal(vehicle.Vin, loaded.Vin);
        Assert.Equal(vehicle.Brand, loaded.Brand);
    }

    [Fact]
    public async Task Redis_CachesVehicleData()
    {
        await _httpClient.GetAsync("/api/vehicle/55");

        using var scope = _fixture.ApiFactory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        var cached = await cache.GetStringAsync("vehicle-55");

        Assert.NotNull(cached);

        var vehicle = JsonSerializer.Deserialize<Vehicle>(cached);
        Assert.NotNull(vehicle);
        Assert.Equal(55, vehicle.Id);
    }

    [Fact]
    public async Task GetVehicle_WithInvalidId_ReturnsBadRequest()
    {
        var response = await _httpClient.GetAsync("/api/vehicle/-1");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
