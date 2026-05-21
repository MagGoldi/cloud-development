using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApiFunction.Models;

namespace ApiFunction;

/// <summary>
/// Бизнес-логика генерации и публикации транспортного средства.
/// </summary>
internal static class VehicleProcessor
{
    private static readonly Random Rng = Random.Shared;
    private static readonly HttpClient Http = new();

    private static readonly string[] Brands = ["Toyota", "BMW", "Mercedes", "Audi", "Ford", "Honda", "Volkswagen", "Hyundai", "Kia", "Nissan"];
    private static readonly string[] Models = ["Sedan", "SUV", "Hatchback", "Coupe", "Crossover", "Pickup", "Minivan", "Wagon"];
    private static readonly string[] BodyTypes = ["Sedan", "SUV", "Hatchback", "Coupe", "Crossover"];
    private static readonly string[] FuelTypes = ["Petrol", "Diesel", "Electric", "Hybrid"];
    private static readonly string[] Colors = ["White", "Black", "Silver", "Blue", "Red", "Grey", "Green"];

    public static async Task<FunctionResponse> ProcessAsync(int id)
    {
        var vehicle = GenerateVehicle(id);
        var json = JsonSerializer.Serialize(vehicle, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        _ = TryPublishToQueueAsync(json);

        return new FunctionResponse
        {
            StatusCode = 200,
            Headers = new()
            {
                ["Content-Type"] = "application/json",
                ["Access-Control-Allow-Origin"] = "*"
            },
            Body = json
        };
    }

    private static object GenerateVehicle(int id) => new
    {
        id,
        vin = GenerateVin(),
        brand = Brands[Rng.Next(Brands.Length)],
        model = Models[Rng.Next(Models.Length)],
        year = Rng.Next(2010, 2025),
        bodyType = BodyTypes[Rng.Next(BodyTypes.Length)],
        fuelType = FuelTypes[Rng.Next(FuelTypes.Length)],
        color = Colors[Rng.Next(Colors.Length)],
        mileage = Math.Round(Rng.NextDouble() * 200000, 1),
        lastServiceDate = DateTime.UtcNow.AddDays(-Rng.Next(30, 730)).ToString("yyyy-MM-dd")
    };

    private static string GenerateVin()
    {
        const string chars = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";
        return string.Concat(Enumerable.Range(0, 17).Select(_ => chars[Rng.Next(chars.Length)]));
    }

    private static async Task TryPublishToQueueAsync(string messageBody)
    {
        try
        {
            var queueUrl = Environment.GetEnvironmentVariable("SQS_QUEUE_URL") ?? "";
            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";
            if (string.IsNullOrEmpty(queueUrl) || string.IsNullOrEmpty(accessKey)) return;

            var uri = new Uri(queueUrl);
            var endpoint = $"{uri.Scheme}://{uri.Host}";
            var path = uri.AbsolutePath;

            var body = $"Action=SendMessage&MessageBody={Uri.EscapeDataString(messageBody)}&Version=2012-11-05";
            var now = DateTime.UtcNow;
            var dateStamp = now.ToString("yyyyMMdd");
            var amzDate = now.ToString("yyyyMMddTHHmmssZ");

            var payloadHash = Sha256Hash(body);
            var headers = $"content-type:application/x-www-form-urlencoded\nhost:{uri.Host}\nx-amz-date:{amzDate}\n";
            var signedHeaders = "content-type;host;x-amz-date";
            var canonicalRequest = $"POST\n{path}\n\n{headers}\n{signedHeaders}\n{payloadHash}";

            var region = "ru-central1";
            var service = "sqs";
            var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
            var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{Sha256Hash(canonicalRequest)}";

            var signingKey = GetSigningKey(secretKey, dateStamp, region, service);
            var signature = HmacSha256Hex(signingKey, stringToSign);

            var authHeader = $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

            var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}{path}")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            request.Headers.TryAddWithoutValidation("Authorization", authHeader);
            request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);

            var resp = await Http.SendAsync(request);
            if (!resp.IsSuccessStatusCode)
                Console.WriteLine($"[WARN] SQS returned {resp.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] SQS publish failed: {ex.Message}");
        }
    }

    private static string Sha256Hash(string data)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static byte[] HmacSha256(byte[] key, string data)
        => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static string HmacSha256Hex(byte[] key, string data)
        => Convert.ToHexString(HmacSha256(key, data)).ToLowerInvariant();

    private static byte[] GetSigningKey(string secret, string date, string region, string service)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + secret);
        var kDate = HmacSha256(kSecret, date);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        return HmacSha256(kService, "aws4_request");
    }
}
