using System;
using KMC.Engine.Orbit;

namespace KMC.Engine.Maneuver
{
    /// <summary>
    /// Build 13.0 generalized two-body apsis maneuver solver.
    ///
    /// Supported:
    /// - Circularize at next apoapsis.
    /// - Set periapsis at next apoapsis.
    /// - Set apoapsis at next periapsis.
    ///
    /// The burn is purely prograde/retrograde. Normal and radial Delta-V
    /// remain zero. Kerbin constants intentionally preserve the validated
    /// Build 11.x scope.
    /// </summary>
    internal sealed class ApsisManeuverPlanner
    {
        private const double KerbinRadiusMeters =
            600000.0;

        private const double KerbinGravitationalParameter =
            3.5316e12;

        private const double StandardGravity =
            9.80665;

        private const double MinimumUsefulDeltaV =
            0.05;

        public ManeuverPlanModel Calculate(
            OrbitModel orbit,
            ManeuverEpochTelemetryModel epoch,
            ManeuverRequestModel request)
        {
            ManeuverRequestModel effectiveRequest =
                ManeuverRequestModel.Clone(
                    request);

            ManeuverPlanModel plan =
                new ManeuverPlanModel
                {
                    Objective =
                        BuildObjective(
                            effectiveRequest),

                    Status =
                        "PLAN UNAVAILABLE"
                };

            if (orbit == null ||
                !orbit.Available ||
                orbit.Current == null ||
                !orbit.Current.Available)
            {
                plan.Evidence.Add(
                    "ORBIT foundation unavailable.");

                return plan;
            }

            OrbitTelemetryState current =
                orbit.Current;

            if (!string.Equals(
                    current.BodyName,
                    "Kerbin",
                    StringComparison.OrdinalIgnoreCase))
            {
                plan.Status =
                    "UNSUPPORTED CENTRAL BODY";

                plan.Evidence.Add(
                    "Build 13.0 apsis constants are validated for Kerbin only.");

                return plan;
            }

            double currentSemiMajorAxis =
                ResolveCurrentSemiMajorAxis(
                    current);

            if (!IsFinite(
                    currentSemiMajorAxis) ||
                currentSemiMajorAxis <= 0.0)
            {
                plan.Status =
                    "INVALID SEMI-MAJOR AXIS";

                plan.Evidence.Add(
                    "No valid current semi-major axis can be established from ORBIT telemetry.");

                return plan;
            }

            bool nodeAtApoapsis;
            double nodeAltitude;
            double oppositeAltitude;
            double timeToNode;

            switch (effectiveRequest.Type)
            {
                case ManeuverRequestType.CircularizeAtApoapsis:
                    nodeAtApoapsis =
                        true;

                    nodeAltitude =
                        current.ApoapsisMeters;

                    oppositeAltitude =
                        current.ApoapsisMeters;

                    timeToNode =
                        current.TimeToApoapsisSeconds;

                    break;

                case ManeuverRequestType.SetPeriapsisAtApoapsis:
                    nodeAtApoapsis =
                        true;

                    nodeAltitude =
                        current.ApoapsisMeters;

                    oppositeAltitude =
                        effectiveRequest.TargetAltitudeMeters;

                    timeToNode =
                        current.TimeToApoapsisSeconds;

                    if (!ValidateTargetPeriapsis(
                            current,
                            oppositeAltitude,
                            plan))
                    {
                        return plan;
                    }

                    break;

                case ManeuverRequestType.SetApoapsisAtPeriapsis:
                    nodeAtApoapsis =
                        false;

                    nodeAltitude =
                        current.PeriapsisMeters;

                    oppositeAltitude =
                        effectiveRequest.TargetAltitudeMeters;

                    timeToNode =
                        current.TimeToPeriapsisSeconds;

                    if (!ValidateTargetApoapsis(
                            current,
                            oppositeAltitude,
                            plan))
                    {
                        return plan;
                    }

                    break;

                default:
                    plan.Status =
                        "UNSUPPORTED MANEUVER REQUEST";

                    plan.Evidence.Add(
                        "The requested maneuver type is not supported by Build 13.0.");

                    return plan;
            }

            if (!IsFinite(
                    current.MissionTimeSeconds) ||
                !IsFinite(
                    timeToNode) ||
                timeToNode < 0.0)
            {
                plan.Status =
                    "INVALID MANEUVER TIME";

                plan.Evidence.Add(
                    "MET or time-to-node telemetry is invalid.");

                return plan;
            }

            double nodeRadius =
                KerbinRadiusMeters +
                nodeAltitude;

            double oppositeRadius =
                KerbinRadiusMeters +
                oppositeAltitude;

            if (!IsFinite(nodeRadius) ||
                !IsFinite(oppositeRadius) ||
                nodeRadius <= 0.0 ||
                oppositeRadius <= 0.0)
            {
                plan.Status =
                    "INVALID APSIS RADIUS";

                plan.Evidence.Add(
                    "Current or requested apsis altitude does not produce a valid Kerbin orbital radius.");

                return plan;
            }

            double currentSpeedTerm =
                VisVivaTerm(
                    nodeRadius,
                    currentSemiMajorAxis);

            if (!IsFinite(currentSpeedTerm) ||
                currentSpeedTerm <= 0.0)
            {
                plan.Status =
                    "NON-BOUND ORBIT";

                plan.Evidence.Add(
                    "Current orbital elements do not produce a valid speed at the maneuver apsis.");

                return plan;
            }

            double desiredSemiMajorAxis =
                (nodeRadius +
                 oppositeRadius) /
                2.0;

            if (!IsFinite(
                    desiredSemiMajorAxis) ||
                desiredSemiMajorAxis <= 0.0)
            {
                plan.Status =
                    "INVALID TARGET ORBIT";

                plan.Evidence.Add(
                    "Requested apsides do not produce a valid target semi-major axis.");

                return plan;
            }

            double desiredSpeedTerm =
                VisVivaTerm(
                    nodeRadius,
                    desiredSemiMajorAxis);

            if (!IsFinite(desiredSpeedTerm) ||
                desiredSpeedTerm <= 0.0)
            {
                plan.Status =
                    "INVALID TARGET ORBIT";

                plan.Evidence.Add(
                    "Requested target orbit does not produce a valid speed at the maneuver point.");

                return plan;
            }

            double currentSpeed =
                Math.Sqrt(
                    currentSpeedTerm);

            double desiredSpeed =
                Math.Sqrt(
                    desiredSpeedTerm);

            double signedProgradeDeltaV =
                desiredSpeed -
                currentSpeed;

            double totalDeltaV =
                Math.Abs(
                    signedProgradeDeltaV);

            if (!IsFinite(
                    signedProgradeDeltaV))
            {
                plan.Status =
                    "INVALID DELTA V";

                plan.Evidence.Add(
                    "Apsis maneuver delta-v calculation produced a non-finite result.");

                return plan;
            }

            double burnDuration =
                EstimateBurnDuration(
                    current,
                    totalDeltaV);

            if (!IsFinite(
                    burnDuration))
            {
                plan.Status =
                    "BURN ESTIMATE UNAVAILABLE";

                plan.Evidence.Add(
                    "Mass/thrust telemetry cannot produce a burn-duration estimate.");

                return plan;
            }

            double nodeMissionTime =
                current.MissionTimeSeconds +
                timeToNode;

            double ignitionLead =
                burnDuration /
                2.0;

            double ignitionMissionTime =
                nodeMissionTime -
                ignitionLead;

            double predictedApoapsis =
                nodeAtApoapsis
                    ? nodeAltitude
                    : oppositeAltitude;

            double predictedPeriapsis =
                nodeAtApoapsis
                    ? oppositeAltitude
                    : nodeAltitude;

            double apoapsisRadius =
                KerbinRadiusMeters +
                predictedApoapsis;

            double periapsisRadius =
                KerbinRadiusMeters +
                predictedPeriapsis;

            double predictedEccentricity =
                Math.Abs(
                    apoapsisRadius -
                    periapsisRadius) /
                (apoapsisRadius +
                 periapsisRadius);

            double predictedPeriod =
                2.0 *
                Math.PI *
                Math.Sqrt(
                    desiredSemiMajorAxis *
                    desiredSemiMajorAxis *
                    desiredSemiMajorAxis /
                    KerbinGravitationalParameter);

            plan.Available =
                true;

            plan.NodeUniversalTimeAvailable =
                false;

            plan.NodeUniversalTimeSeconds =
                double.NaN;

            plan.NodeMissionTimeSeconds =
                nodeMissionTime;

            if (IsUsableEpoch(
                    epoch,
                    current))
            {
                plan.VesselId =
                    epoch.VesselId ??
                    string.Empty;

                plan.NodeUniversalTimeAvailable =
                    true;

                plan.NodeUniversalTimeSeconds =
                    epoch.UniversalTimeSeconds +
                    timeToNode;
            }

            plan.TimeToNodeSeconds =
                timeToNode;

            plan.ProgradeDeltaVMetersPerSecond =
                signedProgradeDeltaV;

            plan.NormalDeltaVMetersPerSecond =
                0.0;

            plan.RadialDeltaVMetersPerSecond =
                0.0;

            plan.TotalDeltaVMetersPerSecond =
                totalDeltaV;

            plan.EstimatedBurnDurationSeconds =
                burnDuration;

            plan.IgnitionLeadSeconds =
                ignitionLead;

            plan.IgnitionMissionTimeSeconds =
                ignitionMissionTime;

            plan.PredictedApoapsisMeters =
                predictedApoapsis;

            plan.PredictedPeriapsisMeters =
                predictedPeriapsis;

            plan.PredictedInclinationDegrees =
                current.InclinationDegrees;

            plan.PredictedEccentricity =
                predictedEccentricity;

            plan.PredictedPeriodSeconds =
                predictedPeriod;

            plan.Status =
                totalDeltaV <=
                    MinimumUsefulDeltaV
                    ? "TARGET ORBIT ALREADY SATISFIED"
                    : "PLAN VALID";

            plan.Evidence.Add(
                "Objective solved from Engine-owned ORBIT telemetry.");

            if (plan.NodeUniversalTimeAvailable)
            {
                plan.Evidence.Add(
                    "Node epoch uses genuine KSP Universal Time from KMC-EPOCH1 plus Engine time-to-apsis.");
            }
            else
            {
                plan.Evidence.Add(
                    "KSP Universal Time side-channel unavailable or not matched to the current vessel; uplink is inhibited.");
            }

            plan.Evidence.Add(
                nodeAtApoapsis
                    ? "Maneuver node is the next apoapsis."
                    : "Maneuver node is the next periapsis.");

            plan.Evidence.Add(
                "Signed prograde delta-v is solved by vis-viva at the maneuver apsis; negative values are retrograde.");

            plan.Evidence.Add(
                "Burn duration uses absolute delta-v, vessel mass, maximum thrust, and average specific impulse.");

            plan.Evidence.Add(
                "Predicted AP/PE preserve the maneuver-point apsis and set the requested opposite apsis.");

            if (effectiveRequest.Type !=
                ManeuverRequestType.CircularizeAtApoapsis)
            {
                plan.Evidence.Add(
                    "Build 13.2 supports signed prograde/retrograde apsis execution through verified GUID/FDAI guidance.");
            }

            return plan;
        }

