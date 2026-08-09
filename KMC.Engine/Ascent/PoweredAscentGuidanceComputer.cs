using System;
using KMC.Engine.Models;

namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Engine-owned bounded predictive ascent guidance computer.
    ///
    /// Build 9.3 preserves the current MissionControl guidance behavior while
    /// preferring verified Engine-owned PROP live thrust when its evidence is
    /// fresh and complete.
    ///
    /// Advisory only. No vehicle control is performed.
    /// </summary>
    internal sealed class PoweredAscentGuidanceComputer
    {
        private readonly GuidanceOptimizer _optimizer =
            new GuidanceOptimizer();

        private readonly AscentEnergyManager _energyManager =
            new AscentEnergyManager();

        private double _previousPredictionApoapsis =
            double.NaN;

        private double _previousPredictionTime =
            double.NaN;

        public void Reset()
        {
            _previousPredictionApoapsis =
                double.NaN;

            _previousPredictionTime =
                double.NaN;
        }

        public PoweredAscentModel Calculate(
            AscentTelemetryState telemetry,
            PropulsionModel propulsion,
            double referencePitchDegrees,
            double targetApoapsisMeters)
        {
            PoweredAscentThrustInput thrustInput =
                SelectThrustInput(
                    telemetry,
                    propulsion);

            PoweredAscentModel result =
                CreateBaseResult(
                    telemetry,
                    thrustInput,
                    referencePitchDegrees);

            if (telemetry == null ||
                !telemetry.Available)
            {
                result.InactiveReason =
                    "NO FLIGHT TELEMETRY";

                return result;
            }

            if (telemetry.AltitudeMeters <
                12000.0)
            {
                result.InactiveReason =
                    "BELOW GUIDANCE ALTITUDE";

                return result;
            }

            if (!thrustInput.CurrentThrustKnown ||
                thrustInput.CurrentThrustKilonewtons <=
                    0.1)
            {
                result.InactiveReason =
                    "NO CURRENT THRUST";

                return result;
            }

            if (telemetry.ApoapsisMeters >=
                targetApoapsisMeters +
                3000.0)
            {
                result.InactiveReason =
                    "APOAPSIS ABOVE GUIDANCE BAND";

                return result;
            }

            AscentTrajectoryPrediction best =
                _optimizer.FindBestPitch(
                    telemetry,
                    thrustInput,
                    referencePitchDegrees,
                    targetApoapsisMeters);

            if (best == null ||
                !best.IsValid)
            {
                result.Mode =
                    "NO SOLUTION";

                result.InactiveReason =
                    "NO VALID TRAJECTORY";

                return result;
            }

            double energyError =
                _energyManager
                    .CalculateTargetEnergyError(
                        telemetry,
                        targetApoapsisMeters);

            double convergence =
                CalculateConvergence(
                    telemetry.MissionTimeSeconds,
                    best.ApoapsisMeters);

            double confidence =
                CalculateConfidence(
                    telemetry,
                    best,
                    energyError,
                    convergence);

            result.Available =
                true;

            result.Mode =
                best.TargetCutoffReached
                    ? "TARGET CUTOFF"
                    : "ADAPTIVE HORIZON";

            result.InactiveReason =
                string.Empty;

            result.RecommendedPitchDegrees =
                best.PitchDegrees;

            result.PitchErrorDegrees =
                telemetry.PitchDegrees -
                best.PitchDegrees;

            result.PredictedApoapsisMeters =
                best.ApoapsisMeters;

            result.PredictedPeriapsisMeters =
                best.PeriapsisMeters;

            result.OrbitErrorMeters =
                best.ApoapsisMeters -
                targetApoapsisMeters;

            result.ConfidencePercent =
                confidence;

            result.PoweredFlightSeconds =
                best.PoweredFlightSeconds;

            result.CoastFlightSeconds =
                best.CoastFlightSeconds;

            result.PredictionConvergenceKnown =
                IsFinite(
                    convergence);

            result.PredictionConvergenceMeters =
                IsFinite(
                    convergence)
                    ? convergence
                    : 0.0;

            result.TargetCutoffReached =
                best.TargetCutoffReached;

            _previousPredictionApoapsis =
                best.ApoapsisMeters;

            _previousPredictionTime =
                telemetry.MissionTimeSeconds;

            return result;
        }

        private static PoweredAscentModel CreateBaseResult(
            AscentTelemetryState telemetry,
            PoweredAscentThrustInput thrustInput,
            double referencePitchDegrees)
        {
            PoweredAscentModel result =
                new PoweredAscentModel
                {
                    Mode =
                        "INACTIVE",

                    ReferencePitchDegrees =
                        referencePitchDegrees,

                    RecommendedPitchDegrees =
                        referencePitchDegrees,

                    ThrustEvidence =
                        thrustInput.Evidence,

                    PropulsionTelemetryFresh =
                        thrustInput.PropulsionTelemetryFresh,

                    PropulsionCoverageComplete =
                        thrustInput.PropulsionCoverageComplete,

                    CurrentThrustKnown =
                        thrustInput.CurrentThrustKnown,

                    CurrentThrustKilonewtons =
                        thrustInput.CurrentThrustKilonewtons,

                    AvailableThrustKnown =
                        thrustInput.AvailableThrustKnown,

                    AvailableThrustKilonewtons =
                        thrustInput.AvailableThrustKilonewtons,

                    ThrottleCommand =
                        thrustInput.ThrottleCommand
                };

            if (telemetry != null)
            {
                result.VesselMassTonnes =
                    telemetry.VesselMassTonnes;

                result.SpecificImpulseSeconds =
                    IsFinite(
                        telemetry.AverageSpecificImpulseSeconds) &&
                    telemetry.AverageSpecificImpulseSeconds >
                        1.0
                        ? telemetry.AverageSpecificImpulseSeconds
                        : 300.0;

                result.PitchErrorDegrees =
                    telemetry.PitchDegrees -
                    referencePitchDegrees;
            }

            return result;
        }

        private static PoweredAscentThrustInput SelectThrustInput(
            AscentTelemetryState telemetry,
            PropulsionModel propulsion)
        {
            PoweredAscentThrustInput result =
                new PoweredAscentThrustInput
                {
                    Evidence =
                        AscentPoweredThrustEvidence.Unknown,

                    ThrottleCommand =
                        telemetry != null
                            ? Math.Max(
                                0.0,
                                telemetry.ThrottleCommand)
                            : 0.0
                };

            if (propulsion != null &&
                propulsion.IsAvailable &&
                propulsion.Live != null)
            {
                result.PropulsionTelemetryFresh =
                    propulsion.Live.TelemetryFresh;

                result.PropulsionCoverageComplete =
                    propulsion.Live.CoverageComplete;

                if (propulsion.Live.TelemetryAvailable &&
                    propulsion.Live.TelemetryFresh &&
                    propulsion.Live.CoverageComplete &&
                    propulsion.Live.CurrentThrustKnown &&
                    propulsion.Live.AvailableThrustKnown)
                {
                    result.Evidence =
                        AscentPoweredThrustEvidence
                            .VerifiedPropulsionLiveState;

                    result.CurrentThrustKnown =
                        true;

                    result.CurrentThrustKilonewtons =
                        Math.Max(
                            0.0,
                            propulsion.Live.CurrentThrust);

                    result.AvailableThrustKnown =
                        true;

                    result.AvailableThrustKilonewtons =
                        Math.Max(
                            0.0,
                            propulsion.Live.AvailableThrust);

                    return result;
                }
            }

            if (telemetry != null &&
                IsFinite(
                    telemetry.CurrentThrustKilonewtons) &&
                IsFinite(
                    telemetry.MaximumThrustKilonewtons))
            {
                result.Evidence =
                    AscentPoweredThrustEvidence
                        .FlightPacketFallback;

                result.CurrentThrustKnown =
                    true;

                result.CurrentThrustKilonewtons =
                    Math.Max(
                        0.0,
                        telemetry.CurrentThrustKilonewtons);

                result.AvailableThrustKnown =
                    true;

                result.AvailableThrustKilonewtons =
                    Math.Max(
                        0.0,
                        telemetry.MaximumThrustKilonewtons);
            }

            return result;
        }

        private double CalculateConvergence(
            double missionTime,
            double predictedApoapsis)
        {
            if (!IsFinite(
                    _previousPredictionApoapsis) ||
                !IsFinite(
                    _previousPredictionTime) ||
                missionTime <=
                    _previousPredictionTime)
            {
                return double.NaN;
            }

            return Math.Abs(
                predictedApoapsis -
                _previousPredictionApoapsis);
        }

        private static double CalculateConfidence(
            AscentTelemetryState telemetry,
            AscentTrajectoryPrediction prediction,
            double energyError,
            double convergence)
        {
            double confidence =
                88.0;

            if (!prediction.TargetCutoffReached)
            {
                confidence -=
                    25.0;
            }

            if (telemetry.DynamicPressureKpa >
                35.0)
            {
                confidence -=
                    18.0;
            }
            else if (telemetry.DynamicPressureKpa >
                     20.0)
            {
                confidence -=
                    8.0;
            }

            if (IsFinite(
                    convergence))
            {
                if (convergence >
                    10000.0)
                {
                    confidence -=
                        30.0;
                }
                else if (convergence >
                         5000.0)
                {
                    confidence -=
                        20.0;
                }
                else if (convergence >
                         2000.0)
                {
                    confidence -=
                        10.0;
                }
                else if (convergence <
                         500.0)
                {
                    confidence +=
                        5.0;
                }
            }

            if (Math.Abs(
                    energyError) >
                1000000.0)
            {
                confidence -=
                    8.0;
            }

            if (prediction.PoweredFlightSeconds >
                50.0)
            {
                confidence -=
                    8.0;
            }

            return Clamp(
                confidence,
                0.0,
                100.0);
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return Math.Max(
                minimum,
                Math.Min(
                    maximum,
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
