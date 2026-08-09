using System;
using System.Diagnostics;
using KMC.Shared;

namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Stateful Engine-owned ascent foundation.
    ///
    /// Build 9.0 runs this component directly from EngineeringEngine so the
    /// new history can be validated in parallel with the legacy ASCENT page
    /// before any renderer ownership is changed.
    /// </summary>
    internal sealed class AscentFoundationSystem
    {
        private readonly AscentHistoryTracker _history =
            new AscentHistoryTracker();

        private AscentModel _latest =
            new AscentModel();

        private DateTime _lastDiagnosticUtc =
            DateTime.MinValue;

        public void Update(
            TelemetryPacket packet,
            DateTime receivedUtc)
        {
            if (packet == null)
            {
                _latest =
                    new AscentModel();

                return;
            }

            bool reset =
                _history.Update(
                    packet);

            AscentTelemetryState current =
                CreateCurrentState(
                    packet);

            AscentModel model =
                new AscentModel
                {
                    Available =
                        true,

                    ReceivedUtc =
                        receivedUtc,

                    Current =
                        current,

                    History =
                        _history.CreateSnapshot(
                            reset)
                };

            double vertical =
                packet.VerticalSpeed;

            double horizontal =
                packet.HorizontalSpeed;

            if (IsFinite(vertical) &&
                IsFinite(horizontal))
            {
                model.FlightPathAngleAvailable =
                    true;

                model.FlightPathAngleDegrees =
                    Math.Atan2(
                        vertical,
                        Math.Max(
                            0.000001,
                            horizontal)) *
                    180.0 /
                    Math.PI;
            }

            _latest =
                model;

            WriteDiagnosticIfDue(
                receivedUtc,
                model);
        }

        public AscentModel GetLatest()
        {
            return
                Clone(
                    _latest);
        }

        private static AscentTelemetryState CreateCurrentState(
            TelemetryPacket packet)
        {
            return
                new AscentTelemetryState
                {
                    Available =
                        true,

                    VesselName =
                        packet.VesselName ??
                        string.Empty,

                    BodyName =
                        packet.BodyName ??
                        string.Empty,

                    MissionTimeSeconds =
                        packet.MissionTime,

                    CurrentStage =
                        packet.CurrentStage,

                    AltitudeMeters =
                        packet.Altitude,

                    RadarAltitudeMeters =
                        packet.RadarAltitude,

                    VerticalSpeedMetersPerSecond =
                        packet.VerticalSpeed,

                    HorizontalSpeedMetersPerSecond =
                        packet.HorizontalSpeed,

                    OrbitalSpeedMetersPerSecond =
                        packet.OrbitalSpeed,

                    PitchDegrees =
                        packet.Pitch,

                    HeadingDegrees =
                        packet.Heading,

                    RollDegrees =
                        packet.Roll,

                    DynamicPressureKpa =
                        packet.DynamicPressureKpa,

                    StaticPressureKpa =
                        packet.StaticPressureKpa,

                    Mach =
                        packet.Mach,

                    GForce =
                        packet.GForce,

                    ApoapsisMeters =
                        packet.Apoapsis,

                    PeriapsisMeters =
                        packet.Periapsis,

                    TimeToApoapsisSeconds =
                        packet.TimeToApoapsis,

                    VesselMassTonnes =
                        packet.VesselMass,

                    CurrentThrustKilonewtons =
                        packet.CurrentThrust,

                    MaximumThrustKilonewtons =
                        packet.MaximumThrust,

                    ThrustToWeightRatio =
                        packet.ThrustToWeightRatio,

                    AverageSpecificImpulseSeconds =
                        packet.AverageSpecificImpulse,

                    StageLiquidFuelAmount =
                        packet.StageLiquidFuelAmount,

                    StageLiquidFuelCapacity =
                        packet.StageLiquidFuelCapacity,

                    StageOxidizerAmount =
                        packet.StageOxidizerAmount,

                    StageOxidizerCapacity =
                        packet.StageOxidizerCapacity
                };
        }

        private void WriteDiagnosticIfDue(
            DateTime receivedUtc,
            AscentModel model)
        {
            DateTime utc =
                receivedUtc.Kind ==
                    DateTimeKind.Utc
                        ? receivedUtc
                        : receivedUtc
                            .ToUniversalTime();

            if (_lastDiagnosticUtc !=
                    DateTime.MinValue &&
                (utc -
                 _lastDiagnosticUtc)
                    .TotalSeconds <
                1.0)
            {
                return;
            }

            _lastDiagnosticUtc =
                utc;

            Debug.WriteLine(
                "KMC.Engine ASCENT FOUNDATION" +
                " | Vessel=" +
                model.Current.VesselName +
                " | MET=" +
                model.Current.MissionTimeSeconds
                    .ToString("0.0") +
                " | Stage=" +
                model.Current.CurrentStage +
                " | Alt=" +
                model.Current.AltitudeMeters
                    .ToString("0.0") +
                "m | Downrange=" +
                model.History.DownrangeMeters
                    .ToString("0.0") +
                "m | Samples=" +
                model.History.SampleCount +
                " | VSpeed=" +
                model.Current.VerticalSpeedMetersPerSecond
                    .ToString("0.0") +
                "m/s | HSpeed=" +
                model.Current.HorizontalSpeedMetersPerSecond
                    .ToString("0.0") +
                "m/s | Pitch=" +
                model.Current.PitchDegrees
                    .ToString("0.0") +
                "deg | Heading=" +
                model.Current.HeadingDegrees
                    .ToString("0.0") +
                "deg | Roll=" +
                model.Current.RollDegrees
                    .ToString("0.0") +
                "deg | FPA=" +
                (model.FlightPathAngleAvailable
                    ? model.FlightPathAngleDegrees
                        .ToString("+0.0;-0.0;0.0") +
                      "deg"
                    : "UNKNOWN") +
                " | Q=" +
                model.Current.DynamicPressureKpa
                    .ToString("0.00") +
                "kPa | Ap=" +
                model.Current.ApoapsisMeters
                    .ToString("0.0") +
                "m | Reset=" +
                model.History.MissionResetDetected);
        }

        private static AscentModel Clone(
            AscentModel source)
        {
            AscentModel clone =
                new AscentModel();

            if (source == null ||
                !source.Available)
            {
                return clone;
            }

            clone.Available =
                source.Available;

            clone.ReceivedUtc =
                source.ReceivedUtc;

            clone.FlightPathAngleAvailable =
                source.FlightPathAngleAvailable;

            clone.FlightPathAngleDegrees =
                source.FlightPathAngleDegrees;

            clone.Current =
                new AscentTelemetryState
                {
                    Available =
                        source.Current.Available,

                    VesselName =
                        source.Current.VesselName,

                    BodyName =
                        source.Current.BodyName,

                    MissionTimeSeconds =
                        source.Current.MissionTimeSeconds,

                    CurrentStage =
                        source.Current.CurrentStage,

                    AltitudeMeters =
                        source.Current.AltitudeMeters,

                    RadarAltitudeMeters =
                        source.Current.RadarAltitudeMeters,

                    VerticalSpeedMetersPerSecond =
                        source.Current.VerticalSpeedMetersPerSecond,

                    HorizontalSpeedMetersPerSecond =
                        source.Current.HorizontalSpeedMetersPerSecond,

                    OrbitalSpeedMetersPerSecond =
                        source.Current.OrbitalSpeedMetersPerSecond,

                    PitchDegrees =
                        source.Current.PitchDegrees,

                    HeadingDegrees =
                        source.Current.HeadingDegrees,

                    RollDegrees =
                        source.Current.RollDegrees,

                    DynamicPressureKpa =
                        source.Current.DynamicPressureKpa,

                    StaticPressureKpa =
                        source.Current.StaticPressureKpa,

                    Mach =
                        source.Current.Mach,

                    GForce =
                        source.Current.GForce,

                    ApoapsisMeters =
                        source.Current.ApoapsisMeters,

                    PeriapsisMeters =
                        source.Current.PeriapsisMeters,

                    TimeToApoapsisSeconds =
                        source.Current.TimeToApoapsisSeconds,

                    VesselMassTonnes =
                        source.Current.VesselMassTonnes,

                    CurrentThrustKilonewtons =
                        source.Current.CurrentThrustKilonewtons,

                    MaximumThrustKilonewtons =
                        source.Current.MaximumThrustKilonewtons,

                    ThrustToWeightRatio =
                        source.Current.ThrustToWeightRatio,

                    AverageSpecificImpulseSeconds =
                        source.Current.AverageSpecificImpulseSeconds,

                    StageLiquidFuelAmount =
                        source.Current.StageLiquidFuelAmount,

                    StageLiquidFuelCapacity =
                        source.Current.StageLiquidFuelCapacity,

                    StageOxidizerAmount =
                        source.Current.StageOxidizerAmount,

                    StageOxidizerCapacity =
                        source.Current.StageOxidizerCapacity
                };

            AscentHistoryModel history =
                new AscentHistoryModel
                {
                    Available =
                        source.History.Available,

                    TrackedVesselName =
                        source.History.TrackedVesselName,

                    DownrangeMeters =
                        source.History.DownrangeMeters,

                    MissionResetDetected =
                        source.History.MissionResetDetected,

                    MissionResetCount =
                        source.History.MissionResetCount
                };

            for (int index = 0;
                 index < source.History.Samples.Count;
                 index++)
            {
                AscentHistorySample sample =
                    source.History.Samples[index];

                history.Samples.Add(
                    new AscentHistorySample
                    {
                        MissionTimeSeconds =
                            sample.MissionTimeSeconds,

                        StageNumber =
                            sample.StageNumber,

                        DownrangeMeters =
                            sample.DownrangeMeters,

                        AltitudeMeters =
                            sample.AltitudeMeters,

                        ApoapsisMeters =
                            sample.ApoapsisMeters,

                        PitchDegrees =
                            sample.PitchDegrees,

                        DynamicPressureKpa =
                            sample.DynamicPressureKpa,

                        VerticalSpeedMetersPerSecond =
                            sample.VerticalSpeedMetersPerSecond,

                        HorizontalSpeedMetersPerSecond =
                            sample.HorizontalSpeedMetersPerSecond,

                        OrbitalSpeedMetersPerSecond =
                            sample.OrbitalSpeedMetersPerSecond,

                        VesselMassTonnes =
                            sample.VesselMassTonnes,

                        CurrentThrustKilonewtons =
                            sample.CurrentThrustKilonewtons,

                        AverageSpecificImpulseSeconds =
                            sample.AverageSpecificImpulseSeconds,

                        StageLiquidFuelAmount =
                            sample.StageLiquidFuelAmount,

                        StageOxidizerAmount =
                            sample.StageOxidizerAmount
                    });
            }

            clone.History =
                history;

            return clone;
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
