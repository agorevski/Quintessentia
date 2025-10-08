# Azure Deployment Setup Guide

This guide provides step-by-step instructions for deploying the SpotifySummarizer application to Azure Web Apps with SQL Azure database and Blob Storage.

## Prerequisites

- Azure subscription with appropriate permissions
- Azure CLI installed (optional, for command-line deployment)
- SQL Server Management Studio or Azure Data Studio (optional, for database management)

## Azure Resources Required

### 1. Azure SQL Database

- **Server Name**: `quintessentia-server.database.windows.net`
- **Database Name**: `quintessentia-database`
- **Pricing Tier**: Basic or Standard (S0) recommended for development

### 2. Azure Storage Account

- **Account Name**: `quintessentia`
- **Type**: StorageV2 (general purpose v2)
- **Replication**: LRS (Locally Redundant Storage) recommended for development
- **Required Containers** (will be auto-created by the application):
  - `episodes` - Stores downloaded audio episodes
  - `transcripts` - Stores transcript text files
  - `summaries` - Stores summary text and audio files

### 3. Azure Web App

- **App Name**: `quintessentia-server` (or your preferred name)
- **Runtime**: .NET 9
- **Operating System**: Windows or Linux
- **Pricing Tier**: Basic B1 or higher recommended

## Setup Instructions

### Step 1: Create Azure SQL Database

