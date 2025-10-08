using Quintessentia.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// Add Azure Blob Storage Service as Singleton
builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

// Add Blob Metadata Service
builder.Services.AddSingleton<IBlobMetadataService, BlobMetadataService>();

// Add application services
builder.Services.AddScoped<IAudioService, AudioService>();
builder.Services.AddScoped<IAzureOpenAIService, AzureOpenAIService>();

var app = builder.Build();

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
