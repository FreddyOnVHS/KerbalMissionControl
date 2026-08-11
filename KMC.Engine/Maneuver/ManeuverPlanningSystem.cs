using System;
using System.Diagnostics;
using KMC.Engine.Orbit;

namespace KMC.Engine.Maneuver
{
    /// <summary>
    /// Stateful Engine owner for maneuver planning.
    ///
    /// Build 13.0 introduces an Engine-owned maneuver request and generalized
    /// apsis planner while retaining Build 12.3.3 immutable maneuver identity
    /// anchoring through time warp.
    ///
    /// The default request remains CIRCULARIZE AT APOAPSIS.
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

        private readonly ApsisManeuverPlanner _planner =
            new ApsisManeuverPlanner();

        private ManeuverPlanModel _latest =
            new ManeuverPlanModel();

        private string _activePlanId =
            string.Empty;

        private string _activeVesselId =
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

        public void SetRequest(
            ManeuverRequestModel request)
        {
            ManeuverRequestStore.Set(
                request);
        }

        public ManeuverRequestModel GetRequest()
        {
            return
                ManeuverRequestStore.Get();
        }

        public void Update(
            OrbitModel orbit,
            ManeuverEpochTelemetryModel epoch,
            DateTime receivedUtc)
        {
            ManeuverRequestModel request =
                ManeuverRequestStore.Get();

            ManeuverPlanModel next =
                _planner.Calculate(
                    orbit,
                    epoch,
                    request);

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
                request,
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
            string candidateVesselId =
                plan.VesselId ??
                string.Empty;

            string candidateVesselName =
                orbit != null &&
                orbit.Current != null
                    ? orbit.Current.VesselName ??
                      string.Empty
                    : string.Empty;

            string objective =
                plan.Objective ??
                string.Empty;

            /*
             * Build 13.0.1:
             *
             * Vessel ID and vessel name are different identity domains.
             * Never compare a GUID-like KSP vessel ID to the display name.
             *
             * KMC-EPOCH1 may briefly be unavailable during high-rate time warp.
             * In that case plan.VesselId becomes empty, but ORBIT still knows
             * the active vessel name. A temporary epoch dropout must not create
             * a new maneuver identity.
             */
            bool vesselMatches;

            if (!string.IsNullOrWhiteSpace(
                    candidateVesselId) &&
                !string.IsNullOrWhiteSpace(
                    _activeVesselId))
            {
                vesselMatches =
                    string.Equals(
                        candidateVesselId,
                        _activeVesselId,
                        StringComparison.Ordinal);
            }
            else
            {
                vesselMatches =
                    !string.IsNullOrWhiteSpace(
                        candidateVesselName) &&
                    !string.IsNullOrWhiteSpace(
                        _activeVesselName) &&
                    string.Equals(
                        candidateVesselName,
                        _activeVesselName,
                        StringComparison.Ordinal);
            }

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
             * Preserve the Build 12.3.3 immutable epoch anchor.
             *
             * Prefer genuine UT when both the candidate and anchor have UT.
             * If the epoch side-channel temporarily drops out, fall back to
             * the immutable Node MET anchor rather than declaring a new plan.
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

                _activeVesselId =
                    candidateVesselId;

                _activeVesselName =
                    candidateVesselName;

                _activeObjective =
                    objective;

                _activePlanId =
                    "MNV-13.0-" +
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
            else
            {
                /*
                 * If the plan was first established while the epoch channel
                 * was unavailable, adopt the genuine vessel ID and UT once
                 * they return. Do not change PlanId.
                 */
                if (string.IsNullOrWhiteSpace(
                        _activeVesselId) &&
                    !string.IsNullOrWhiteSpace(
                        candidateVesselId))
                {
                    _activeVesselId =
                        candidateVesselId;
                }

                if (string.IsNullOrWhiteSpace(
                        _activeVesselName) &&
                    !string.IsNullOrWhiteSpace(
                        candidateVesselName))
                {
                    _activeVesselName =
                        candidateVesselName;
                }

                if (!IsFinite(
                        _activeNodeUtAnchor) &&
                    plan.NodeUniversalTimeAvailable &&
                    IsFinite(
                        plan.NodeUniversalTimeSeconds))
                {
                    _activeNodeUtAnchor =
                        plan.NodeUniversalTimeSeconds;
                }
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
            ManeuverRequestModel request,
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
                " | Request=" +
                (request != null
                    ? request.Type.ToString()
                    : "DEFAULT") +
                " | TargetAlt=" +
                Format(
                    request != null
                        ? request.TargetAltitudeMeters
                        : double.NaN,
                    "0") + "m" +
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
