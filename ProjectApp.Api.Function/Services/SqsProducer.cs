using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace ApiFunction.Services;

internal sealed class SqsProducer(string queueUrl, string accessKey, string secretKey)
{
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
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            var config = new AmazonSQSConfig
            {
                ServiceURL = "https://message-queue.api.cloud.yandex.net",
                AuthenticationRegion = "ru-central1"
            };
            using var sqs = new AmazonSQSClient(credentials, config);
            await sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = messageBody
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] SQS publish failed: {ex.Message}");
        }
    }
}
