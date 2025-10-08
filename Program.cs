using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Quintessentia.Data;
using Quintessentia.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure Key Vault (optional - only if credentials are available)
try
{
    var keyVaultName = builder.Configuration["KeyVault:Name"] ?? "quintessentia";
    var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
    
    // Only attempt to add Key Vault if we're in Azure with managed identity
    // or if running locally with appropriate credentials
    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ExcludeVisualStudioCredential = false,
        ExcludeVisualStudioCodeCredential = false,
        ExcludeAzureCliCredential = false,
        ExcludeEnvironmentCredential = false,
        ExcludeManagedIdentityCredential = false
    });
    
    builder.Configuration.AddAzureKeyVault(keyVaultUri, credential);
    builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Information);
    Console.WriteLine($"Successfully connected to Azure Key Vault: {keyVaultName}");
}
catch (Exception ex)
{
    // Key Vault is optional - log the error but continue startup
    Console.WriteLine($"Warning: Could not connect to Azure Key Vault: {ex.Message}");
    Console.WriteLine("Continuing with configuration from appsettings.json and environment variables...");
    builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);
}

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Add DbContext with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Azure Blob Storage Service as Singleton
builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

// Add application services
builder.Services.AddScoped<IAudioService, AudioService>();
builder.Services.AddScoped<IAzureOpenAIService, AzureOpenAIService>();

var app = builder.Build();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        // Check if database exists and is accessible
        if (dbContext.Database.CanConnect())
        {
            app.Logger.LogInformation("Database connection successful");
            
            // Apply pending migrations
            var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();
            if (pendingMigrations.Any())
            {
                app.Logger.LogInformation($"Applying {pendingMigrations.Count} pending migrations...");
                dbContext.Database.Migrate();
                app.Logger.LogInformation("Database migrations applied successfully");
            }
            else
            {
                app.Logger.LogInformation("Database is up to date, no migrations needed");
            }
        }
        else
        {
            app.Logger.LogWarning("Cannot connect to database. Migrations will be skipped.");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error during database initialization. The application will start but database operations may fail.");
        // Don't crash the app - let it start and show errors when database is actually needed
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
