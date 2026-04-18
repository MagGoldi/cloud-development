using Amazon.SQS;
using Minio;
using ProjectApp.FileService.Services;
using ProjectApp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var sqsConfig = new AmazonSQSConfig
    {
        ServiceURL = configuration["Sqs:ServiceUrl"] ?? "http://localhost:9324"
    };
    return new AmazonSQSClient("test", "test", sqsConfig);
});

builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new MinioClient()
        .WithEndpoint(configuration["Minio:Endpoint"] ?? "localhost:9000")
        .WithCredentials(
            configuration["Minio:AccessKey"] ?? "minioadmin",
            configuration["Minio:SecretKey"] ?? "minioadmin")
        .WithSSL(false)
        .Build();
});

builder.Services.AddSingleton<MinioStorageService>();
builder.Services.AddHostedService<SqsConsumerService>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
