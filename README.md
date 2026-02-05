# AzFunctionAppInsights

## Aquarium Health Monitoring Function Demo

This demo showcases an Azure Function written in C# that leverages ILogger for comprehensive logging with Application Insights integration.

### Features

- **ILogger Integration**: Uses `LogInformation()` for standard logging and `LogWarning()` for alerts
- **Application Insights**: All logs are persisted to Azure Application Insights
- **HTTP Trigger**: POST endpoint for receiving aquarium water quality metrics
- **ARM Templates**: Complete infrastructure-as-code for Azure deployment

### Project Structure

```
AquariumHealthMonitor/          # C# Azure Function project
├── WaterQualityProcessor.cs    # Main function with ILogger examples
├── AquariumHealthMonitor.csproj
├── host.json                    # App Insights configuration
└── local.settings.json          # Local development settings

AzureDeployConfigs/              # ARM deployment templates
├── aquarium-resources.json      # Main ARM template
└── deploy-parameters.json       # Deployment parameters
```

### Function Behavior

The `ProcessAquariumMetrics` function:
1. Receives aquarium water quality data via HTTP POST
2. Logs informational messages using `LogInformation()`
3. Triggers warnings using `LogWarning()` when metrics exceed safe thresholds
4. Returns analysis results with health status

**Example Request:**
```bash
curl -X POST "https://your-function-app.azurewebsites.net/api/aquarium/analyze?tankId=tank-001" \
  -H "Content-Type: application/json" \
  -d '{
    "phValue": 7.2,
    "tempCelsius": 25.5,
    "ammoniaPPM": 0.3,
    "fishCount": 35
  }'
```

**Logging Examples:**
- `LogInformation`: Records successful metrics processing and tank readings
- `LogWarning`: Alerts for pH out of range (6.5-8.5), temperature issues (22-28°C), or elevated ammonia (>0.5ppm)

### Local Development

1. **Prerequisites:**
   - .NET 8.0 SDK
   - Azure Functions Core Tools
   - Azure Storage Emulator or Azurite

2. **Run Locally:**
   ```bash
   cd AquariumHealthMonitor
   dotnet restore
   func start
   ```

3. **Test the Function:**
   ```bash
   curl -X POST "http://localhost:7071/api/aquarium/analyze?tankId=test-tank" \
     -H "Content-Type: application/json" \
     -d '{"phValue": 9.5, "tempCelsius": 30, "ammoniaPPM": 0.8, "fishCount": 60}'
   ```

### Azure Deployment

1. **Deploy Infrastructure:**
   ```bash
   az group create --name aquarium-monitor-rg --location eastus
   
   az deployment group create \
     --resource-group aquarium-monitor-rg \
     --template-file AzureDeployConfigs/aquarium-resources.json \
     --parameters AzureDeployConfigs/deploy-parameters.json
   ```

2. **Publish Function Code:**
   ```bash
   cd AquariumHealthMonitor
   dotnet publish -c Release
   
   func azure functionapp publish aquarium-health-monitor
   ```

3. **View Logs in Application Insights:**
   - Navigate to Azure Portal → Application Insights resource
   - Go to "Logs" section
   - Query examples:
     ```kusto
     traces
     | where message contains "Tank"
     | project timestamp, message, severityLevel
     | order by timestamp desc
     
     traces
     | where severityLevel == 2  // Warnings
     | project timestamp, message
     ```

### Application Insights Configuration

The `host.json` file configures Application Insights with:
- Sampling enabled for cost optimization
- Live Metrics for real-time monitoring
- Information-level logging enabled

All `LogInformation()` and `LogWarning()` calls are automatically sent to Application Insights.

### ARM Template Resources

The ARM template deploys:
1. **Storage Account**: Required for Azure Functions runtime
2. **Application Insights**: Telemetry and logging destination
3. **App Service Plan**: Consumption (serverless) plan
4. **Function App**: The Azure Functions host with App Insights connection

### Customization

**Adjust Thresholds:** Edit constants in `WaterQualityProcessor.cs`:
```csharp
private const double MIN_SAFE_PH = 6.5;
private const double MAX_SAFE_PH = 8.5;
private const double AMMONIA_THRESHOLD_PPM = 0.5;
```

**Change Logging Level:** Modify `host.json`:
```json
"logLevel": {
  "default": "Information"  // Options: Trace, Debug, Information, Warning, Error, Critical
}
```