using System;

namespace AquariumHealthMonitor
{
    public class TankMetricsPayload
    {
        public double? phValue { get; set; }
        public double? tempCelsius { get; set; }
        public double? ammoniaPPM { get; set; }
        public int? fishCount { get; set; }

        public ValidationOutcome ValidateCompleteness()
        {
            var missingFields = new System.Collections.Generic.List<string>();

            if (!phValue.HasValue || phValue.Value <= 0)
                missingFields.Add("phValue");
            
            if (!tempCelsius.HasValue || tempCelsius.Value <= 0)
                missingFields.Add("tempCelsius");
            
            if (!ammoniaPPM.HasValue || ammoniaPPM.Value < 0)
                missingFields.Add("ammoniaPPM");
            
            if (!fishCount.HasValue || fishCount.Value < 0)
                missingFields.Add("fishCount");

            return new ValidationOutcome
            {
                IsValid = missingFields.Count == 0,
                MissingOrInvalidFields = missingFields
            };
        }
    }

    public class ValidationOutcome
    {
        public bool IsValid { get; set; }
        public System.Collections.Generic.List<string> MissingOrInvalidFields { get; set; }
    }

    public class AnalysisThresholds
    {
        public double MinAcceptablePH { get; set; } = 6.5;
        public double MaxAcceptablePH { get; set; } = 8.5;
        public double MinSafeTemperature { get; set; } = 22.0;
        public double MaxSafeTemperature { get; set; } = 28.0;
        public double AmmoniaDangerLevel { get; set; } = 0.5;
        public int OvercrowdingThreshold { get; set; } = 50;
    }
}
