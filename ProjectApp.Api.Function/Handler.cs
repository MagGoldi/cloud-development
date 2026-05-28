using System.Text.Json;
using System.Text.Json.Serialization;
using ApiFunction.Services;

namespace ApiFunction;

public class Handler
{
    private static readonly VehicleFaker Faker = new();
    private static readonly HttpClient Http = new();

    public async Task<string> FunctionHandler(string input)
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

        var cached = await TryGetCachedAsync(id);
        if (cached != null)
            return Envelope(200, cached);

        var json = Faker.Generate(id);

        var producer = SqsProducer.TryCreate();
        if (producer != null)
            await producer.PublishAsync(json);

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

    private static async Task<string?> TryGetCachedAsync(int id)
    {
        try
        {
            var bucket = Environment.GetEnvironmentVariable("S3_BUCKET") ?? "vehicle-data-store";
            var url = $"https://storage.yandexcloud.net/{bucket}/vehicle-{id}.json";
            var resp = await Http.GetAsync(url);
            if (resp.IsSuccessStatusCode)
                return await resp.Content.ReadAsStringAsync();
        }
        catch { }
        return null;
    }
}

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