1. **Navigate to [Azure Portal](https://portal.azure.com)**

2. **Create SQL Database**:
   - Click "Create a resource" → "Databases" → "SQL Database"
   - Fill in the details:
     - Subscription: Your subscription
     - Resource Group: Create new or select existing
     - Database Name: `quintessentia-database`
     - Server: Create new server
       - Server name: `quintessentia-server`
       - Location: Choose your region
       - Authentication: SQL authentication
       - Server admin login: Choose a username
       - Password: Choose a strong password
     - Compute + storage: Basic or Standard S0
   - Click "Review + Create" → "Create"

3. **Configure Firewall**:
   - Go to your SQL Server resource
   - Navigate to "Security" → "Networking"
   - Add your client IP address
   - Enable "Allow Azure services and resources to access this server"
   - Click "Save"

4. **Get Connection String**:

   ```txt
   Server=tcp:quintessentia-server.database.windows.net,1433;Initial Catalog=quintessentia-database;Persist Security Info=False;User ID={your_username};Password={your_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
   ```

### Step 2: Create Azure Storage Account

1. **Create Storage Account**:
   - Click "Create a resource" → "Storage" → "Storage account"
   - Fill in the details:
     - Resource Group: Same as database
     - Storage account name: `quintessentia`
     - Region: Same as database
     - Performance: Standard
     - Redundancy: LRS (Locally Redundant Storage)
   - Click "Review + Create" → "Create"

2. **Get Storage Connection String**:
   - Go to your Storage Account
   - Navigate to "Security + networking" → "Access keys"
   - Copy "Connection string" under key1 or key2
   - Format: `DefaultEndpointsProtocol=https;AccountName=quintessentia;AccountKey={your_key};EndpointSuffix=core.windows.net`

3. **Note**: Containers (`episodes`, `transcripts`, `summaries`) will be automatically created by the application on first run.

### Step 3: Create Azure Web App

1. **Create Web App**:
   - Click "Create a resource" → "Web" → "Web App"
   - Fill in the details:
     - Resource Group: Same as database
     - Name: `quintessentia-server` (or your preferred name)
     - Publish: Code
     - Runtime stack: .NET 9
     - Operating System: Windows (or Linux)
     - Region: Same as database
     - Pricing Plan: Basic B1 or higher
   - Click "Review + Create" → "Create"

### Step 4: Configure Application Settings

1. **Navigate to your Web App** in Azure Portal

2. **Go to "Settings" → "Configuration"**

3. **Add Connection String**:
   - Click "New connection string"
   - Name: `DefaultConnection`
   - Value: Your SQL Database connection string (from Step 1.4)
   - Type: SQLAzure
   - Click "OK"

4. **Add Application Settings**:
   - Click "New application setting" for each of the following:

   **Azure Storage Configuration**:
   - Name: `AzureStorage__ConnectionString`
   - Value: Your Storage Account connection string (from Step 2.2)

   - Name: `AzureStorage__Containers__Episodes`
   - Value: `episodes`

   - Name: `AzureStorage__Containers__Transcripts`
   - Value: `transcripts`

   - Name: `AzureStorage__Containers__Summaries`
   - Value: `summaries`

   **Azure OpenAI Configuration** (copy from your current appsettings.json):
   - Name: `AzureOpenAI__SpeechToText__Endpoint`
   - Value: Your endpoint URL

   - Name: `AzureOpenAI__SpeechToText__Key`
   - Value: Your API key

   - Name: `AzureOpenAI__SpeechToText__DeploymentName`
   - Value: Your deployment name

   - Name: `AzureOpenAI__GPT__Endpoint`
   - Value: Your endpoint URL

   - Name: `AzureOpenAI__GPT__Key`
   - Value: Your API key

   - Name: `AzureOpenAI__GPT__DeploymentName`
   - Value: Your deployment name

   - Name: `AzureOpenAI__TextToSpeech__Endpoint`
   - Value: Your endpoint URL

   - Name: `AzureOpenAI__TextToSpeech__Key`
   - Value: Your API key

   - Name: `AzureOpenAI__TextToSpeech__DeploymentName`
   - Value: Your deployment name

5. **Click "Save"** at the top

### Step 5: Update Local Configuration

Update your local `appsettings.json` or `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:quintessentia-server.database.windows.net,1433;Initial Catalog=quintessentia-database;Persist Security Info=False;User ID={your_username};Password={your_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=quintessentia;AccountKey={your_storage_account_key};EndpointSuffix=core.windows.net",
    "Containers": {
      "Episodes": "episodes",
      "Transcripts": "transcripts",
      "Summaries": "summaries"
    }
  }
}
```

### Step 6: Deploy Application

#### Option A: Deploy from Visual Studio

1. Right-click on the project in Solution Explorer
2. Select "Publish"
3. Choose "Azure" → "Azure App Service (Windows/Linux)"
4. Select your subscription and Web App
5. Click "Finish" → "Publish"

#### Option B: Deploy using Azure CLI

```bash
# Login to Azure
az login --tenant 8a106375-957e-4690-978b-a81220e49845

# Build the application
dotnet publish -c Release

# Create a zip file of the publish output
cd bin/Release/net9.0/publish
zip -r ../../../../../publish.zip .

# Deploy to Azure Web App
az webapp deployment source config-zip \
  --resource-group {your-resource-group} \
  --name quintessentia-server \
  --src publish.zip
```

#### Option C: Deploy using GitHub Actions

Create `.github/workflows/azure-deploy.yml`:

```yaml
name: Deploy to Azure Web App

on:
  push:
    branches:
      - main

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '9.0.x'
      
      - name: Build
        run: dotnet build --configuration Release
      
      - name: Publish
        run: dotnet publish -c Release -o ./publish
      
      - name: Deploy to Azure Web App
        uses: azure/webapps-deploy@v2
        with:
          app-name: 'quintessentia-server'
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          package: ./publish
```

### Step 7: Initialize Database

The database will be automatically initialized on first application start in Development mode. For Production:

1. **Option A**: Run migrations manually using command line:

   ```bash
   dotnet ef database update --connection "Your_Connection_String_Here"
   ```

2. **Option B**: Use the Azure Portal:
   - Navigate to your Web App
   - Go to "Development Tools" → "Console"
   - Run: `dotnet ef database update`

3. **Option C**: Let the application auto-migrate (already configured for Development environment in Program.cs)

### Step 8: Verify Deployment

1. **Navigate to your Web App URL**: `https://quintessentia-server.azurewebsites.net`

2. **Test the application**:
   - Upload an audio episode URL
   - Verify files are stored in Azure Blob Storage
   - Check that database records are created
   - Download episodes and summaries

3. **Monitor Application**:
   - Go to Web App → "Monitoring" → "Log stream"
   - Check for any errors or warnings

## Architecture Overview

### Data Flow

1. **User submits audio URL** → Controller receives request
2. **Controller generates cache key** from URL (SHA256 hash)
3. **Check database** for existing episode
4. **If not cached**:
   - Download MP3 from URL to temp directory
   - Upload to Blob Storage (`episodes` container)
   - Save metadata to SQL Database
5. **For summarization**:
   - Download episode from Blob Storage to temp
   - Transcribe with Azure OpenAI (Whisper)
   - Upload transcript to Blob Storage (`transcripts` container)
   - Summarize with GPT
   - Upload summary text to Blob Storage (`transcripts` container)
   - Generate speech with TTS
   - Upload summary audio to Blob Storage (`summaries` container)
   - Save summary metadata to database
   - Clean up temp files
6. **For downloads**:
   - Stream files directly from Blob Storage to user
   - No local file system needed

### Database Schema

**PodcastEpisodes Table**:

- Id (PK)
- CacheKey (Unique Index)
- OriginalUrl (Index)
- BlobPath
- DownloadDate
- FileSize

**PodcastSummaries Table**:

- Id (PK)
- EpisodeId (FK, Unique Index)
- TranscriptBlobPath
- SummaryTextBlobPath
- SummaryAudioBlobPath
- TranscriptWordCount
- SummaryWordCount
- ProcessedDate

## Troubleshooting

### Issue: "Azure Storage connection string is not configured"

- **Solution**: Verify `AzureStorage__ConnectionString` is set in Application Settings with correct format

### Issue: "Cannot open database"

- **Solution**:
  - Check firewall rules allow Azure services
  - Verify connection string is correct
  - Ensure database exists

### Issue: "Blob container not found"

- **Solution**: Containers are auto-created. Check storage account permissions and connection string

### Issue: Application timeout during processing

- **Solution**:
  - Increase Web App pricing tier for more resources
  - Adjust timeout settings in Azure Web App configuration
  - Consider using Azure Functions for long-running processes

### Issue: "Insufficient permissions" when accessing blobs

- **Solution**: Verify storage account access keys are correct and account has proper permissions

## Cost Optimization

### Development/Testing

- SQL Database: Basic tier (~$5/month)
- Storage Account: LRS, pay-as-you-go (~$0.02/GB/month)
- Web App: Basic B1 (~$13/month)
- **Estimated Total**: ~$20-25/month

### Production

- SQL Database: Standard S1 or higher
- Storage Account: GRS for redundancy
- Web App: Standard S1 or higher
- Consider Azure CDN for blob storage caching

## Security Best Practices

1. **Never commit secrets** to source control
2. **Use Azure Key Vault** for production secrets
3. **Enable HTTPS only** on Web App
4. **Restrict SQL firewall** to specific IPs when possible
5. **Use Managed Identity** instead of connection strings (advanced)
6. **Enable diagnostic logging** for monitoring
7. **Set blob access levels** to Private (already configured)
8. **Rotate access keys** regularly

## Monitoring and Logging

1. **Application Insights**: Enable for detailed telemetry
   - Go to Web App → "Monitoring" → "Application Insights"
   - Click "Turn on Application Insights"

2. **Log Analytics**: Query logs for troubleshooting
   - Access via Application Insights
   - Write KQL queries to analyze logs

3. **Alerts**: Set up alerts for failures
   - Create alerts for HTTP 5xx errors
   - Monitor database DTU usage
   - Track storage capacity

## Next Steps

1. Set up continuous deployment with GitHub Actions
2. Configure custom domain and SSL certificate
3. Enable Application Insights for monitoring
4. Implement Azure Key Vault for secrets management
5. Set up staging slots for zero-downtime deployments
6. Configure auto-scaling rules based on load

## Support

For issues or questions:

- Check Azure Portal for error logs
- Review Application Insights telemetry
- Consult Azure [documentation](https://docs.microsoft.com/azure)
