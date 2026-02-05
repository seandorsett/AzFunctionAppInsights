using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AquariumHealthMonitor
{
    public static class WaterQualityProcessor
    {
        private static readonly AnalysisThresholds Thresholds = new AnalysisThresholds();

        [FunctionName("ProcessAquariumMetrics")]
        public static async Task<IActionResult> ExecuteWaterQualityAnalysis(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "aquarium/analyze")] HttpRequest incomingRequest,
            ILogger metricsLogger)
        {
            string tankIdentifier = incomingRequest.Query["tankId"];
            
            metricsLogger.LogInformation($"Beginning water quality analysis for aquarium tank: {tankIdentifier ?? "unspecified"}");

            string requestBody = await new StreamReader(incomingRequest.Body).ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                metricsLogger.LogWarning($"Empty metrics received for tank: {tankIdentifier}");
                return new BadRequestObjectResult("Metrics payload is required");
            }

            try
            {
                var metricsPayload = JsonConvert.DeserializeObject<TankMetricsPayload>(requestBody);
                
                if (metricsPayload == null)
                {
                    metricsLogger.LogWarning($"Could not deserialize metrics for tank: {tankIdentifier}");
                    return new BadRequestObjectResult("Invalid JSON structure");
                }

                var validationResult = metricsPayload.ValidateCompleteness();
                if (!validationResult.IsValid)
                {
                    string missingFieldsList = string.Join(", ", validationResult.MissingOrInvalidFields);
                    metricsLogger.LogWarning($"Tank {tankIdentifier} metrics incomplete or invalid. Missing/invalid fields: {missingFieldsList}");
                    return new BadRequestObjectResult($"Required fields missing or invalid: {missingFieldsList}");
                }

                double phReading = metricsPayload.phValue.Value;
                double temperatureReading = metricsPayload.tempCelsius.Value;
                double ammoniaMeasurement = metricsPayload.ammoniaPPM.Value;
                int populationCount = metricsPayload.fishCount.Value;

                metricsLogger.LogInformation($"Tank {tankIdentifier} readings - pH: {phReading}, Temperature: {temperatureReading}°C, Ammonia: {ammoniaMeasurement}ppm, Fish: {populationCount}");

                bool concernsDetected = false;

                if (phReading < Thresholds.MinAcceptablePH || phReading > Thresholds.MaxAcceptablePH)
                {
                    metricsLogger.LogWarning($"CRITICAL: Tank {tankIdentifier} pH level out of safe range: {phReading} (safe range: {Thresholds.MinAcceptablePH}-{Thresholds.MaxAcceptablePH})");
                    concernsDetected = true;
                }

                if (temperatureReading < Thresholds.MinSafeTemperature || temperatureReading > Thresholds.MaxSafeTemperature)
                {
                    metricsLogger.LogWarning($"CRITICAL: Tank {tankIdentifier} temperature unsafe: {temperatureReading}°C (safe range: {Thresholds.MinSafeTemperature}-{Thresholds.MaxSafeTemperature}°C)");
                    concernsDetected = true;
                }

                if (ammoniaMeasurement > Thresholds.AmmoniaDangerLevel)
                {
                    metricsLogger.LogWarning($"ALERT: Tank {tankIdentifier} ammonia levels elevated at {ammoniaMeasurement}ppm (threshold: {Thresholds.AmmoniaDangerLevel}ppm)");
                    concernsDetected = true;
                }

                if (populationCount > Thresholds.OvercrowdingThreshold)
                {
                    metricsLogger.LogInformation($"Tank {tankIdentifier} has high population density with {populationCount} fish (threshold: {Thresholds.OvercrowdingThreshold})");
                }

                var analysisResult = new
                {
                    status = "analysis_complete",
                    tankId = tankIdentifier,
                    evaluationTimestamp = DateTime.UtcNow,
                    healthStatus = concernsDetected ? "requires_attention" : "optimal",
                    warningsDetected = concernsDetected,
                    metricsProcessed = new
                    {
                        ph = phReading,
                        temperature = temperatureReading,
                        ammonia = ammoniaMeasurement,
                        population = populationCount
                    }
                };

                return new OkObjectResult(analysisResult);
            }
            catch (JsonException parsingError)
            {
                metricsLogger.LogWarning($"Unable to parse metrics JSON for tank {tankIdentifier}: {parsingError.Message}");
                return new BadRequestObjectResult("Malformed metrics data");
            }
            catch (Exception generalError)
            {
                metricsLogger.LogError(generalError, $"Analysis failed for tank {tankIdentifier}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