        private static bool ValidateTargetPeriapsis(
            OrbitTelemetryState current,
            double targetAltitude,
            ManeuverPlanModel plan)
        {
            if (!IsFinite(targetAltitude))
            {
                plan.Status =
                    "TARGET PERIAPSIS REQUIRED";

                plan.Evidence.Add(
                    "Set-periapsis requests require a finite target altitude.");

                return false;
            }

            if (targetAltitude <=
                -KerbinRadiusMeters)
            {
                plan.Status =
                    "INVALID TARGET PERIAPSIS";

                plan.Evidence.Add(
                    "Target periapsis does not produce a positive orbital radius.");

                return false;
            }

            if (!IsFinite(
                    current.ApoapsisMeters) ||
                targetAltitude >
                    current.ApoapsisMeters)
            {
                plan.Status =
                    "TARGET ABOVE NODE APSIS";

                plan.Evidence.Add(
                    "For SET PERIAPSIS AT APOAPSIS, target periapsis cannot exceed the maneuver-point apoapsis altitude.");

                return false;
            }

            return true;
        }

        private static bool ValidateTargetApoapsis(
            OrbitTelemetryState current,
            double targetAltitude,
            ManeuverPlanModel plan)
        {
            if (!IsFinite(targetAltitude))
            {
                plan.Status =
                    "TARGET APOAPSIS REQUIRED";

                plan.Evidence.Add(
                    "Set-apoapsis requests require a finite target altitude.");

                return false;
            }

            if (targetAltitude <=
                -KerbinRadiusMeters)
            {
                plan.Status =
                    "INVALID TARGET APOAPSIS";

                plan.Evidence.Add(
                    "Target apoapsis does not produce a positive orbital radius.");

                return false;
            }

            if (!IsFinite(
                    current.PeriapsisMeters) ||
                targetAltitude <
                    current.PeriapsisMeters)
            {
                plan.Status =
                    "TARGET BELOW NODE APSIS";

                plan.Evidence.Add(
                    "For SET APOAPSIS AT PERIAPSIS, target apoapsis cannot be below the maneuver-point periapsis altitude.");

                return false;
            }

            return true;
        }

