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
        private const double MIN_SAFE_PH = 6.5;
        private const double MAX_SAFE_PH = 8.5;
        private const double MIN_TEMP_CELSIUS = 22.0;
        private const double MAX_TEMP_CELSIUS = 28.0;
        private const double AMMONIA_THRESHOLD_PPM = 0.5;
        private const int HIGH_POPULATION_THRESHOLD = 50;

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
                dynamic metricsPayload = JsonConvert.DeserializeObject(requestBody);
                
                double phLevel = metricsPayload?.phValue ?? 0.0;
                double temperatureCelsius = metricsPayload?.tempCelsius ?? 0.0;
                double ammoniaPartsPerMillion = metricsPayload?.ammoniaPPM ?? 0.0;
                int fishPopulation = metricsPayload?.fishCount ?? 0;

                metricsLogger.LogInformation($"Tank {tankIdentifier} readings - pH: {phLevel}, Temperature: {temperatureCelsius}°C, Ammonia: {ammoniaPartsPerMillion}ppm, Fish: {fishPopulation}");

                bool hasWarnings = false;

                if (phLevel < MIN_SAFE_PH || phLevel > MAX_SAFE_PH)
                {
                    metricsLogger.LogWarning($"CRITICAL: Tank {tankIdentifier} pH level out of safe range: {phLevel} (safe range: {MIN_SAFE_PH}-{MAX_SAFE_PH})");
                    hasWarnings = true;
                }

                if (temperatureCelsius < MIN_TEMP_CELSIUS || temperatureCelsius > MAX_TEMP_CELSIUS)
                {
                    metricsLogger.LogWarning($"CRITICAL: Tank {tankIdentifier} temperature unsafe: {temperatureCelsius}°C (safe range: {MIN_TEMP_CELSIUS}-{MAX_TEMP_CELSIUS}°C)");
                    hasWarnings = true;
                }

                if (ammoniaPartsPerMillion > AMMONIA_THRESHOLD_PPM)
                {
                    metricsLogger.LogWarning($"ALERT: Tank {tankIdentifier} ammonia levels elevated at {ammoniaPartsPerMillion}ppm (threshold: {AMMONIA_THRESHOLD_PPM}ppm)");
                    hasWarnings = true;
                }

                if (fishPopulation > HIGH_POPULATION_THRESHOLD)
                {
                    metricsLogger.LogInformation($"Tank {tankIdentifier} has high population density with {fishPopulation} fish (threshold: {HIGH_POPULATION_THRESHOLD})");
                }

                var analysisResult = new
                {
                    status = "analysis_complete",
                    tankId = tankIdentifier,
                    evaluationTimestamp = DateTime.UtcNow,
                    healthStatus = hasWarnings ? "requires_attention" : "optimal",
                    warningsDetected = hasWarnings,
                    metricsProcessed = new
                    {
                        ph = phLevel,
                        temperature = temperatureCelsius,
                        ammonia = ammoniaPartsPerMillion,
                        population = fishPopulation
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
