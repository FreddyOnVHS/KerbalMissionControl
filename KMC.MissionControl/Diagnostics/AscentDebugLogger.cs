using System;
using System.IO;
using KMC.MissionControl.Guidance;

namespace KMC.MissionControl.Diagnostics
{
    /// <summary>
    /// Owns ascent CSV file creation, headers, escaping, synchronization,
    /// and append operations.
    ///
    /// Diagnostics are deliberately fail-safe and never interrupt the
    /// mission display.
    /// </summary>
    public sealed class AscentDebugLogger
    {
        private static readonly object SyncRoot =
            new object();

        private const string Header =
            "MET,Stage,InitialStage,AltitudeM," +
            "DownrangeM,LiveTWR,PlanningTWR," +
            "ProfileScaleM,TargetAltitudeM," +
            "TargetPitchDeg,ActualPitchDeg," +
            "ApoapsisM,BurnTimeRemainingS," +
            "PredictedBurnoutVelocityMps," +
            "PredictedApoapsisM," +
            "PredictionTargetErrorM," +
            "PredictionConfidencePercent," +
            "PredictionStatus," +
            "PlannerNominalPitchDeg," +
            "PlannerRecommendedPitchDeg," +
            "PlannerPitchCorrectionDeg," +
            "PlannerRecoveryAuthorityPercent," +
            "PlannerTargetAchievable," +
            "PlannerFlightPhase," +
            "PlannerThrottleCommandPercent," +
            "PlannerCutoffRequired," +
            "PlannerCoastLockoutActive," +
            "PlannerCommand," +
            "PlannerThrottleCommand," +
            "PlannerStatus," +
            "PlannerNextEvent," +
            "CircularizationAvailable," +
            "CircularizationDeltaV," +
            "CircularizationBurnTimeS," +
            "CircularizationIgnitionInS," +
            "CircularizationPeriapsisErrorM," +
            "CircularizationPitchDeg," +
            "MecoCountdownSeconds," +
            "FlashAlert," +
            "PredictedShutdownApoapsisM," +
            "PredictedShutdownPeriapsisM," +
            "PredictedOrbitErrorM," +
            "OrbitalEnergyError," +
            "GuidancePhase," +
            "OrbitSafetyReason," +
            "OrbitAchieved," +
            "PauseBurn," +
            "ActualApoapsisM," +
            "ActualPeriapsisM," +
            "PeriapsisSafe," +
            "PredictedPeriapsisSafe," +
            "RemainingDeltaV," +
            "EnergySatisfied," +
            "DeltaVSatisfied," +
            "SafetyDecisionTime," +
            "PoweredGuidanceAvailable," +
            "PoweredGuidancePitchDeg," +
            "PoweredPredictedApoapsisM," +
            "PoweredPredictedPeriapsisM," +
            "PoweredOrbitErrorM," +
            "PoweredGuidanceConfidencePercent," +
            "PoweredGuidanceBurnSeconds," +
            "PoweredGuidanceCoastSeconds," +
            "PoweredPredictionConvergenceM," +
            "PoweredTargetCutoffReached," +
            "PoweredGuidanceMode," +
            "PeriapsisRecoveryActive," +
            "PeriapsisRecoveryErrorM," +
            "PeriapsisRecoveryThrottlePercent," +
            "PeriapsisRecoveryDesiredThrottlePercent," +
            "PeriapsisRecoveryCommandAgeS," +
            "PeriapsisRecoveryCommandHeld," +
            "PeriapsisRecoveryCutoff," +
            "PeriapsisRecoveryReason";

        public void Write(
            AscentDebugRecord record)
        {
            if (record == null)
            {
                return;
            }

            try
            {
                string directory =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder
                                .LocalApplicationData),
                        "KMC");

                Directory.CreateDirectory(
                    directory);

                string path =
                    Path.Combine(
                        directory,
                        "ascent-debug.csv");

