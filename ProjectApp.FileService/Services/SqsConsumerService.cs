using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using ProjectApp.Domain.Entities;

namespace ProjectApp.FileService.Services;

/// <summary>
/// Фоновый сервис для получения сообщений из SQS и сохранения данных в MinIO
/// </summary>
public class SqsConsumerService(
    IAmazonSQS sqsClient,
    MinioStorageService storageService,
    IConfiguration configuration,
    ILogger<SqsConsumerService> logger) : BackgroundService
{
    private readonly string _queueName = configuration["Sqs:QueueName"] ?? "vehicle-queue";
    private string? _queueUrl;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitializeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 5
                }, stoppingToken);

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error polling SQS");
                await Task.Delay(3000, stoppingToken);
            }
        }
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        var response = await sqsClient.CreateQueueAsync(_queueName, ct);
        _queueUrl = response.QueueUrl;
        logger.LogInformation("SQS queue {Queue} ready at {Url}", _queueName, _queueUrl);

        await storageService.EnsureBucketExistsAsync(ct);
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken ct)
    {
        try
        {
            var vehicle = JsonSerializer.Deserialize<Vehicle>(message.Body);
            if (vehicle != null)
            {
                await storageService.SaveVehicleAsync(vehicle, ct);
                logger.LogInformation("Vehicle {Id} processed and saved to MinIO", vehicle.Id);
            }

            await sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
        }
    }
}
