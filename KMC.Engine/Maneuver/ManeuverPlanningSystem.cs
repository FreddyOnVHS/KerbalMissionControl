using System;
using System.Diagnostics;
using KMC.Engine.Orbit;

namespace KMC.Engine.Maneuver
{
    /// <summary>
    /// Stateful Engine owner for maneuver planning. Build 11.0 exposes exactly
    /// one objective: circularization at the next apoapsis.
    ///
    /// Build 12.3.2:
    /// Stable maneuver identity is based on genuine Node UT whenever available.
    /// This prevents time warp / sparse packet timing from creating a new
    /// PlanId for the same physical maneuver.
    /// </summary>
    public sealed class ManeuverPlanningSystem
    {
        private const double NodeUtAnchorToleranceSeconds =
            5.0;

        private const double NodeMetFallbackToleranceSeconds =
            5.0;

        private const double DeltaVIdentityToleranceMetersPerSecond =
            0.25;

        private readonly object _syncRoot =
            new object();

        private readonly CircularizeAtApoapsisPlanner _planner =
            new CircularizeAtApoapsisPlanner();

        private ManeuverPlanModel _latest =
            new ManeuverPlanModel();

        private string _activePlanId =
            string.Empty;

        private string _activeVesselName =
            string.Empty;

        private string _activeObjective =
            string.Empty;

        private double _activeNodeUtAnchor =
            double.NaN;

        private double _activeNodeMissionTimeAnchor =
            double.NaN;

        private double _activeProgradeDeltaV =
            double.NaN;

        private double _activeNormalDeltaV =
            double.NaN;

        private double _activeRadialDeltaV =
            double.NaN;

        private int _planSequence;
        private DateTime _lastDiagnosticUtc =
            DateTime.MinValue;

        public void Update(
            OrbitModel orbit,
            ManeuverEpochTelemetryModel epoch,
            DateTime receivedUtc)
        {
            ManeuverPlanModel next =
                _planner.Calculate(
                    orbit,
                    epoch);

            if (next.Available)
            {
                AssignStablePlanId(
                    next,
                    orbit);
            }

            lock (_syncRoot)
            {
                _latest =
                    next;
            }

            WriteDiagnosticIfDue(
                next,
                receivedUtc);
        }

        public ManeuverPlanModel GetLatest()
        {
            lock (_syncRoot)
            {
                return
                    ManeuverPlanModel.Clone(
                        _latest);
            }
        }

        private void AssignStablePlanId(
            ManeuverPlanModel plan,
            OrbitModel orbit)
        {
            string vesselName =
                !string.IsNullOrWhiteSpace(
                    plan.VesselId)
                    ? plan.VesselId
                    : orbit != null &&
                      orbit.Current != null
                        ? orbit.Current.VesselName ??
                          string.Empty
                        : string.Empty;

            string objective =
                plan.Objective ??
                string.Empty;

            bool vesselMatches =
                string.Equals(
                    vesselName,
                    _activeVesselName,
                    StringComparison.Ordinal);

            bool objectiveMatches =
                string.Equals(
                    objective,
                    _activeObjective,
                    StringComparison.Ordinal);

            bool deltaVMatches =
                IsWithin(
                    plan.ProgradeDeltaVMetersPerSecond,
                    _activeProgradeDeltaV,
                    DeltaVIdentityToleranceMetersPerSecond) &&
                IsWithin(
                    plan.NormalDeltaVMetersPerSecond,
                    _activeNormalDeltaV,
                    DeltaVIdentityToleranceMetersPerSecond) &&
                IsWithin(
                    plan.RadialDeltaVMetersPerSecond,
                    _activeRadialDeltaV,
                    DeltaVIdentityToleranceMetersPerSecond);

            bool epochMatches =
                false;

            /*
             * Build 12.3.3:
             *
             * The first accepted maneuver epoch is an immutable identity
             * anchor. Do NOT move the anchor on every telemetry update.
             *
             * The KSP UT side channel and the ordinary orbital telemetry
             * packet are not sampled at exactly the same instant during
             * time warp. That can make the freshly calculated Node UT drift
             * by roughly a second even though the physical maneuver is the
             * same. Comparing every new sample against the PREVIOUS sample
             * allowed that error to walk the identity forward and eventually
             * create a new PlanId.
             *
             * Instead, every candidate is compared to the ORIGINAL anchor.
             */
            if (plan.NodeUniversalTimeAvailable &&
                IsFinite(
                    plan.NodeUniversalTimeSeconds) &&
                IsFinite(
                    _activeNodeUtAnchor))
            {
                epochMatches =
                    Math.Abs(
                        plan.NodeUniversalTimeSeconds -
                        _activeNodeUtAnchor) <=
                    NodeUtAnchorToleranceSeconds;
            }
            else if (IsFinite(
                         plan.NodeMissionTimeSeconds) &&
                     IsFinite(
                         _activeNodeMissionTimeAnchor))
            {
                epochMatches =
                    Math.Abs(
                        plan.NodeMissionTimeSeconds -
                        _activeNodeMissionTimeAnchor) <=
                    NodeMetFallbackToleranceSeconds;
            }

            bool samePlan =
                vesselMatches &&
                objectiveMatches &&
                deltaVMatches &&
                epochMatches &&
                !string.IsNullOrEmpty(
                    _activePlanId);

            if (!samePlan)
            {
                _planSequence++;

                _activeVesselName =
                    vesselName;

                _activeObjective =
                    objective;

                _activePlanId =
                    "MNV-11.2-" +
                    _planSequence.ToString(
                        "D4");

                _activeNodeUtAnchor =
                    plan.NodeUniversalTimeAvailable &&
                    IsFinite(
                        plan.NodeUniversalTimeSeconds)
                        ? plan.NodeUniversalTimeSeconds
                        : double.NaN;

                _activeNodeMissionTimeAnchor =
                    IsFinite(
                        plan.NodeMissionTimeSeconds)
                        ? plan.NodeMissionTimeSeconds
                        : double.NaN;

                _activeProgradeDeltaV =
                    plan.ProgradeDeltaVMetersPerSecond;

                _activeNormalDeltaV =
                    plan.NormalDeltaVMetersPerSecond;

                _activeRadialDeltaV =
                    plan.RadialDeltaVMetersPerSecond;
            }
            else if (!IsFinite(
                         _activeNodeUtAnchor) &&
                     plan.NodeUniversalTimeAvailable &&
                     IsFinite(
                         plan.NodeUniversalTimeSeconds))
            {
                /*
                 * A plan that was initially identified using the MET fallback
                 * may later receive genuine UT telemetry. Adopt UT once, then
                 * keep it fixed for the rest of this maneuver identity.
                 */
                _activeNodeUtAnchor =
                    plan.NodeUniversalTimeSeconds;
            }

            plan.PlanId =
                _activePlanId;
        }

