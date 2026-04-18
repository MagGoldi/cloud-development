var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache")
    .WithRedisCommander();

var minio = builder.AddContainer("minio", "minio/minio")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "minio-api")
    .WithHttpEndpoint(port: 9001, targetPort: 9001, name: "minio-console")
    .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin");

var sqs = builder.AddContainer("sqs", "softwaremill/elasticmq-native")
    .WithHttpEndpoint(port: 9324, targetPort: 9324, name: "sqs");

var fileService = builder.AddProject<Projects.ProjectApp_FileService>("projectapp-fileservice")
    .WaitFor(minio)
    .WaitFor(sqs);

var gateway = builder.AddProject<Projects.ProjectApp_Gateway>("projectapp-gateway")
    .WithEndpoint("http", e => e.Port = 5200);

for (var i = 0; i < 3; i++)
{
    var api = builder.AddProject<Projects.ProjectApp_Api>($"projectapp-api-{i + 1}")
        .WithReference(redis)
        .WaitFor(redis)
        .WaitFor(sqs)
        .WithEndpoint("http", e => e.Port = 5180 + i)
        .WithEndpoint("https", e => e.Port = 7170 + i);

    gateway = gateway.WithReference(api).WaitFor(api);
}

builder.AddProject<Projects.Client_Wasm>("client")
    .WaitFor(gateway);

builder.Build().Run();
