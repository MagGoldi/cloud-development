using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace ApiFunction;

/// <summary>
/// Cloud Function генерации транспортного средства по идентификатору.
/// При наличии кэша в Object Storage возвращает сохранённые данные;
/// иначе генерирует новые и публикует их в Message Queue.
/// </summary>
public class Handler
{
    private static readonly Random _rng = Random.Shared;
    private static readonly HttpClient _http = new();

    private static readonly string[] _brands = ["Toyota", "BMW", "Mercedes", "Audi", "Ford", "Honda", "Volkswagen", "Hyundai", "Kia", "Nissan"];
    private static readonly string[] _models = ["Sedan", "SUV", "Hatchback", "Coupe", "Crossover", "Pickup", "Minivan", "Wagon"];
    private static readonly string[] _bodyTypes = ["Sedan", "SUV", "Hatchback", "Coupe", "Crossover"];
    private static readonly string[] _fuelTypes = ["Petrol", "Diesel", "Electric", "Hybrid"];
    private static readonly string[] _colors = ["White", "Black", "Silver", "Blue", "Red", "Grey", "Green"];

    private static readonly JsonSerializerOptions _camelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Точка входа Cloud Function. Принимает HTTP-запрос от API Gateway,
    /// возвращает JSON транспортного средства с CORS-заголовками.
    /// </summary>
    /// <param name="input">Сериализованный запрос от API Gateway</param>
    public string FunctionHandler(string input)
    {
        FunctionRequest? req = null;
        try { req = JsonSerializer.Deserialize<FunctionRequest>(input ?? "{}"); } catch { }

        string? idRaw = null;
        req?.PathParams?.TryGetValue("id", out idRaw);
        if (string.IsNullOrEmpty(idRaw))
        {
            var url = req?.Url ?? req?.Path ?? "";
            idRaw = url.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries)
                       .LastOrDefault(p => !p.StartsWith('{'));
        }

        if (!int.TryParse(idRaw, out var id) || id <= 0)
            return Envelope(400, """{"error":"Identifier must be a positive number."}""");

        var cached = TryGetCachedAsync(id).GetAwaiter().GetResult();
        if (cached != null)
            return Envelope(200, cached);

        var vehicle = new
        {
            id,
            vin = GenerateVin(),
            brand = _brands[_rng.Next(_brands.Length)],
            model = _models[_rng.Next(_models.Length)],
            year = _rng.Next(2010, 2025),
            bodyType = _bodyTypes[_rng.Next(_bodyTypes.Length)],
            fuelType = _fuelTypes[_rng.Next(_fuelTypes.Length)],
            color = _colors[_rng.Next(_colors.Length)],
            mileage = Math.Round(_rng.NextDouble() * 200000, 1),
            lastServiceDate = DateTime.UtcNow.AddDays(-_rng.Next(30, 730)).ToString("yyyy-MM-dd")
        };

        var json = JsonSerializer.Serialize(vehicle, _camelCase);

        Task.Run(() => TryPublishAsync(json));

        return Envelope(200, json);
    }

    private static string Envelope(int status, string body) =>
        JsonSerializer.Serialize(new
        {
            statusCode = status,
            headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Access-Control-Allow-Origin"] = "*"
            },
            body
        });

    private static string GenerateVin()
    {
        const string chars = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";
        return string.Concat(Enumerable.Range(0, 17).Select(_ => chars[_rng.Next(chars.Length)]));
    }

    private static async Task<string?> TryGetCachedAsync(int id)
    {
        try
        {
            var bucket = Environment.GetEnvironmentVariable("S3_BUCKET") ?? "vehicle-data-store";
            var url = $"https://storage.yandexcloud.net/{bucket}/vehicle-{id}.json";
            var resp = await _http.GetAsync(url);
            if (resp.IsSuccessStatusCode)
                return await resp.Content.ReadAsStringAsync();
        }
        catch { }
        return null;
    }

    private static async Task TryPublishAsync(string messageBody)
    {
        try
        {
            var queueUrl = Environment.GetEnvironmentVariable("SQS_QUEUE_URL") ?? "";
            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";
            if (string.IsNullOrEmpty(queueUrl) || string.IsNullOrEmpty(accessKey)) return;

            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            var config = new AmazonSQSConfig
            {
                ServiceURL = "https://message-queue.api.cloud.yandex.net",
                AuthenticationRegion = "ru-central1"
            };
            using var sqs = new AmazonSQSClient(credentials, config);
            await sqs.SendMessageAsync(new SendMessageRequest { QueueUrl = queueUrl, MessageBody = messageBody });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] SQS publish failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Модель входящего запроса от Yandex API Gateway.
/// </summary>
public class FunctionRequest
{
    [JsonPropertyName("httpMethod")] public string HttpMethod { get; set; } = "";
    [JsonPropertyName("headers")] public Dictionary<string, string>? Headers { get; set; }
    [JsonPropertyName("path")] public string? Path { get; set; }
    [JsonPropertyName("queryStringParameters")] public Dictionary<string, string>? QueryStringParameters { get; set; }
    [JsonPropertyName("pathParameters")] public Dictionary<string, string>? PathParameters { get; set; }
    [JsonPropertyName("pathParams")] public Dictionary<string, string>? PathParams { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("body")] public string? Body { get; set; }
    [JsonPropertyName("isBase64Encoded")] public bool IsBase64Encoded { get; set; }
}
