using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Services.SqsPublisher;

/// <summary>
/// Сервис публикации данных транспортного средства в очередь SQS
/// </summary>
public interface ISqsPublisher
{
    /// <summary>
    /// Отправляет данные транспортного средства в очередь
    /// </summary>
    Task SendVehicleAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
}
