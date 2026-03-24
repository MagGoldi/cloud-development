using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Services.VehicleGeneratorService;

public class VehicleGeneratorService(
    VehicleFaker faker,
    ILogger<VehicleGeneratorService> logger) : IVehicleGeneratorService
{
    public Task<Vehicle> FetchByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Generating new vehicle data for id {Id}", id);
        var vehicle = faker.Generate();
        vehicle.Id = id;
        return Task.FromResult(vehicle);
    }
}
