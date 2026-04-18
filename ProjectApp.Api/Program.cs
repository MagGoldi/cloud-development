using Amazon.SQS;
using ProjectApp.Api.Services.SqsPublisher;
using ProjectApp.Api.Services.VehicleGeneratorService;
using ProjectApp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("cache");

builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var sqsConfig = new AmazonSQSConfig
    {
        ServiceURL = configuration["Sqs:ServiceUrl"] ?? "http://localhost:9324"
    };
    return new AmazonSQSClient("test", "test", sqsConfig);
});

builder.Services.AddSingleton<ISqsPublisher, SqsPublisher>();

builder.Services.AddSingleton<VehicleFaker>();
builder.Services.AddScoped<VehicleGeneratorService>();
builder.Services.AddScoped<IVehicleGeneratorService>(sp =>
    ActivatorUtilities.CreateInstance<CachedVehicleGeneratorService>(sp,
        sp.GetRequiredService<VehicleGeneratorService>()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Vehicle Generator API"
    });

    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    var domainXmlPath = Path.Combine(AppContext.BaseDirectory, "ProjectApp.Domain.xml");
    if (File.Exists(domainXmlPath))
    {
        options.IncludeXmlComments(domainXmlPath);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;