        private static bool IsWithin(
            double actual,
            double reference,
            double tolerance)
        {
            return
                IsFinite(actual) &&
                IsFinite(reference) &&
                Math.Abs(
                    actual -
                    reference) <=
                tolerance;
        }

        private void WriteDiagnosticIfDue(
            ManeuverPlanModel plan,
            DateTime receivedUtc)
        {
            if (_lastDiagnosticUtc !=
                    DateTime.MinValue &&
                (receivedUtc -
                 _lastDiagnosticUtc)
                    .TotalSeconds < 1.0)
            {
                return;
            }

            _lastDiagnosticUtc =
                receivedUtc;

            Debug.WriteLine(
                "KMC.Engine MANEUVER PLAN" +
                " | Available=" + plan.Available +
                " | PlanId=" + plan.PlanId +
                " | Objective=" + plan.Objective +
                " | Status=" + plan.Status +
                " | NodeMET=" +
                Format(
                    plan.NodeMissionTimeSeconds,
                    "0.0") + "s" +
                " | NodeUTAvailable=" +
                plan.NodeUniversalTimeAvailable +
                " | NodeUT=" +
                Format(
                    plan.NodeUniversalTimeSeconds,
                    "0.0") + "s" +
                " | VesselId=" + plan.VesselId +
                " | TNode=" +
                Format(
                    plan.TimeToNodeSeconds,
                    "0.0") + "s" +
                " | ProgradeDV=" +
                Format(
                    plan.ProgradeDeltaVMetersPerSecond,
                    "0.00") + "m/s" +
                " | NormalDV=" +
                Format(
                    plan.NormalDeltaVMetersPerSecond,
                    "0.00") + "m/s" +
                " | RadialDV=" +
                Format(
                    plan.RadialDeltaVMetersPerSecond,
                    "0.00") + "m/s" +
                " | TotalDV=" +
                Format(
                    plan.TotalDeltaVMetersPerSecond,
                    "0.00") + "m/s" +
                " | Burn=" +
                Format(
                    plan.EstimatedBurnDurationSeconds,
                    "0.00") + "s" +
                " | IgnLead=" +
                Format(
                    plan.IgnitionLeadSeconds,
                    "0.00") + "s" +
                " | IgnMET=" +
                Format(
                    plan.IgnitionMissionTimeSeconds,
                    "0.0") + "s" +
                " | PredAp=" +
                Format(
                    plan.PredictedApoapsisMeters,
                    "0") + "m" +
                " | PredPe=" +
                Format(
                    plan.PredictedPeriapsisMeters,
                    "0") + "m" +
                " | PredInc=" +
                Format(
                    plan.PredictedInclinationDegrees,
                    "0.000") + "deg" +
                " | PredEcc=" +
                Format(
                    plan.PredictedEccentricity,
                    "0.000000") +
                " | PredPeriod=" +
                Format(
                    plan.PredictedPeriodSeconds,
                    "0.0") + "s");
        }

        private static string Format(
            double value,
            string format)
        {
            return
                IsFinite(value)
                    ? value.ToString(
                        format)
                    : "N/A";
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
