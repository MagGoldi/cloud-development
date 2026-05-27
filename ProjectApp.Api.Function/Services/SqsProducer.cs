using System.Security.Cryptography;
using System.Text;

namespace ApiFunction.Services;

internal sealed class SqsProducer
{
    private static readonly HttpClient Http = new();

    private readonly string _queueUrl;
    private readonly string _accessKey;
    private readonly string _secretKey;

    public SqsProducer(string queueUrl, string accessKey, string secretKey)
    {
        _queueUrl = queueUrl;
        _accessKey = accessKey;
        _secretKey = secretKey;
    }

    public static SqsProducer? TryCreate()
    {
        var queueUrl = Environment.GetEnvironmentVariable("SQS_QUEUE_URL") ?? "";
        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";

        return string.IsNullOrEmpty(queueUrl) || string.IsNullOrEmpty(accessKey)
            ? null
            : new SqsProducer(queueUrl, accessKey, secretKey);
    }

    public async Task PublishAsync(string messageBody)
    {
        try
        {
            var uri = new Uri(_queueUrl);
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

            var signingKey = GetSigningKey(_secretKey, dateStamp, region, service);
            var signature = HmacSha256Hex(signingKey, stringToSign);

            var authHeader = $"AWS4-HMAC-SHA256 Credential={_accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

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
