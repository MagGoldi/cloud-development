using ProjectApp.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ProjectApp.Api.Services.VehicleGeneratorService;

/// <summary>
/// Декоратор над генератором транспортных средств с поддержкой кэширования через Redis
/// </summary>
public class CachedVehicleGeneratorService(
    IVehicleGeneratorService innerService,
    IDistributedCache cache,
    IConfiguration configuration,
    ILogger<CachedVehicleGeneratorService> logger) : IVehicleGeneratorService
{
    private readonly int _ttlMinutes = configuration.GetValue("CacheSettings:ExpirationMinutes", 10);

    /// <summary>
    /// Возвращает транспортное средство из кэша или генерирует новое и сохраняет в кэш
    /// </summary>
    public async Task<Vehicle> FetchByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var key = $"vehicle-{id}";

        try
        {
            var raw = await cache.GetStringAsync(key, cancellationToken);
            if (!string.IsNullOrEmpty(raw))
            {
                var vehicle = JsonSerializer.Deserialize<Vehicle>(raw);
                if (vehicle != null)
                {
                    logger.LogInformation("Vehicle {Id} retrieved from cache", id);
                    return vehicle;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for vehicle {Id}. Falling back to generation.", id);
        }

        var generatedVehicle = await innerService.FetchByIdAsync(id, cancellationToken);

        try
        {
            var opts = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_ttlMinutes)
            };

            await cache.SetStringAsync(
                key,
                JsonSerializer.Serialize(generatedVehicle),
                opts,
                cancellationToken);

            logger.LogInformation("Vehicle {Id} successfully stored in cache.", id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to store vehicle {Id} in cache.", id);
        }

        return generatedVehicle;
    }
}
