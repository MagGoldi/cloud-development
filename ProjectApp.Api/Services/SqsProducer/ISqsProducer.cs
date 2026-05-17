using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Services.SqsProducer;

/// <summary>
/// Сервис отправки данных транспортного средства в очередь SQS
/// </summary>
public interface ISqsProducer
{
    /// <summary>
    /// Отправляет данные транспортного средства в очередь
    /// </summary>
    Task SendVehicleAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
}
