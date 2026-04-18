using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Services.VehicleGeneratorService;

/// <summary>
/// Сервис генерации данных транспортных средств на основе Bogus
/// </summary>
public class VehicleGeneratorService(
    VehicleFaker faker,
    ILogger<VehicleGeneratorService> logger) : IVehicleGeneratorService
{
    /// <summary>
    /// Генерирует новые данные транспортного средства для заданного идентификатора
    /// </summary>
    public Task<Vehicle> FetchByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Generating new vehicle data for id {Id}", id);
        var vehicle = faker.Generate();
        vehicle.Id = id;
        return Task.FromResult(vehicle);
    }
}
