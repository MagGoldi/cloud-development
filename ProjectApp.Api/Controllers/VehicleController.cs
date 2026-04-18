using ProjectApp.Api.Services.SqsPublisher;
using ProjectApp.Api.Services.VehicleGeneratorService;
using ProjectApp.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ProjectApp.Api.Controllers;

/// <summary>
/// Контроллер для генерации и получения характеристик транспортных средств
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class VehicleController(
    IVehicleGeneratorService vehicleService,
    ISqsPublisher sqsPublisher,
    ILogger<VehicleController> logger) : ControllerBase
{
    /// <summary>
    /// Возвращает сгенерированное транспортное средство по его уникальному идентификатору
    /// </summary>
    /// <param name="id">Идентификатор транспортного средства</param>
    /// <param name="cancellationToken">Токен отмены</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Vehicle), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Vehicle>> GetById([FromRoute] int id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Request received for vehicle id {Id}", id);

        if (id <= 0)
        {
            logger.LogWarning("Invalid vehicle id {Id} received", id);
            return BadRequest("Identifier must be a positive number.");
        }

        var vehicle = await vehicleService.FetchByIdAsync(id, cancellationToken);

        try
        {
            await sqsPublisher.SendVehicleAsync(vehicle, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send vehicle {Id} to SQS", id);
        }

        return Ok(vehicle);
    }
}
