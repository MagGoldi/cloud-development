using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FileServiceFunction.Models;

namespace FileServiceFunction;

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

    public async Task<string> FunctionHandler(string input)
    {
        QueueTriggerEvent? trigger = null;
        try { trigger = JsonSerializer.Deserialize<QueueTriggerEvent>(input ?? "{}"); } catch { }

        var messages = trigger?.Messages ?? [];
        foreach (var message in messages)
        {
            try { await ProcessMessageAsync(message); }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {message.Details?.Message?.MessageId}: {ex.Message}");
            }
        }

        return "{}";
    }

    private async Task ProcessMessageAsync(QueueMessage message)
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

        var hasId = doc.RootElement.TryGetProperty("id", out var idEl)
                    || doc.RootElement.TryGetProperty("Id", out idEl);

        if (!hasId || idEl.ValueKind != JsonValueKind.Number)
            throw new InvalidOperationException("Message does not contain a valid numeric 'id' field.");

        var id = idEl.GetInt32();
        var objectName = $"vehicle-{id}.json";

        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes);

        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectName,
            InputStream = stream,
            ContentType = "application/json"
        });

        Console.WriteLine($"[INFO] Saved {objectName}");
    }
}
