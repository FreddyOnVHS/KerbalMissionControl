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

        private readonly CircularizationPredictor
            _circularizationPredictor =
                new CircularizationPredictor();

        private OrbitModel
            _latest =
                new OrbitModel();

        private double
            _lastMissionTime =
                double.NaN;

        private DateTime
            _lastDiagnosticUtc =
                DateTime.MinValue;

        private DateTime
            _lastPredictionDiagnosticUtc =
                DateTime.MinValue;

        private DateTime
            _lastVelocityDiagnosticUtc =
                DateTime.MinValue;

        public void Update(
            KMC.Shared.TelemetryPacket packet,
            DateTime receivedUtc,
            AscentModel ascent,
            VelocityVectorTelemetryModel velocityVector)
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
                _circularizationPredictor.Reset();

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

            VelocityVectorTelemetryModel evaluatedVelocity =
                VelocityVectorTelemetryModel.Clone(
                    velocityVector);

            evaluatedVelocity.EvaluateAgainstFlightPacket(
                packet,
                receivedUtc);

            CircularizationPredictionModel prediction =
                _circularizationPredictor.Calculate(
                    current,
                    targetOrbit,
                    handoffObserved);

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
                            KerbinAtmosphereTopMeters,

                    CircularizationPrediction =
                        prediction,

                    VelocityVector =
                        evaluatedVelocity
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

            WritePredictionDiagnosticIfDue(
                next,
                receivedUtc);

            WriteVelocityDiagnosticIfDue(
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

        private void WritePredictionDiagnosticIfDue(
            OrbitModel model,
            DateTime receivedUtc)
        {
            if (model == null ||
                model.CircularizationPrediction == null)
            {
                return;
            }

            if (_lastPredictionDiagnosticUtc !=
                    DateTime.MinValue &&
                (receivedUtc -
                 _lastPredictionDiagnosticUtc)
                    .TotalSeconds <
                    1.0)
            {
                return;
            }

            _lastPredictionDiagnosticUtc =
                receivedUtc;

            CircularizationPredictionModel prediction =
                model.CircularizationPrediction;

            Debug.WriteLine(
                "KMC.Engine ORBIT PREDICTION" +
                " | Available=" +
                prediction.Available +
                " | Status=" +
                prediction.Status +
                " | Evidence=" +
                prediction.ThrustEvidence +
                " | Target=" +
                Format(
                    prediction.TargetOrbitMeters,
                    "0") +
                "m" +
                " | V=" +
                Format(
                    prediction.CurrentOrbitalSpeedMetersPerSecond,
                    "0.0") +
                "m/s" +
                " | Vtgt=" +
                Format(
                    prediction.TargetSpeedMetersPerSecond,
                    "0.0") +
                "m/s" +
                " | DV=" +
                Format(
                    prediction.RemainingDeltaVMetersPerSecond,
                    "0.0") +
                "m/s" +
                " | Burn=" +
                Format(
                    prediction.BurnTimeSeconds,
                    "0.0") +
                "s" +
                " | IgnitionIn=" +
                Format(
                    prediction.IgnitionInSeconds,
                    "0.0") +
                "s" +
                " | Throttle=" +
                Format(
                    prediction.RecommendedThrottleFraction *
                    100.0,
                    "0") +
                "%" +
                " | ShutdownDV=" +
                Format(
                    prediction.ShutdownResponseDeltaVMetersPerSecond,
                    "0.00") +
                "m/s" +
                " | PredAp=" +
                Format(
                    prediction.PredictedApoapsisMeters,
                    "0") +
                "m" +
                " | PredPe=" +
                Format(
                    prediction.PredictedPeriapsisMeters,
                    "0") +
                "m" +
                " | OrbitErr=" +
                Format(
                    prediction.PredictedOrbitErrorMeters,
                    "0") +
                "m" +
                " | EnergyErr=" +
                Format(
                    prediction.EnergyErrorJoulesPerKilogram,
                    "0") +
                "J/kg" +
                " | PredEnergyErr=" +
                Format(
                    prediction.PredictedEnergyErrorJoulesPerKilogram,
                    "0") +
                "J/kg" +
                " | InitialDV=" +
                Format(
                    prediction.InitialDeltaVMetersPerSecond,
                    "0.0") +
                "m/s" +
                " | Complete=" +
                Format(
                    prediction.BurnCompletionPercent,
                    "0.0") +
                "%" +
                " | Handoff=" +
                model.AscentHandoffObserved +
                " | Reset=" +
                model.ResetOccurredThisUpdate);
        }

        private void WriteVelocityDiagnosticIfDue(
            OrbitModel model,
            DateTime receivedUtc)
        {
            if (model == null ||
                model.VelocityVector == null)
            {
                return;
            }

            if (_lastVelocityDiagnosticUtc !=
                    DateTime.MinValue &&
                (receivedUtc -
                 _lastVelocityDiagnosticUtc)
                    .TotalSeconds <
                    1.0)
            {
                return;
            }

            _lastVelocityDiagnosticUtc =
                receivedUtc;

            VelocityVectorTelemetryModel vector =
                model.VelocityVector;

            Debug.WriteLine(
                "KMC.Engine VELOCITY VECTOR" +
                " | Available=" +
                vector.Available +
                " | Status=" +
                vector.Status +
                " | Fresh=" +
                vector.Fresh +
                " | VesselMatch=" +
                vector.VesselMatchesFlightPacket +
                " | Surface=(" +
                "R=" +
                Format(
                    vector.SurfaceRightMetersPerSecond,
                    "0.0") +
                "," +
                "Nose=" +
                Format(
                    vector.SurfaceNoseMetersPerSecond,
                    "0.0") +
                "," +
                "RefFwd=" +
                Format(
                    vector.SurfaceReferenceForwardMetersPerSecond,
                    "0.0") +
                ")" +
                " | SurfaceMag=" +
                Format(
                    vector.SurfaceMagnitudeMetersPerSecond,
                    "0.0") +
                "m/s" +
                " | SurfacePacket=" +
                Format(
                    vector.FlightPacketSurfaceSpeedMetersPerSecond,
                    "0.0") +
                "m/s" +
                " | SurfaceDiff=" +
                Format(
                    vector.SurfaceSpeedDifferenceMetersPerSecond,
                    "0.00") +
                "m/s" +
                " | SurfaceAgree=" +
                vector.SurfaceSpeedAgreement +
                " | Orbital=(" +
                "R=" +
                Format(
                    vector.OrbitalRightMetersPerSecond,
                    "0.0") +
                "," +
                "Nose=" +
                Format(
                    vector.OrbitalNoseMetersPerSecond,
                    "0.0") +
                "," +
                "RefFwd=" +
                Format(
                    vector.OrbitalReferenceForwardMetersPerSecond,
                    "0.0") +
                ")" +
                " | OrbitalMag=" +
                Format(
                    vector.OrbitalMagnitudeMetersPerSecond,
                    "0.0") +
                "m/s" +
                " | OrbitalPacket=" +
                Format(
                    vector.FlightPacketOrbitalSpeedMetersPerSecond,
                    "0.0") +
                "m/s" +
                " | OrbitalDiff=" +
                Format(
                    vector.OrbitalSpeedDifferenceMetersPerSecond,
                    "0.00") +
                "m/s" +
                " | OrbitalAgree=" +
                vector.OrbitalSpeedAgreement);
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
