using System;
using System.Diagnostics;
using KMC.Engine.Ascent;

namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Stateful Engine owner for the current ORBIT foundation.
    ///
    /// Build 10.0 establishes ownership, reset semantics, target handoff, and
    /// diagnostics only. Circularization calculations begin in Build 10.1.
    /// </summary>
    public sealed class OrbitFoundationSystem
    {
        private const double DefaultTargetOrbitMeters =
            80000.0;

        private const double KerbinAtmosphereTopMeters =
            70000.0;

        private readonly object
            _syncRoot =
                new object();

        private OrbitModel
            _latest =
                new OrbitModel();

        private double
            _lastMissionTime =
                double.NaN;

        private DateTime
            _lastDiagnosticUtc =
                DateTime.MinValue;

        public void Update(
            KMC.Shared.TelemetryPacket packet,
            DateTime receivedUtc,
            AscentModel ascent)
        {
            if (packet == null)
            {
                return;
            }

            bool reset =
                IsFinite(
                    _lastMissionTime) &&
                packet.MissionTime + 0.5 <
                    _lastMissionTime;

            if (reset)
            {
                lock (_syncRoot)
                {
                    _latest =
                        new OrbitModel();
                }
            }

            OrbitTelemetryState current =
                OrbitTelemetryState.FromPacket(
                    packet,
                    receivedUtc);

            double targetOrbit =
                DefaultTargetOrbitMeters;

            bool inheritedTarget =
                false;

            bool handoffObserved =
                false;

            if (ascent != null &&
                ascent.Available)
            {
                if (ascent.Profile != null &&
                    IsFinite(
                        ascent.Profile.TargetApoapsisMeters) &&
                    ascent.Profile.TargetApoapsisMeters >
                        0.0)
                {
                    targetOrbit =
                        ascent.Profile.TargetApoapsisMeters;

                    inheritedTarget =
                        true;
                }

                if (ascent.FlightDirector != null &&
                    ascent.FlightDirector.Available &&
                    ascent.FlightDirector
                        .OrbitHandoffRequired)
                {
                    handoffObserved =
                        true;
                }
            }

            OrbitModel next =
                new OrbitModel
                {
                    Available =
                        current.Available,

                    ResetOccurredThisUpdate =
                        reset,

                    Current =
                        current,

                    TargetOrbitMeters =
                        targetOrbit,

                    TargetInheritedFromAscent =
                        inheritedTarget,

                    AscentHandoffObserved =
                        handoffObserved,

                    IsAboveAtmosphere =
                        IsFinite(
                            current.AltitudeMeters) &&
                        current.AltitudeMeters >=
                            KerbinAtmosphereTopMeters,

                    LivePeriapsisAboveAtmosphere =
                        IsFinite(
                            current.PeriapsisMeters) &&
                        current.PeriapsisMeters >=
                            KerbinAtmosphereTopMeters
                };

            lock (_syncRoot)
            {
                _latest =
                    next;
            }

            _lastMissionTime =
                packet.MissionTime;

            WriteDiagnosticIfDue(
                next,
                receivedUtc);
        }

        public OrbitModel GetLatest()
        {
            lock (_syncRoot)
            {
                return
                    OrbitModel.Clone(
                        _latest);
            }
        }

        private void WriteDiagnosticIfDue(
            OrbitModel model,
            DateTime receivedUtc)
        {
            if (model == null ||
                !model.Available)
            {
                return;
            }

            if (_lastDiagnosticUtc !=
                    DateTime.MinValue &&
                (receivedUtc -
                 _lastDiagnosticUtc)
                    .TotalSeconds <
                    1.0)
            {
                return;
            }

            _lastDiagnosticUtc =
                receivedUtc;

            OrbitTelemetryState current =
                model.Current;

            Debug.WriteLine(
                "KMC.Engine ORBIT FOUNDATION" +
                " | MET=" +
                Format(
                    current.MissionTimeSeconds,
                    "0.0") +
                "s" +
                " | Stage=" +
                current.CurrentStage +
                " | Alt=" +
                Format(
                    current.AltitudeMeters,
                    "0") +
                "m" +
                " | Ap=" +
                Format(
                    current.ApoapsisMeters,
                    "0") +
                "m" +
                " | Pe=" +
                Format(
                    current.PeriapsisMeters,
                    "0") +
                "m" +
                " | TAp=" +
                Format(
                    current.TimeToApoapsisSeconds,
                    "0.0") +
                "s" +
                " | TPe=" +
                Format(
                    current.TimeToPeriapsisSeconds,
                    "0.0") +
                "s" +
                " | V=" +
                Format(
                    current.OrbitalSpeedMetersPerSecond,
                    "0.0") +
                "m/s" +
                " | Ecc=" +
                Format(
                    current.Eccentricity,
                    "0.00000") +
                " | SMA=" +
                Format(
                    current.SemiMajorAxisMeters,
                    "0") +
                "m" +
                " | Inc=" +
                Format(
                    current.InclinationDegrees,
                    "0.00") +
                "deg" +
                " | Target=" +
                Format(
                    model.TargetOrbitMeters,
                    "0") +
                "m" +
                " | TargetFromAscent=" +
                model.TargetInheritedFromAscent +
                " | Handoff=" +
                model.AscentHandoffObserved +
                " | AboveAtmo=" +
                model.IsAboveAtmosphere +
                " | PeSafe=" +
                model.LivePeriapsisAboveAtmosphere +
                " | Reset=" +
                model.ResetOccurredThisUpdate);
        }

        private static string Format(
            double value,
            string format)
        {
            return
                IsFinite(
                    value)
                    ? value.ToString(
                        format)
                    : "---";
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
