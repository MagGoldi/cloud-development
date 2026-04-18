using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Services.SqsPublisher;

/// <summary>
/// Реализация публикации данных транспортного средства в SQS
/// </summary>
public class SqsPublisher(
    IAmazonSQS sqsClient,
    IConfiguration configuration,
    ILogger<SqsPublisher> logger) : ISqsPublisher
{
    private readonly string _queueName = configuration["Sqs:QueueName"] ?? "vehicle-queue";
    private string? _queueUrl;

    /// <summary>
    /// Отправляет сериализованные данные транспортного средства в очередь SQS
    /// </summary>
    public async Task SendVehicleAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        var queueUrl = await GetQueueUrlAsync(cancellationToken);
        var body = JsonSerializer.Serialize(vehicle);

        await sqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = body
        }, cancellationToken);

        logger.LogInformation("Vehicle {Id} sent to SQS", vehicle.Id);
    }

    private async Task<string> GetQueueUrlAsync(CancellationToken ct)
    {
        if (_queueUrl != null)
            return _queueUrl;

        var response = await sqsClient.CreateQueueAsync(_queueName, ct);
        _queueUrl = response.QueueUrl;
        return _queueUrl;
    }
}