        private static string BuildObjective(
            ManeuverRequestModel request)
        {
            if (request == null)
            {
                return
                    "CIRCULARIZE AT APOAPSIS";
            }

            switch (request.Type)
            {
                case ManeuverRequestType.SetPeriapsisAtApoapsis:
                    return
                        IsFinite(
                            request.TargetAltitudeMeters)
                            ? "SET PERIAPSIS " +
                              FormatKilometers(
                                  request.TargetAltitudeMeters) +
                              " AT APOAPSIS"
                            : "SET PERIAPSIS AT APOAPSIS";

                case ManeuverRequestType.SetApoapsisAtPeriapsis:
                    return
                        IsFinite(
                            request.TargetAltitudeMeters)
                            ? "SET APOAPSIS " +
                              FormatKilometers(
                                  request.TargetAltitudeMeters) +
                              " AT PERIAPSIS"
                            : "SET APOAPSIS AT PERIAPSIS";

                default:
                    return
                        "CIRCULARIZE AT APOAPSIS";
            }
        }

        private static string FormatKilometers(
            double meters)
        {
            return
                (meters / 1000.0)
                .ToString("0.0") +
                " KM";
        }

        private static double ResolveCurrentSemiMajorAxis(
            OrbitTelemetryState current)
        {
            if (current != null &&
                IsFinite(
                    current.SemiMajorAxisMeters) &&
                current.SemiMajorAxisMeters >
                    0.0)
            {
                return
                    current.SemiMajorAxisMeters;
            }

            if (current == null ||
                !IsFinite(
                    current.ApoapsisMeters) ||
                !IsFinite(
                    current.PeriapsisMeters))
            {
                return double.NaN;
            }

            double apoapsisRadius =
                KerbinRadiusMeters +
                current.ApoapsisMeters;

            double periapsisRadius =
                KerbinRadiusMeters +
                current.PeriapsisMeters;

            if (apoapsisRadius <= 0.0 ||
                periapsisRadius <= 0.0)
            {
                return double.NaN;
            }

            return
                (apoapsisRadius +
                 periapsisRadius) /
                2.0;
        }

