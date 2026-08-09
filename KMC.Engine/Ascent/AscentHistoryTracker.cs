using System;
using System.Collections.Generic;
using KMC.Shared;

namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Engine-owned trajectory history.
    ///
    /// Build 9.0 intentionally mirrors the proven MissionControl ascent
    /// history semantics:
    /// - 0.20 second minimum sample interval
    /// - 900 retained samples
    /// - horizontal-speed downrange integration
    /// - mission-time rollback reset
    /// - vessel-name changes do NOT reset the mission
    /// </summary>
    internal sealed class AscentHistoryTracker
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

        private long _missionResetCount;

        public bool Update(
            TelemetryPacket packet)
        {
            if (packet == null)
            {
                return false;
            }

            string vesselName =
                packet.VesselName ??
                string.Empty;

            bool vesselChanged =
                !string.Equals(
                    vesselName,
                    _trackedVesselName,
                    StringComparison.Ordinal);

            bool timeReset =
                IsFinite(
                    _previousMissionTime) &&
                packet.MissionTime + 0.5 <
                    _previousMissionTime;

            if (timeReset)
            {
                Reset(
                    vesselName);

                _missionResetCount++;
            }
            else if (vesselChanged)
            {
                /*
                 * Staging/separation/control-point changes may rename the
                 * active vessel. Preserve the current trajectory.
                 */
                _trackedVesselName =
                    vesselName;
            }

            if (!IsFinite(
                    packet.MissionTime))
            {
                return timeReset;
            }

            if (!IsFinite(
                    _previousMissionTime))
            {
                _previousMissionTime =
                    packet.MissionTime;
            }

            double deltaTime =
                packet.MissionTime -
                _previousMissionTime;

            if (deltaTime < 0.0 ||
                deltaTime > 10.0)
            {
                deltaTime =
                    0.0;
            }

            if (deltaTime > 0.0 &&
                IsFinite(
                    packet.HorizontalSpeed))
            {
                _downrangeMeters +=
                    Math.Max(
                        0.0,
                        packet.HorizontalSpeed) *
                    deltaTime;
            }

            bool shouldSample =
                _samples.Count == 0 ||
                packet.MissionTime -
                    _samples[
                        _samples.Count - 1]
                        .MissionTimeSeconds >=
                    MinimumSampleIntervalSeconds;

            if (shouldSample)
            {
                _samples.Add(
                    CreateSample(
                        packet));

                while (_samples.Count >
                       MaximumSamples)
                {
                    _samples.RemoveAt(
                        0);
                }
            }

            _previousMissionTime =
                packet.MissionTime;

            return timeReset;
        }

        public AscentHistoryModel CreateSnapshot(
            bool missionResetDetected)
        {
            AscentHistoryModel model =
                new AscentHistoryModel
                {
                    Available =
                        true,

                    TrackedVesselName =
                        _trackedVesselName,

                    DownrangeMeters =
                        Math.Max(
                            0.0,
                            _downrangeMeters),

                    MissionResetDetected =
                        missionResetDetected,

                    MissionResetCount =
                        _missionResetCount
                };

            for (int index = 0;
                 index < _samples.Count;
                 index++)
            {
                AscentHistorySample source =
                    _samples[index];

                model.Samples.Add(
                    new AscentHistorySample
                    {
                        MissionTimeSeconds =
                            source.MissionTimeSeconds,

                        StageNumber =
                            source.StageNumber,

                        DownrangeMeters =
                            source.DownrangeMeters,

                        AltitudeMeters =
                            source.AltitudeMeters,

                        ApoapsisMeters =
                            source.ApoapsisMeters,

                        PitchDegrees =
                            source.PitchDegrees,

                        DynamicPressureKpa =
                            source.DynamicPressureKpa,

                        VerticalSpeedMetersPerSecond =
                            source.VerticalSpeedMetersPerSecond,

                        HorizontalSpeedMetersPerSecond =
                            source.HorizontalSpeedMetersPerSecond,

                        OrbitalSpeedMetersPerSecond =
                            source.OrbitalSpeedMetersPerSecond,

                        VesselMassTonnes =
                            source.VesselMassTonnes,

                        CurrentThrustKilonewtons =
                            source.CurrentThrustKilonewtons,

                        AverageSpecificImpulseSeconds =
                            source.AverageSpecificImpulseSeconds,

                        StageLiquidFuelAmount =
                            source.StageLiquidFuelAmount,

                        StageOxidizerAmount =
                            source.StageOxidizerAmount
                    });
            }

            return model;
        }

        private AscentHistorySample CreateSample(
            TelemetryPacket packet)
        {
            return
                new AscentHistorySample
                {
                    MissionTimeSeconds =
                        packet.MissionTime,

                    StageNumber =
                        packet.CurrentStage,

                    DownrangeMeters =
                        Math.Max(
                            0.0,
                            _downrangeMeters),

                    AltitudeMeters =
                        Math.Max(
                            0.0,
                            packet.Altitude),

                    ApoapsisMeters =
                        packet.Apoapsis,

                    PitchDegrees =
                        packet.Pitch,

                    DynamicPressureKpa =
                        packet.DynamicPressureKpa,

                    VerticalSpeedMetersPerSecond =
                        packet.VerticalSpeed,

                    HorizontalSpeedMetersPerSecond =
                        packet.HorizontalSpeed,

                    OrbitalSpeedMetersPerSecond =
                        packet.OrbitalSpeed,

                    VesselMassTonnes =
                        packet.VesselMass,

                    CurrentThrustKilonewtons =
                        packet.CurrentThrust,

                    AverageSpecificImpulseSeconds =
                        packet.AverageSpecificImpulse,

                    StageLiquidFuelAmount =
                        packet.StageLiquidFuelAmount,

                    StageOxidizerAmount =
                        packet.StageOxidizerAmount
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
