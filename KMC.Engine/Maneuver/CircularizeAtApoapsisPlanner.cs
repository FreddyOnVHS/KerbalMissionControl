using System;
using KMC.Engine.Orbit;

namespace KMC.Engine.Maneuver
{
    /// <summary>
    /// Solves the Build 11.0 maneuver objective: circularize the current bound
    /// Kerbin orbit at its next apoapsis. This planner consumes ORBIT state and
    /// owns all maneuver-specific calculations.
    /// </summary>
    internal sealed class CircularizeAtApoapsisPlanner
    {
        private const double KerbinRadiusMeters = 600000.0;
        private const double KerbinGravitationalParameter = 3.5316e12;
        private const double StandardGravity = 9.80665;
        private const double MinimumUsefulDeltaV = 0.05;

        public ManeuverPlanModel Calculate(OrbitModel orbit)
        {
            ManeuverPlanModel plan = new ManeuverPlanModel
            {
                Objective = "CIRCULARIZE AT APOAPSIS",
                Status = "PLAN UNAVAILABLE"
            };

            if (orbit == null || !orbit.Available || orbit.Current == null || !orbit.Current.Available)
            {
                plan.Evidence.Add("ORBIT foundation unavailable.");
                return plan;
            }

            OrbitTelemetryState current = orbit.Current;

            if (!string.Equals(current.BodyName, "Kerbin", StringComparison.OrdinalIgnoreCase))
            {
                plan.Status = "UNSUPPORTED CENTRAL BODY";
                plan.Evidence.Add("Build 11.0 constants are validated for Kerbin only.");
                return plan;
            }

            if (!IsFinite(current.MissionTimeSeconds) ||
                !IsFinite(current.TimeToApoapsisSeconds) ||
                current.TimeToApoapsisSeconds < 0.0)
            {
                plan.Status = "INVALID MANEUVER TIME";
                plan.Evidence.Add("MET or time-to-apoapsis telemetry is invalid.");
                return plan;
            }

            if (!IsFinite(current.ApoapsisMeters) || current.ApoapsisMeters <= -KerbinRadiusMeters)
            {
                plan.Status = "INVALID APOAPSIS";
                plan.Evidence.Add("Apoapsis telemetry does not produce a valid orbital radius.");
                return plan;
            }

            double apoapsisRadius = KerbinRadiusMeters + current.ApoapsisMeters;
            double semiMajorAxis = ResolveSemiMajorAxis(current, apoapsisRadius);

            if (!IsFinite(semiMajorAxis) || semiMajorAxis <= 0.0)
            {
                plan.Status = "INVALID SEMI-MAJOR AXIS";
                plan.Evidence.Add("No valid semi-major axis can be established from ORBIT telemetry.");
                return plan;
            }

            double visVivaTerm =
                KerbinGravitationalParameter *
                ((2.0 / apoapsisRadius) - (1.0 / semiMajorAxis));

            if (!IsFinite(visVivaTerm) || visVivaTerm <= 0.0)
            {
                plan.Status = "NON-BOUND ORBIT";
                plan.Evidence.Add("Current orbital elements do not describe a supported bound ellipse.");
                return plan;
            }

            double speedAtApoapsis = Math.Sqrt(visVivaTerm);
            double circularSpeed = Math.Sqrt(KerbinGravitationalParameter / apoapsisRadius);
            double progradeDeltaV = circularSpeed - speedAtApoapsis;

            if (!IsFinite(progradeDeltaV))
            {
                plan.Status = "INVALID DELTA V";
                plan.Evidence.Add("Circularization delta-v calculation produced a non-finite result.");
                return plan;
            }

            if (progradeDeltaV < -MinimumUsefulDeltaV)
            {
                plan.Status = "APOAPSIS STATE INCONSISTENT";
                plan.Evidence.Add("Computed apoapsis speed exceeds circular speed; no prograde apoapsis circularization is valid.");
                return plan;
            }

            progradeDeltaV = Math.Max(0.0, progradeDeltaV);

            double burnDuration = EstimateBurnDuration(current, progradeDeltaV);

            if (!IsFinite(burnDuration))
            {
                plan.Status = "BURN ESTIMATE UNAVAILABLE";
                plan.Evidence.Add("Mass/thrust telemetry cannot produce a burn-duration estimate.");
                return plan;
            }

            double nodeMissionTime = current.MissionTimeSeconds + current.TimeToApoapsisSeconds;
            double ignitionLead = burnDuration / 2.0;
            double ignitionMissionTime = nodeMissionTime - ignitionLead;
            double circularPeriod =
                2.0 * Math.PI *
                Math.Sqrt(
                    apoapsisRadius * apoapsisRadius * apoapsisRadius /
                    KerbinGravitationalParameter);

            plan.Available = true;
            plan.NodeUniversalTimeAvailable = false;
            plan.NodeUniversalTimeSeconds = double.NaN;
            plan.NodeMissionTimeSeconds = nodeMissionTime;
            plan.TimeToNodeSeconds = current.TimeToApoapsisSeconds;
            plan.ProgradeDeltaVMetersPerSecond = progradeDeltaV;
            plan.NormalDeltaVMetersPerSecond = 0.0;
            plan.RadialDeltaVMetersPerSecond = 0.0;
            plan.TotalDeltaVMetersPerSecond = progradeDeltaV;
            plan.EstimatedBurnDurationSeconds = burnDuration;
            plan.IgnitionLeadSeconds = ignitionLead;
            plan.IgnitionMissionTimeSeconds = ignitionMissionTime;
            plan.PredictedApoapsisMeters = current.ApoapsisMeters;
            plan.PredictedPeriapsisMeters = current.ApoapsisMeters;
            plan.PredictedInclinationDegrees = current.InclinationDegrees;
            plan.PredictedEccentricity = 0.0;
            plan.PredictedPeriodSeconds = circularPeriod;
            plan.Status = progradeDeltaV <= MinimumUsefulDeltaV
                ? "ALREADY CIRCULAR AT APOAPSIS"
                : "PLAN VALID";

            plan.Evidence.Add("Objective solved from Engine-owned ORBIT telemetry.");
            plan.Evidence.Add("Node epoch uses MET + time-to-apoapsis; KSP Universal Time is not present in KMC6 telemetry.");
            plan.Evidence.Add("Delta-v uses Kerbin vis-viva solution at the next apoapsis.");
            plan.Evidence.Add("Burn duration uses vessel mass, maximum thrust, and average specific impulse from flight telemetry.");
            plan.Evidence.Add("Predicted circular orbit preserves current apoapsis radius and inclination.");

            return plan;
        }

