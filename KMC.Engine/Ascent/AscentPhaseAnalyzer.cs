using System;

namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Engine-owned powered-ascent phase analyzer.
    ///
    /// The phase thresholds intentionally preserve the ascent side of the
    /// current MissionControl MissionPlanner. Once MECO has been latched,
    /// Engine ASCENT terminates at CoastHandoff. Circularization belongs to
    /// the future ORBIT system.
    /// </summary>
    internal sealed class AscentPhaseAnalyzer
    {
        private const double TargetApproachBandMeters =
            12000.0;

        private const double MecoCountdownWindowSeconds =
            5.0;

        private const double MecoFlashDurationSeconds =
            1.25;

        private readonly AscentCutoffAnalyzer _cutoffAnalyzer =
            new AscentCutoffAnalyzer();

        private bool _ascentMecoLatched;

        private double _mecoCommandTime =
            double.NaN;

        public void Reset()
        {
            _cutoffAnalyzer.Reset();

            _ascentMecoLatched =
                false;

            _mecoCommandTime =
                double.NaN;
        }

        public AscentPhaseModel Update(
            AscentTelemetryState telemetry,
            double targetApoapsisMeters)
        {
            AscentPhaseModel result =
                new AscentPhaseModel();

            if (telemetry == null ||
                !telemetry.Available)
            {
                return result;
            }

            result.Available =
                true;

            result.Cutoff =
                _cutoffAnalyzer.Update(
                    telemetry,
                    targetApoapsisMeters);

            bool launchStarted =
                telemetry.MissionTimeSeconds >= 1.0 ||
                telemetry.RadarAltitudeMeters >= 15.0 ||
                telemetry.VerticalSpeedMetersPerSecond >= 3.0;

            result.MissionStarted =
                launchStarted;

            if (!launchStarted)
            {
                SetPhase(
                    result,
                    AscentFlightPhase.Prelaunch);

                return result;
            }

            if (_ascentMecoLatched)
            {
                result.MecoLatched =
                    true;

                result.OrbitHandoffRequired =
                    true;

                SetPhase(
                    result,
                    AscentFlightPhase.CoastHandoff);

                return result;
            }

            if (result.Cutoff.CutoffReached)
            {
                _ascentMecoLatched =
                    true;

                _mecoCommandTime =
                    telemetry.MissionTimeSeconds;

                result.MecoLatched =
                    true;

                result.FlashAlert =
                    true;

                SetPhase(
                    result,
                    AscentFlightPhase.Meco);

                return result;
            }

            if (result.Cutoff.EstimatedMecoAvailable &&
                result.Cutoff.EstimatedMecoSeconds <=
                    MecoCountdownWindowSeconds)
            {
                result.MecoCountdownSeconds =
                    Math.Max(
                        1,
                        Math.Min(
                            5,
                            (int)Math.Ceiling(
                                result.Cutoff
                                    .EstimatedMecoSeconds)));

                SetPhase(
                    result,
                    AscentFlightPhase.MecoCountdown);

                return result;
            }

            if (telemetry.ApoapsisMeters >=
                targetApoapsisMeters -
                TargetApproachBandMeters)
            {
                SetPhase(
                    result,
                    AscentFlightPhase.TargetApproach);

                return result;
            }

            SetPhase(
                result,
                AscentFlightPhase.Ascent);

            result.FlashAlert =
                IsFinite(
                    _mecoCommandTime) &&
                telemetry.MissionTimeSeconds -
                    _mecoCommandTime <=
                    MecoFlashDurationSeconds;

            return result;
        }

        private static void SetPhase(
            AscentPhaseModel result,
            AscentFlightPhase phase)
        {
            result.Phase =
                phase;

            switch (phase)
            {
                case AscentFlightPhase.Prelaunch:
                    result.PhaseName =
                        "PRELAUNCH";
                    break;

                case AscentFlightPhase.Ascent:
                    result.PhaseName =
                        "ASCENT";
                    break;

                case AscentFlightPhase.TargetApproach:
                    result.PhaseName =
                        "TARGET APPROACH";
                    break;

                case AscentFlightPhase.MecoCountdown:
                    result.PhaseName =
                        "MECO COUNTDOWN";
                    break;

                case AscentFlightPhase.Meco:
                    result.PhaseName =
                        "MECO";
                    break;

                case AscentFlightPhase.CoastHandoff:
                    result.PhaseName =
                        "COAST HANDOFF";
                    break;

                default:
                    result.PhaseName =
                        "UNKNOWN";
                    break;
            }
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
