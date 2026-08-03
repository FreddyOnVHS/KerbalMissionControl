using System;
using System.Collections.Generic;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Flight
{
    /// <summary>
    /// Owns ascent trajectory history, sample cadence, downrange integration,
    /// active-vessel name changes, mission reset detection, and sample
    /// retention.
    /// </summary>
    public sealed class AscentFlightHistory
    {
        private const double MinimumSampleIntervalSeconds =
            0.20;

        private const int MaximumSamples =
            900;

        private readonly List<AscentHistorySample> _samples =
            new List<AscentHistorySample>();

        private string _trackedVesselName =
            string.Empty;

        private double _previousMissionTime =
            double.NaN;

        private double _downrangeMeters;

        public IList<AscentHistorySample> Samples
        {
            get { return _samples; }
        }

        public double DownrangeMeters
        {
            get { return _downrangeMeters; }
        }

        public AscentFlightHistoryUpdate Update(
            MissionTelemetry telemetry)
        {
            AscentFlightHistoryUpdate result =
                new AscentFlightHistoryUpdate();

            if (telemetry == null)
            {
                return result;
            }

            string vesselName =
                telemetry.VesselName ??
                string.Empty;

            bool vesselChanged =
                !string.Equals(
                    vesselName,
                    _trackedVesselName,
                    StringComparison.Ordinal);

            bool timeReset =
                IsFinite(
                    _previousMissionTime) &&
                telemetry.MissionTime + 0.5 <
                _previousMissionTime;

            if (timeReset)
            {
                Reset(
                    vesselName);

                result.MissionReset =
                    true;
            }
            else if (vesselChanged)
            {
                /*
                 * Staging, separation, docking, and control-point changes
                 * can rename the active vessel without starting a new
                 * mission. Preserve the existing trajectory.
                 */
                _trackedVesselName =
                    vesselName;
            }

            if (!IsFinite(
                    telemetry.MissionTime))
            {
                return result;
            }

            if (!IsFinite(
                    _previousMissionTime))
            {
                _previousMissionTime =
                    telemetry.MissionTime;
            }

            double deltaTime =
                telemetry.MissionTime -
                _previousMissionTime;

            if (deltaTime < 0.0 ||
                deltaTime > 10.0)
            {
                deltaTime =
                    0.0;
            }

            if (deltaTime > 0.0 &&
                IsFinite(
                    telemetry.HorizontalSpeed))
            {
                _downrangeMeters +=
                    Math.Max(
                        0.0,
                        telemetry.HorizontalSpeed) *
                    deltaTime;
            }

            bool shouldSample =
                _samples.Count == 0 ||
                telemetry.MissionTime -
                _samples[
                    _samples.Count - 1]
                    .MissionTime >=
                MinimumSampleIntervalSeconds;

            if (shouldSample)
            {
                _samples.Add(
                    CreateSample(
                        telemetry));

                while (_samples.Count >
                       MaximumSamples)
                {
                    _samples.RemoveAt(
                        0);
                }

                result.SampleAdded =
                    true;
            }

            _previousMissionTime =
                telemetry.MissionTime;

            return result;
        }

        private AscentHistorySample CreateSample(
            MissionTelemetry telemetry)
        {
            return new AscentHistorySample
            {
                MissionTime =
                    telemetry.MissionTime,

                DownrangeMeters =
                    Math.Max(
                        0.0,
                        _downrangeMeters),

                AltitudeMeters =
                    Math.Max(
                        0.0,
                        telemetry.Altitude),

                ApoapsisMeters =
                    telemetry.Apoapsis,

                PitchDegrees =
                    telemetry.Pitch,

                DynamicPressureKpa =
                    telemetry.DynamicPressureKpa,

                StageLiquidFuelAmount =
                    telemetry.StageLiquidFuelAmount,

                StageOxidizerAmount =
                    telemetry.StageOxidizerAmount,

                OrbitalSpeedMetersPerSecond =
                    telemetry.OrbitalSpeed,

                VesselMassTonnes =
                    telemetry.VesselMass,

                CurrentThrustKilonewtons =
                    telemetry.CurrentThrust,

                AverageSpecificImpulseSeconds =
                    telemetry.AverageSpecificImpulse,

                StageNumber =
                    telemetry.CurrentStage
            };
        }

        private void Reset(
            string vesselName)
        {
            _samples.Clear();

            _trackedVesselName =
                vesselName ??
                string.Empty;

            _previousMissionTime =
                double.NaN;

            _downrangeMeters =
                0.0;
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
