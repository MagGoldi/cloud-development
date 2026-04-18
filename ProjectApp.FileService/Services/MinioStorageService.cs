using System.Text;
using System.Text.Json;
using Minio;
using Minio.DataModel.Args;
using ProjectApp.Domain.Entities;

namespace ProjectApp.FileService.Services;

/// <summary>
/// Сервис сохранения и чтения файлов транспортных средств в MinIO
/// </summary>
public class MinioStorageService(
    IMinioClient minioClient,
    IConfiguration configuration,
    ILogger<MinioStorageService> logger)
{
    private readonly string _bucketName = configuration["Minio:BucketName"] ?? "vehicles";

    /// <summary>
    /// Создаёт бакет, если он ещё не существует
    /// </summary>
    public async Task EnsureBucketExistsAsync(CancellationToken ct = default)
    {
        var exists = await minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucketName), ct);

        if (!exists)
        {
            await minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_bucketName), ct);
            logger.LogInformation("Bucket {Bucket} created", _bucketName);
        }
    }

    /// <summary>
    /// Сохраняет данные транспортного средства в виде JSON-файла
    /// </summary>
    public async Task SaveVehicleAsync(Vehicle vehicle, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(vehicle, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes);
        var objectName = $"vehicle-{vehicle.Id}.json";

        await minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType("application/json"), ct);

        logger.LogInformation("Vehicle {Id} saved as {Object}", vehicle.Id, objectName);
    }

    /// <summary>
    /// Читает данные транспортного средства из MinIO по идентификатору
    /// </summary>
    public async Task<Vehicle?> GetVehicleAsync(int id, CancellationToken ct = default)
    {
        var objectName = $"vehicle-{id}.json";
        using var ms = new MemoryStream();

        await minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream => stream.CopyTo(ms)), ct);

        ms.Position = 0;
        return await JsonSerializer.DeserializeAsync<Vehicle>(ms, cancellationToken: ct);
    }
}
