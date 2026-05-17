using Amazon.SQS;
using LocalStack.Client.Extensions;
using Minio;
using ProjectApp.FileService.Services;
using ProjectApp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddLocalStack(builder.Configuration);
builder.Services.AddAwsService<IAmazonSQS>();

builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();

    // When run via Aspire, WithReference(minio) sets ConnectionStrings:minio = "http://host:port"
    // For local run without Aspire, fall back to Minio:Endpoint setting
    string minioEndpoint;
    var connectionString = configuration.GetConnectionString("minio");
    if (connectionString is not null)
    {
        var uri = new Uri(connectionString);
        minioEndpoint = $"{uri.Host}:{uri.Port}";
    }
    else
    {
        minioEndpoint = configuration["Minio:Endpoint"] ?? "localhost:9000";
    }

    return new MinioClient()
        .WithEndpoint(minioEndpoint)
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
