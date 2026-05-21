using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace FileServiceFunction;

/// <summary>
/// Cloud Function файлового сервиса. Получает пакет сообщений из Yandex Message Queue
/// и сохраняет данные каждого транспортного средства в Yandex Object Storage.
/// </summary>
public class Handler
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public Handler()
    {
        var endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT")
                       ?? "https://storage.yandexcloud.net";
        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";
        _bucketName = Environment.GetEnvironmentVariable("S3_BUCKET") ?? "vehicles-storage";

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true
        };
        _s3Client = new AmazonS3Client(credentials, config);
    }

    /// <summary>
    /// Точка входа Cloud Function. Обрабатывает пакет сообщений из очереди
    /// и сохраняет каждое транспортное средство как JSON-файл в Object Storage.
    /// </summary>
    /// <param name="input">Сериализованное событие триггера Message Queue</param>
    public string FunctionHandler(string input)
    {
        QueueTriggerEvent? trigger = null;
        try { trigger = JsonSerializer.Deserialize<QueueTriggerEvent>(input ?? "{}"); } catch { }

        var messages = trigger?.Messages ?? [];
        foreach (var message in messages)
        {
            try { ProcessMessage(message); }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {message.Details?.Message?.MessageId}: {ex.Message}");
            }
        }

        return "{}";
    }

    private void ProcessMessage(QueueMessage message)
    {
        var rawBody = message.Details?.Message?.Body ?? "";

        string json;
        try
        {
            var decoded = Convert.FromBase64String(rawBody);
            json = Encoding.UTF8.GetString(decoded);
        }
        catch
        {
            json = rawBody;
        }

        using var doc = JsonDocument.Parse(json);
        var idProp = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl
                   : doc.RootElement.TryGetProperty("Id", out var idEl2) ? idEl2 : default;
        var id = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt32() : 0;
        var objectName = $"vehicle-{id}.json";

        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes);

        _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectName,
            InputStream = stream,
            ContentType = "application/json"
        }).GetAwaiter().GetResult();

        Console.WriteLine($"[INFO] Saved {objectName}");
    }
}

public class QueueTriggerEvent
{
    [JsonPropertyName("messages")]
    public List<QueueMessage> Messages { get; set; } = [];
}

public class QueueMessage
{
    [JsonPropertyName("event_metadata")]
    public EventMetadata? EventMetadata { get; set; }

    [JsonPropertyName("details")]
    public MessageDetails? Details { get; set; }
}

public class EventMetadata
{
    [JsonPropertyName("event_id")] public string EventId { get; set; } = "";
    [JsonPropertyName("event_type")] public string EventType { get; set; } = "";
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
}

public class MessageDetails
{
    [JsonPropertyName("queue_id")] public string QueueId { get; set; } = "";
    [JsonPropertyName("message")] public SqsMessage? Message { get; set; }
}

public class SqsMessage
{
    [JsonPropertyName("message_id")] public string MessageId { get; set; } = "";
    [JsonPropertyName("md5_of_body")] public string Md5OfBody { get; set; } = "";
    [JsonPropertyName("body")] public string Body { get; set; } = "";
    [JsonPropertyName("attributes")] public Dictionary<string, string>? Attributes { get; set; }
}