                lock (SyncRoot)
                {
                    bool writeHeader =
                        !File.Exists(path);

                    using (StreamWriter writer =
                        new StreamWriter(
                            path,
                            true))
                    {
                        if (writeHeader)
                        {
                            writer.WriteLine(
                                Header);
                        }

                        writer.WriteLine(
                            BuildRow(
                                record));
                    }
                }
            }
            catch
            {
                /*
                 * Diagnostics must never interrupt the mission display.
                 */
            }
        }

        private static string BuildRow(
            AscentDebugRecord record)
        {
            MissionPlannerResult plan =
                record.MissionPlan ??
                new MissionPlannerResult();

            return string.Join(
                ",",
                record.MissionTimeSeconds
                    .ToString("0.000"),
                record.Stage,
                record.InitialStage,
                record.AltitudeMeters
                    .ToString("0.000"),
                record.DownrangeMeters
                    .ToString("0.000"),
                record.LiveThrustToWeightRatio
                    .ToString("0.000"),
                IsFinite(
                    record.PlanningThrustToWeightRatio)
                    ? record.PlanningThrustToWeightRatio
                        .ToString("0.000")
                    : string.Empty,
                record.ProfileScaleMeters
                    .ToString("0.000"),
                record.TargetAltitudeMeters
                    .ToString("0.000"),
                record.TargetPitchDegrees
                    .ToString("0.000"),
                record.ActualPitchDegrees
                    .ToString("0.000"),
                record.ApoapsisMeters
                    .ToString("0.000"),
                record.PredictionAvailable
                    ? record.BurnTimeRemainingSeconds
                        .ToString("0.000")
                    : string.Empty,
                record.PredictionAvailable
                    ? record
                        .PredictedBurnoutVelocityMetersPerSecond
                        .ToString("0.000")
                    : string.Empty,
                record.PredictionAvailable
                    ? record.PredictedApoapsisMeters
                        .ToString("0.000")
                    : string.Empty,
                record.PredictionAvailable
                    ? record.PredictionTargetErrorMeters
                        .ToString("0.000")
                    : string.Empty,
                record.PredictionAvailable
                    ? record.PredictionConfidencePercent
                        .ToString("0.000")
                    : string.Empty,
                EscapeCsvField(
                    record.PredictionStatus),
                plan.NominalPitchDegrees
                    .ToString("0.000"),
                plan.RecommendedPitchDegrees
                    .ToString("0.000"),
                plan.PitchCorrectionDegrees
                    .ToString("0.000"),
                plan.RecoveryAuthorityPercent
                    .ToString("0.000"),
                plan.IsTargetAchievable
                    ? "1"
                    : "0",
                EscapeCsvField(
                    plan.FlightPhase),
                plan.ThrottleCommandPercent
                    .ToString("0.000"),
                plan.CutoffRequired
                    ? "1"
                    : "0",
                plan.CoastLockoutActive
                    ? "1"
                    : "0",
                EscapeCsvField(
                    plan.Command),
                EscapeCsvField(
                    plan.ThrottleCommand),
                EscapeCsvField(
                    plan.Status),
                EscapeCsvField(
                    plan.NextEvent),
                plan.CircularizationAvailable
                    ? "1"
                    : "0",
                plan.CircularizationDeltaV
                    .ToString("0.000"),
                plan.CircularizationBurnTimeSeconds
                    .ToString("0.000"),
                plan.CircularizationIgnitionInSeconds
                    .ToString("0.000"),
                plan.CircularizationPeriapsisErrorMeters
                    .ToString("0.000"),
                plan.CircularizationPitchDegrees
                    .ToString("0.000"),
                plan.MecoCountdownSeconds,
                plan.FlashAlert
                    ? "1"
                    : "0",
                plan.PredictedShutdownApoapsisMeters
                    .ToString("0.000"),
                plan.PredictedShutdownPeriapsisMeters
                    .ToString("0.000"),
                plan.PredictedOrbitErrorMeters
                    .ToString("0.000"),
                plan.OrbitalEnergyError
                    .ToString("0.000"),
                EscapeCsvField(
                    plan.FlightPhase),
                EscapeCsvField(
                    plan.OrbitSafetyReason),
                plan.OrbitSafetyAchieved ? "1" : "0",
                plan.OrbitSafetyPauseBurn ? "1" : "0",
                record.ActualApoapsisMeters
                    .ToString("0.000"),
                record.ActualPeriapsisMeters
                    .ToString("0.000"),
                plan.ActualPeriapsisSafe ? "1" : "0",
                plan.PredictedPeriapsisSafe ? "1" : "0",
                plan.CircularizationDeltaV
                    .ToString("0.000"),
                plan.OrbitEnergySatisfied ? "1" : "0",
                plan.OrbitDeltaVSatisfied ? "1" : "0",
                plan.OrbitSafetyDecisionTime
                    .ToString("0.000"),
                plan.PoweredGuidanceAvailable
                    ? "1"
                    : "0",
                plan.PoweredGuidancePitchDegrees
                    .ToString("0.000"),
                plan.PoweredPredictedApoapsisMeters
                    .ToString("0.000"),
                plan.PoweredPredictedPeriapsisMeters
                    .ToString("0.000"),
                plan.PoweredOrbitErrorMeters
                    .ToString("0.000"),
                plan.PoweredGuidanceConfidencePercent
                    .ToString("0.000"),
                plan.PoweredGuidanceBurnSeconds
                    .ToString("0.000"),
                plan.PoweredGuidanceCoastSeconds
                    .ToString("0.000"),
                plan.PoweredPredictionConvergenceMeters
                    .ToString("0.000"),
                plan.PoweredTargetCutoffReached
                    ? "1"
                    : "0",
                EscapeCsvField(
                    plan.PoweredGuidanceMode),
                plan.PeriapsisRecoveryActive
                    ? "1"
                    : "0",
                plan.PeriapsisRecoveryErrorMeters
                    .ToString("0.000"),
                plan.PeriapsisRecoveryThrottlePercent
                    .ToString("0.000"),
                plan.PeriapsisRecoveryDesiredThrottlePercent
                    .ToString("0.000"),
                plan.PeriapsisRecoveryCommandAgeSeconds
                    .ToString("0.000"),
                plan.PeriapsisRecoveryCommandHeld
                    ? "1"
                    : "0",
                plan.PeriapsisRecoveryCutoff
                    ? "1"
                    : "0",
                EscapeCsvField(
                    plan.PeriapsisRecoveryReason));
        }

        private static string EscapeCsvField(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return string.Empty;
            }

            bool requiresQuotes =
                value.IndexOf(',') >= 0 ||
                value.IndexOf('"') >= 0 ||
                value.IndexOf('\r') >= 0 ||
                value.IndexOf('\n') >= 0;

            if (!requiresQuotes)
            {
                return value;
            }

            return
                "\"" +
                value.Replace(
                    "\"",
                    "\"\"") +
                "\"";
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
