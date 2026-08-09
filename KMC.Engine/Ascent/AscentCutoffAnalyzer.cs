using System;

namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Tracks apoapsis rise rate and estimates time to the ascent cutoff
    /// threshold.
    ///
    /// Build 9.4 preserves the current MissionPlanner smoothing/threshold
    /// behavior:
    /// - valid trend sample dt: 0.10 .. 2.0 s
    /// - valid instantaneous rise: 0 .. 50000 m/s
    /// - smoothing: 72% previous / 28% new
    /// - MECO threshold: target apoapsis - 250 m
    /// </summary>
    internal sealed class AscentCutoffAnalyzer
    {
        private const double AscentCutoffToleranceMeters =
            250.0;

        private double _lastApoapsisMeters =
            double.NaN;

        private double _lastApoapsisSampleTime =
            double.NaN;

        private double _smoothedApoapsisRateMetersPerSecond =
            double.NaN;

        public void Reset()
        {
            _lastApoapsisMeters =
                double.NaN;

            _lastApoapsisSampleTime =
                double.NaN;

            _smoothedApoapsisRateMetersPerSecond =
                double.NaN;
        }

        public AscentCutoffModel Update(
            AscentTelemetryState telemetry,
            double targetApoapsisMeters)
        {
            AscentCutoffModel result =
                new AscentCutoffModel
                {
                    Available =
                        telemetry != null &&
                        telemetry.Available,

                    TargetApoapsisMeters =
                        targetApoapsisMeters,

                    CutoffToleranceMeters =
                        AscentCutoffToleranceMeters,

                    CutoffThresholdMeters =
                        targetApoapsisMeters -
                        AscentCutoffToleranceMeters
                };

            if (telemetry == null ||
                !telemetry.Available)
            {
                return result;
            }

            UpdateApoapsisTrend(
                telemetry);

            if (IsFinite(
                    _smoothedApoapsisRateMetersPerSecond) &&
                _smoothedApoapsisRateMetersPerSecond >=
                    1.0)
            {
                result.ApoapsisRiseRateAvailable =
                    true;

                result.ApoapsisRiseRateMetersPerSecond =
                    _smoothedApoapsisRateMetersPerSecond;

                double remainingMeters =
                    targetApoapsisMeters -
                    AscentCutoffToleranceMeters -
                    telemetry.ApoapsisMeters;

                result.EstimatedMecoAvailable =
                    true;

                result.EstimatedMecoSeconds =
                    remainingMeters <= 0.0
                        ? 0.0
                        : remainingMeters /
                          _smoothedApoapsisRateMetersPerSecond;
            }

            result.CutoffReached =
                telemetry.ApoapsisMeters >=
                result.CutoffThresholdMeters;

            return result;
        }

        private void UpdateApoapsisTrend(
            AscentTelemetryState telemetry)
        {
            if (!IsFinite(
                    telemetry.MissionTimeSeconds) ||
                !IsFinite(
                    telemetry.ApoapsisMeters))
            {
                return;
            }

            if (IsFinite(
                    _lastApoapsisSampleTime))
            {
                double elapsed =
                    telemetry.MissionTimeSeconds -
                    _lastApoapsisSampleTime;

                if (elapsed >= 0.10 &&
                    elapsed <= 2.0)
                {
                    double instantaneousRate =
                        (telemetry.ApoapsisMeters -
                         _lastApoapsisMeters) /
                        elapsed;

                    if (instantaneousRate > 0.0 &&
                        instantaneousRate < 50000.0)
                    {
                        if (!IsFinite(
                                _smoothedApoapsisRateMetersPerSecond))
                        {
                            _smoothedApoapsisRateMetersPerSecond =
                                instantaneousRate;
                        }
                        else
                        {
                            _smoothedApoapsisRateMetersPerSecond =
                                _smoothedApoapsisRateMetersPerSecond *
                                0.72 +
                                instantaneousRate *
                                0.28;
                        }
                    }
                }
            }

            _lastApoapsisMeters =
                telemetry.ApoapsisMeters;

            _lastApoapsisSampleTime =
                telemetry.MissionTimeSeconds;
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
