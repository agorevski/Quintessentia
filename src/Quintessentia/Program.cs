using Quintessentia.Services.Contracts;
using Quintessentia.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

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

// Use mock Azure OpenAI service in Development, real service in other environments
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IAzureOpenAIService, MockAzureOpenAIService>();
}
else
{
    builder.Services.AddScoped<IAzureOpenAIService, AzureOpenAIService>();
}

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

app.UseAuthorization();

// Health check endpoint for Azure Web Apps
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
