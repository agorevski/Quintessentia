using Quintessentia.Controllers;
using Quintessentia.Middleware;
using Quintessentia.Services;
using Quintessentia.Services.Contracts;
using Quintessentia.Services.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register focused controllers for DI (used by AudioController facade)
builder.Services.AddScoped<ProcessingController>();
builder.Services.AddScoped<DownloadController>();
builder.Services.AddScoped<StreamController>();
builder.Services.AddScoped<ResultController>();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// Register shared JSON serializer options
builder.Services.AddSingleton<JsonOptionsService>();

// Register URL validator for SSRF protection
builder.Services.AddSingleton<IUrlValidator, UrlValidator>();

// Use mock storage services in Development, Azure services in other environments
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IStorageService, LocalFileStorageService>();
    builder.Services.AddSingleton<IMetadataService, LocalFileMetadataService>();
}
else
{
    builder.Services.AddSingleton<IStorageService, AzureBlobStorageService>();
    builder.Services.AddSingleton<IMetadataService, AzureBlobMetadataService>();
}

// Add application services
builder.Services.AddScoped<IAudioService, AudioService>();

// Add new refactored services
builder.Services.AddSingleton<ICacheKeyService, CacheKeyService>();
builder.Services.AddSingleton<IStorageConfiguration, StorageConfiguration>();
builder.Services.AddScoped<IEpisodeQueryService, EpisodeQueryService>();
builder.Services.AddScoped<IProcessingProgressService, ProcessingProgressService>();

// Use mock Azure OpenAI service in Development, real service in other environments
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IAzureOpenAIService, MockAzureOpenAIService>();
}
else
{
    builder.Services.AddScoped<IAzureOpenAIService, AzureOpenAIService>();
}

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<AzureBlobStorageHealthCheck>("azure-blob-storage", tags: ["azure", "storage"])
    .AddCheck<AzureOpenAIHealthCheck>("azure-openai", tags: ["azure", "ai"]);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Only use HTTPS redirection in development
// Azure Web Apps handles SSL termination at the load balancer
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();

// Add correlation ID middleware for request tracing
app.UseCorrelationId();

app.UseAuthorization();

// Health check endpoint for Azure Web Apps with comprehensive monitoring
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data
            })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