        private static double VisVivaTerm(
            double radius,
            double semiMajorAxis)
        {
            if (!IsFinite(radius) ||
                !IsFinite(semiMajorAxis) ||
                radius <= 0.0 ||
                semiMajorAxis <= 0.0)
            {
                return double.NaN;
            }

            return
                KerbinGravitationalParameter *
                ((2.0 / radius) -
                 (1.0 / semiMajorAxis));
        }

        private static bool IsUsableEpoch(
            ManeuverEpochTelemetryModel epoch,
            OrbitTelemetryState current)
        {
            if (epoch == null ||
                !epoch.Available ||
                current == null ||
                string.IsNullOrWhiteSpace(
                    epoch.VesselId) ||
                !IsFinite(
                    epoch.UniversalTimeSeconds) ||
                !IsFinite(
                    epoch.MissionTimeSeconds))
            {
                return false;
            }

            if (!string.Equals(
                    epoch.VesselName ??
                    string.Empty,
                    current.VesselName ??
                    string.Empty,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (Math.Abs(
                    epoch.MissionTimeSeconds -
                    current.MissionTimeSeconds) >
                2.0)
            {
                return false;
            }

            return true;
        }

        private static double EstimateBurnDuration(
            OrbitTelemetryState current,
            double deltaV)
        {
            if (deltaV <=
                MinimumUsefulDeltaV)
            {
                return 0.0;
            }

            double massKilograms =
                Math.Max(
                    0.0,
                    current.VesselMassTonnes) *
                1000.0;

            double thrustNewtons =
                Math.Max(
                    0.0,
                    current.MaximumThrustKilonewtons) *
                1000.0;

            double specificImpulse =
                current.AverageSpecificImpulseSeconds;

            if (massKilograms <= 0.0 ||
                thrustNewtons <= 0.0)
            {
                return double.NaN;
            }

            if (specificImpulse > 1.0 &&
                IsFinite(
                    specificImpulse))
            {
                double exhaustVelocity =
                    specificImpulse *
                    StandardGravity;

                double finalMass =
                    massKilograms /
                    Math.Exp(
                        deltaV /
                        exhaustVelocity);

                double propellantMass =
                    Math.Max(
                        0.0,
                        massKilograms -
                        finalMass);

                double massFlow =
                    thrustNewtons /
                    exhaustVelocity;

                if (massFlow > 0.0 &&
                    IsFinite(
                        massFlow))
                {
                    return
                        propellantMass /
                        massFlow;
                }
            }

            double acceleration =
                thrustNewtons /
                massKilograms;

            return
                acceleration > 0.0
                    ? deltaV /
                      acceleration
                    : double.NaN;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(
                    value) &&
                !double.IsInfinity(
                    value);
        }
    }
}
