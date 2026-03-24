var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache")
    .WithRedisCommander();

var api = builder.AddProject<Projects.ProjectApp_Api>("projectapp-api")
    .WithReference(redis)
    .WaitFor(redis)
    .WithEndpoint("http", e => e.Port = 5179);

builder.AddProject<Projects.Client_Wasm>("client")
    .WaitFor(api);

builder.Build().Run();
