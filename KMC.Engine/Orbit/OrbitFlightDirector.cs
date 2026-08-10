using System;

namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Engine-owned ORBIT flight director.
    ///
    /// Advisory only. It does not command throttle, attitude, staging, or SAS.
    ///
    /// Attitude guidance deliberately does not invent pitch/yaw targets.
    /// ORBIT attitude is expressed as orbital-prograde guidance and the
    /// verified vessel-frame orbital velocity vector is carried with the
    /// result for later FDAI/display use.
    /// </summary>
    internal sealed class OrbitFlightDirector
    {
        private const double CircularizationReadyLeadSeconds =
            8.0;

        private const double KerbinAtmosphereTopMeters =
            70000.0;

        private const double OrbitNominalToleranceMeters =
            3000.0;

        public void Reset()
        {
            /*
             * Stateless in Build 10.4.
             *
             * Kept as an explicit reset contract so later ORBIT guidance can
             * add state without changing OrbitFoundationSystem ownership.
             */
        }

        public OrbitFlightDirectorModel Calculate(
            OrbitTelemetryState current,
            CircularizationPredictionModel prediction,
            OrbitSafetyModel safety,
            PeriapsisRecoveryModel recovery,
            VelocityVectorTelemetryModel velocityVector,
            bool ascentHandoffObserved)
        {
            OrbitFlightDirectorModel result =
                CreateDefaultResult(
                    current,
                    prediction,
                    safety,
                    velocityVector);

            if (current == null ||
                !current.Available)
            {
                return result;
            }

            PopulatePrograde(
                result,
                velocityVector);

            PopulateOrbitEvidence(
                result,
                current,
                prediction);

            if (!ascentHandoffObserved)
            {
                result.FlightPhase =
                    "ORBIT WAITING";

                result.Command =
                    "AWAIT ORBIT HANDOFF";

                result.AttitudeCommand =
                    "HOLD ATTITUDE";

                result.ThrottleCommandPercent =
                    0.0;

                result.ThrottleCommand =
                    "THROTTLE 0%";

                result.CoastLockoutActive =
                    true;

                result.Status =
                    "ASCENT OWNS GUIDANCE";

                result.NextEvent =
                    "ASCENT HANDOFF";

                result.DecisionSource =
                    "ASCENT HANDOFF";

                return result;
            }

            result.Available =
                true;

            if (safety != null &&
                (safety.CutoffLatched ||
                 safety.OrbitAchieved))
            {
                ConfigureOrbitAchieved(
                    result,
                    safety);

                return result;
            }

            if (recovery != null &&
                recovery.Available &&
                recovery.Active)
            {
                ConfigurePeriapsisRecovery(
                    result,
                    recovery);

                return result;
            }

            /*
             * Build 10.6 - safe-orbit reacquisition.
             *
             * A quick-load/reconnect can place KMC into a vessel that is
             * already safely orbiting while the stateful circularization
             * latch is intentionally reset. In that case the mathematical
             * circularization predictor can still produce a future ignition
             * time, but that is not an operational burn command.
             *
             * Only use this path before circularization has started. Active
             * burn/recovery/safety states retain their established priority.
             */
            bool circularizationStarted =
                safety != null &&
                safety.CircularizationStarted;

            if (!circularizationStarted &&
                IsSafeOrbit(
                    current))
            {
                double targetOrbitMeters =
                    safety != null
                        ? safety.TargetOrbitMeters
                        : double.NaN;

                if (IsNominalOrbit(
                        current,
                        targetOrbitMeters))
                {
                    ConfigureReacquiredNominalOrbit(
                        result,
                        current);

                    return result;
                }

                ConfigureReacquiredSafeOrbit(
                    result,
                    current);

                return result;
            }

            if (prediction == null ||
                !prediction.Available)
            {
                result.FlightPhase =
                    "ORBIT WAITING";

                result.Command =
                    "HOLD ORBITAL PROGRADE";

                result.AttitudeCommand =
                    GetProgradeAttitudeCommand(
                        result);

                result.ThrottleCommandPercent =
                    0.0;

                result.ThrottleCommand =
                    "THROTTLE 0%";

                result.CoastLockoutActive =
                    true;

                result.Status =
                    "BURN SOLUTION WAIT";

                result.NextEvent =
                    "CIRCULARIZATION SOLUTION";

                result.DecisionSource =
                    "PREDICTION UNAVAILABLE";

                return result;
            }

            result.CircularizationStarted =
                circularizationStarted;

            if (circularizationStarted)
            {
                ConfigureCircularizationBurn(
                    result,
                    prediction,
                    safety);

                return result;
            }

            if (prediction.IgnitionInSeconds >
                CircularizationReadyLeadSeconds)
            {
                ConfigureCoast(
                    result,
                    prediction);

                return result;
            }

            ConfigureCircularizationReady(
                result,
                prediction);

            return result;
        }

        private static OrbitFlightDirectorModel CreateDefaultResult(
            OrbitTelemetryState current,
            CircularizationPredictionModel prediction,
            OrbitSafetyModel safety,
            VelocityVectorTelemetryModel velocityVector)
        {
            OrbitFlightDirectorModel result =
                new OrbitFlightDirectorModel();

            if (current != null)
            {
                result.ActualApoapsisMeters =
                    current.ApoapsisMeters;

                result.ActualPeriapsisMeters =
                    current.PeriapsisMeters;
            }

            if (prediction != null)
            {
                result.IgnitionInSeconds =
                    prediction.IgnitionInSeconds;

                result.BurnTimeSeconds =
                    prediction.BurnTimeSeconds;

                result.RemainingDeltaVMetersPerSecond =
                    prediction.RemainingDeltaVMetersPerSecond;

                result.BurnCompletionPercent =
                    prediction.BurnCompletionPercent;

                result.PredictedApoapsisMeters =
                    prediction.PredictedApoapsisMeters;

                result.PredictedPeriapsisMeters =
                    prediction.PredictedPeriapsisMeters;
            }

            if (safety != null)
            {
                result.CircularizationStarted =
                    safety.CircularizationStarted;

                result.OrbitAchieved =
                    safety.OrbitAchieved;

                result.CutoffRequired =
                    safety.CutoffRequired;
            }

            PopulatePrograde(
                result,
                velocityVector);

            return result;
        }

        private static bool IsSafeOrbit(
            OrbitTelemetryState current)
        {
            if (current == null ||
                !current.Available ||
                !IsFinite(
                    current.ApoapsisMeters) ||
                !IsFinite(
                    current.PeriapsisMeters))
            {
                return false;
            }

            return
                current.ApoapsisMeters >=
                    KerbinAtmosphereTopMeters &&
                current.PeriapsisMeters >=
                    KerbinAtmosphereTopMeters;
        }

        private static bool IsNominalOrbit(
            OrbitTelemetryState current,
            double targetOrbitMeters)
        {
            if (!IsSafeOrbit(
                    current) ||
                !IsFinite(
                    targetOrbitMeters) ||
                targetOrbitMeters <=
                    KerbinAtmosphereTopMeters)
            {
                return false;
            }

            return
                Math.Abs(
                    current.ApoapsisMeters -
                    targetOrbitMeters) <=
                    OrbitNominalToleranceMeters &&
                Math.Abs(
                    current.PeriapsisMeters -
                    targetOrbitMeters) <=
                    OrbitNominalToleranceMeters;
        }

        private static void ConfigureReacquiredNominalOrbit(
            OrbitFlightDirectorModel result,
            OrbitTelemetryState current)
        {
            bool producingThrust =
                IsProducingThrust(
                    current);

            result.Available =
                true;

            result.FlightPhase =
                "ORBIT ACHIEVED";

            result.OrbitAchieved =
                true;

            result.CutoffRequired =
                producingThrust;

            result.CoastLockoutActive =
                true;

            result.IgnitionDue =
                false;

            result.CircularizationStarted =
                false;

            result.PeriapsisRecoveryActive =
                false;

            result.AttitudeCommand =
                GetProgradeAttitudeCommand(
                    result);

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.Command =
                producingThrust
                    ? "CUTOFF - HOLD ORBITAL PROGRADE"
                    : "HOLD ORBITAL PROGRADE";

            result.Status =
                "NOMINAL ORBIT REACQUIRED";

            result.NextEvent =
                "VERIFY ORBIT";

            result.DecisionSource =
                "ORBIT REACQUISITION";
        }

        private static void ConfigureReacquiredSafeOrbit(
            OrbitFlightDirectorModel result,
            OrbitTelemetryState current)
        {
            bool producingThrust =
                IsProducingThrust(
                    current);

            result.Available =
                true;

            result.FlightPhase =
                "SAFE ORBIT";

            result.OrbitAchieved =
                false;

            result.CutoffRequired =
                producingThrust;

            result.CoastLockoutActive =
                true;

            result.IgnitionDue =
                false;

            result.CircularizationStarted =
                false;

            result.PeriapsisRecoveryActive =
                false;

            result.AttitudeCommand =
                GetProgradeAttitudeCommand(
                    result);

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.Command =
                producingThrust
                    ? "CUTOFF - HOLD ORBITAL PROGRADE"
                    : "HOLD ORBITAL PROGRADE";

            result.Status =
                "OFF-NOMINAL / NO BURN COMMANDED";

            result.NextEvent =
                "MANEUVER PLAN REQUIRED";

            result.DecisionSource =
                "ORBIT REACQUISITION";
        }

        private static bool IsProducingThrust(
            OrbitTelemetryState current)
        {
            return
                current != null &&
                (current.CurrentThrustKilonewtons >
                    0.1 ||
                 current.Throttle >
                    0.01);
        }

        private static void ConfigureCoast(
            OrbitFlightDirectorModel result,
            CircularizationPredictionModel prediction)
        {
            result.FlightPhase =
                "COAST TO APOAPSIS";

            result.Command =
                "HOLD ORBITAL PROGRADE";

            result.AttitudeCommand =
                GetProgradeAttitudeCommand(
                    result);

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.CoastLockoutActive =
                true;

            result.IgnitionDue =
                false;

            result.Status =
                "COAST PREDICTION";

            result.NextEvent =
                "IGNITION T-" +
                FormatCountdown(
                    prediction.IgnitionInSeconds);

            result.DecisionSource =
                "CIRCULARIZATION PREDICTION";
        }

        private static void ConfigureCircularizationReady(
            OrbitFlightDirectorModel result,
            CircularizationPredictionModel prediction)
        {
            bool ignitionDue =
                prediction.IgnitionInSeconds <=
                    0.0;

            result.FlightPhase =
                "CIRCULARIZATION READY";

            result.AttitudeCommand =
                GetProgradeAttitudeCommand(
                    result);

            result.IgnitionDue =
                ignitionDue;

            result.CoastLockoutActive =
                !ignitionDue;

            if (ignitionDue)
            {
                result.Command =
                    "IGNITE - HOLD ORBITAL PROGRADE";

                result.ThrottleCommandPercent =
                    ClampPercent(
                        prediction
                            .RecommendedThrottleFraction *
                        100.0);

                result.ThrottleCommand =
                    FormatThrottle(
                        result.ThrottleCommandPercent);

                result.Status =
                    "IGNITION DUE";

                result.NextEvent =
                    "CIRCULARIZATION BURN";
            }
            else
            {
                result.Command =
                    "HOLD ORBITAL PROGRADE - PREPARE IGNITION";

                result.ThrottleCommandPercent =
                    0.0;

                result.ThrottleCommand =
                    "THROTTLE 0%";

                result.Status =
                    "CIRCULARIZATION READY";

                result.NextEvent =
                    "IGNITION T-" +
                    FormatCountdown(
                        prediction.IgnitionInSeconds);
            }

            result.DecisionSource =
                "CIRCULARIZATION PREDICTION";
        }

        private static void ConfigureCircularizationBurn(
            OrbitFlightDirectorModel result,
            CircularizationPredictionModel prediction,
            OrbitSafetyModel safety)
        {
            result.FlightPhase =
                "CIRCULARIZATION BURN";

            result.Command =
                "HOLD ORBITAL PROGRADE";

            result.AttitudeCommand =
                GetProgradeAttitudeCommand(
                    result);

            result.IgnitionDue =
                true;

            result.CircularizationStarted =
                true;

            result.CoastLockoutActive =
                false;

            result.ThrottleCommandPercent =
                ClampPercent(
                    prediction
                        .RecommendedThrottleFraction *
                    100.0);

            result.ThrottleCommand =
                FormatThrottle(
                    result.ThrottleCommandPercent);

            result.Status =
                prediction.Status;

            result.NextEvent =
                "DV " +
                prediction
                    .RemainingDeltaVMetersPerSecond
                    .ToString("0.0") +
                " M/S";

            result.DecisionSource =
                "CIRCULARIZATION PREDICTION";

            /*
             * Safety always outranks the nominal prediction.
             */
            if (safety != null &&
                safety.CutoffRequired)
            {
                result.CutoffRequired =
                    true;

                result.ThrottleCommandPercent =
                    0.0;

                result.ThrottleCommand =
                    "THROTTLE 0%";

                result.Command =
                    "CUTOFF - HOLD ORBITAL PROGRADE";

                result.Status =
                    safety.Reason;

                result.NextEvent =
                    "VERIFY ORBIT";

                result.DecisionSource =
                    "ORBIT SAFETY";
            }
        }

        private static void ConfigurePeriapsisRecovery(
            OrbitFlightDirectorModel result,
            PeriapsisRecoveryModel recovery)
        {
            result.FlightPhase =
                "PERIAPSIS RECOVERY";

            result.PeriapsisRecoveryActive =
                true;

            result.AttitudeCommand =
                GetProgradeAttitudeCommand(
                    result);

            result.ThrottleCommandPercent =
                ClampPercent(
                    recovery.ThrottlePercent);

            result.ThrottleCommand =
                FormatThrottle(
                    result.ThrottleCommandPercent);

            result.CutoffRequired =
                recovery.CutoffRequired;

            if (recovery.CutoffRequired)
            {
                result.Command =
                    "CUTOFF - HOLD ORBITAL PROGRADE";

                result.Status =
                    recovery.Reason;

                result.NextEvent =
                    "VERIFY PERIAPSIS";
            }
            else if (recovery.ProducingThrust)
            {
                result.Command =
                    "HOLD ORBITAL PROGRADE";

                result.Status =
                    recovery.Reason;

                result.NextEvent =
                    "RAISE PE / FOLLOW THROTTLE";
            }
            else
            {
                result.Command =
                    "IGNITE - HOLD ORBITAL PROGRADE";

                result.Status =
                    "PERIAPSIS RECOVERY";

                result.NextEvent =
                    "RAISE PE / FOLLOW THROTTLE";
            }

            result.DecisionSource =
                "PERIAPSIS RECOVERY";
        }

        private static void ConfigureOrbitAchieved(
            OrbitFlightDirectorModel result,
            OrbitSafetyModel safety)
        {
            result.FlightPhase =
                "ORBIT ACHIEVED";

            result.OrbitAchieved =
                true;

            result.CutoffRequired =
                true;

            result.CoastLockoutActive =
                true;

            result.AttitudeCommand =
                GetProgradeAttitudeCommand(
                    result);

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.Command =
                "CUTOFF - HOLD ORBITAL PROGRADE";

            result.Status =
                safety != null &&
                !string.IsNullOrEmpty(
                    safety.Reason)
                    ? safety.Reason
                    : "ORBIT ACHIEVED";

            result.NextEvent =
                "VERIFY ORBIT";

            result.DecisionSource =
                "ORBIT SAFETY";
        }

        private static void PopulateOrbitEvidence(
            OrbitFlightDirectorModel result,
            OrbitTelemetryState current,
            CircularizationPredictionModel prediction)
        {
            if (current != null)
            {
                result.ActualApoapsisMeters =
                    current.ApoapsisMeters;

                result.ActualPeriapsisMeters =
                    current.PeriapsisMeters;
            }

            if (prediction == null)
            {
                return;
            }

            result.IgnitionInSeconds =
                prediction.IgnitionInSeconds;

            result.BurnTimeSeconds =
                prediction.BurnTimeSeconds;

            result.RemainingDeltaVMetersPerSecond =
                prediction.RemainingDeltaVMetersPerSecond;

            result.BurnCompletionPercent =
                prediction.BurnCompletionPercent;

            result.PredictedApoapsisMeters =
                prediction.PredictedApoapsisMeters;

            result.PredictedPeriapsisMeters =
                prediction.PredictedPeriapsisMeters;
        }

        private static void PopulatePrograde(
            OrbitFlightDirectorModel result,
            VelocityVectorTelemetryModel vector)
        {
            if (result == null ||
                vector == null ||
                !vector.Available ||
                !vector.Fresh ||
                !vector.VesselMatchesFlightPacket ||
                !vector.OrbitalSpeedAgreement ||
                !IsFinite(
                    vector.OrbitalMagnitudeMetersPerSecond) ||
                vector.OrbitalMagnitudeMetersPerSecond <
                    1.0)
            {
                return;
            }

            result.ProgradeAvailable =
                true;

            result.OrbitalProgradeRightMetersPerSecond =
                vector.OrbitalRightMetersPerSecond;

            result.OrbitalProgradeNoseMetersPerSecond =
                vector.OrbitalNoseMetersPerSecond;

            result.OrbitalProgradeReferenceForwardMetersPerSecond =
                vector.OrbitalReferenceForwardMetersPerSecond;

            result.OrbitalProgradeMagnitudeMetersPerSecond =
                vector.OrbitalMagnitudeMetersPerSecond;
        }

        private static string GetProgradeAttitudeCommand(
            OrbitFlightDirectorModel result)
        {
            return
                result != null &&
                result.ProgradeAvailable
                    ? "HOLD ORBITAL PROGRADE"
                    : "POINT ORBITAL PROGRADE";
        }

        private static string FormatCountdown(
            double seconds)
        {
            if (!IsFinite(seconds))
            {
                return "--";
            }

            if (seconds <= 0.0)
            {
                return "0.0S";
            }

            return
                seconds.ToString("0.0") +
                "S";
        }

        private static string FormatThrottle(
            double percent)
        {
            return
                "THROTTLE " +
                ClampPercent(percent)
                    .ToString("0") +
                "%";
        }

        private static double ClampPercent(
            double value)
        {
            if (!IsFinite(value))
            {
                return 0.0;
            }

            return
                Math.Max(
                    0.0,
                    Math.Min(
                        100.0,
                        value));
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