        private static double ResolveSemiMajorAxis(
            OrbitTelemetryState current,
            double apoapsisRadius)
        {
            if (IsFinite(current.SemiMajorAxisMeters) && current.SemiMajorAxisMeters > 0.0)
            {
                return current.SemiMajorAxisMeters;
            }

            if (IsFinite(current.PeriapsisMeters))
            {
                double periapsisRadius = KerbinRadiusMeters + current.PeriapsisMeters;

                if (periapsisRadius > 0.0)
                {
                    return (apoapsisRadius + periapsisRadius) / 2.0;
                }
            }

            return double.NaN;
        }

        private static double EstimateBurnDuration(
            OrbitTelemetryState current,
            double deltaV)
        {
            if (deltaV <= MinimumUsefulDeltaV)
            {
                return 0.0;
            }

            double massKilograms = Math.Max(0.0, current.VesselMassTonnes) * 1000.0;
            double thrustNewtons = Math.Max(0.0, current.MaximumThrustKilonewtons) * 1000.0;
            double specificImpulse = current.AverageSpecificImpulseSeconds;

            if (massKilograms <= 0.0 || thrustNewtons <= 0.0)
            {
                return double.NaN;
            }

            if (specificImpulse > 1.0 && IsFinite(specificImpulse))
            {
                double exhaustVelocity = specificImpulse * StandardGravity;
                double finalMass = massKilograms / Math.Exp(deltaV / exhaustVelocity);
                double propellantMass = Math.Max(0.0, massKilograms - finalMass);
                double massFlow = thrustNewtons / exhaustVelocity;

                if (massFlow > 0.0 && IsFinite(massFlow))
                {
                    return propellantMass / massFlow;
                }
            }

            double acceleration = thrustNewtons / massKilograms;
            return acceleration > 0.0 ? deltaV / acceleration : double.NaN;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
