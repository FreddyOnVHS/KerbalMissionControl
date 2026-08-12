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

        /*
         * Build 13.5.5:
         * Plan sequence numbers restart when Mission Control restarts.
         * Add a process-session token so a new maneuver cannot reuse the
         * exact PlanId of stale KSP maneuver-node history from an earlier
         * Mission Control session.
         *
         * Example:
         *   MNV-13.0-A7C2F914D31B-0001
         *
         * The sequence remains human-readable while the session token keeps
         * identity ownership distinct across process restarts.
         */
        private static readonly string PlanSessionId =
            CreatePlanSessionId();

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

        private DateTime _activeRequestUtc =
            DateTime.MinValue;

        private ManeuverPlanModel _activePlanSnapshot;

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

            bool requestChanged =
                HasRequestChanged(
                    request);

            if (requestChanged)
            {
                ResetActivePlanIdentity();

                _activeRequestUtc =
                    request != null
                        ? request.RequestedUtc
                        : DateTime.MinValue;
            }

            ManeuverPlanModel candidate =
                _planner.Calculate(
                    orbit,
                    epoch,
                    request);

            ManeuverPlanModel next =
                candidate;

            if (candidate.Available)
            {
                bool activePlanExists =
                    _activePlanSnapshot != null &&
                    !string.IsNullOrWhiteSpace(
                        _activePlanId);

                bool manualEpochAnchored =
                    request != null &&
                    (request.Type ==
                         ManeuverRequestType.ManualProgradeRetrograde ||
                     request.Type ==
                         ManeuverRequestType.ManualNormalAntiNormal ||
                     request.Type ==
                         ManeuverRequestType.ManualRadialInOut) &&
                    IsFinite(
                        _activeNodeUtAnchor);

                if (activePlanExists &&
                    !requestChanged &&
                    manualEpochAnchored)
                {
                    /*
                     * Build 13.3:
                     * Manual T+ is relative to the COMPUTE event, not a
                     * continuously moving "now". Once genuine UT is anchored,
                     * hold that exact node epoch until crew re-COMPUTEs.
                     */
                    next =
                        BuildHeldActivePlan(
                            orbit,
                            epoch);
                }
                else if (activePlanExists &&
                         !requestChanged &&
                         !CandidateMatchesActivePlan(
                             candidate,
                             orbit))
                {
                    /*
                     * Build 13.2.1:
                     * Do not silently roll a reviewed maneuver forward to the
                     * next apsis / next orbit. Hold the original accepted
                     * maneuver until the crew explicitly presses COMPUTE.
                     */
                    next =
                        BuildHeldActivePlan(
                            orbit,
                            epoch);
                }
                else
                {
                    AssignStablePlanId(
                        candidate,
                        orbit);

                    _activePlanSnapshot =
                        ManeuverPlanModel.Clone(
                            candidate);

                    next =
                        candidate;
                }
            }
            else if (_activePlanSnapshot != null &&
                     !requestChanged &&
                     CurrentVesselMatchesActivePlan(
                         orbit))
            {
                /*
                 * Preserve the reviewed plan through transient planner
                 * unavailability. The held plan remains tied to its original
                 * immutable node epoch.
                 */
                next =
                    BuildHeldActivePlan(
                        orbit,
                        epoch);
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

        private bool HasRequestChanged(
            ManeuverRequestModel request)
        {
            DateTime requestedUtc =
                request != null
                    ? request.RequestedUtc
                    : DateTime.MinValue;

            return
                requestedUtc !=
                _activeRequestUtc;
        }

        private void ResetActivePlanIdentity()
        {
            _activePlanId =
                string.Empty;

            _activeVesselId =
                string.Empty;

            _activeVesselName =
                string.Empty;

            _activeObjective =
                string.Empty;

            _activeNodeUtAnchor =
                double.NaN;

            _activeNodeMissionTimeAnchor =
                double.NaN;

            _activeProgradeDeltaV =
                double.NaN;

            _activeNormalDeltaV =
                double.NaN;

            _activeRadialDeltaV =
                double.NaN;

            _activePlanSnapshot =
                null;
        }

        private bool CandidateMatchesActivePlan(
            ManeuverPlanModel plan,
            OrbitModel orbit)
        {
            if (plan == null ||
                string.IsNullOrWhiteSpace(
                    _activePlanId))
            {
                return false;
            }

            string candidateVesselId =
                plan.VesselId ??
                string.Empty;

            string candidateVesselName =
                orbit != null &&
                orbit.Current != null
                    ? orbit.Current.VesselName ??
                      string.Empty
                    : string.Empty;

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
                    plan.Objective ??
                    string.Empty,
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

            return
                vesselMatches &&
                objectiveMatches &&
                deltaVMatches &&
                epochMatches;
        }

        private bool CurrentVesselMatchesActivePlan(
            OrbitModel orbit)
        {
            string currentVesselName =
                orbit != null &&
                orbit.Current != null
                    ? orbit.Current.VesselName ??
                      string.Empty
                    : string.Empty;

            return
                !string.IsNullOrWhiteSpace(
                    currentVesselName) &&
                !string.IsNullOrWhiteSpace(
                    _activeVesselName) &&
                string.Equals(
                    currentVesselName,
                    _activeVesselName,
                    StringComparison.Ordinal);
        }

        private ManeuverPlanModel BuildHeldActivePlan(
            OrbitModel orbit,
            ManeuverEpochTelemetryModel epoch)
        {
            ManeuverPlanModel held =
                ManeuverPlanModel.Clone(
                    _activePlanSnapshot);

            if (held == null)
            {
                return
                    new ManeuverPlanModel();
            }

            held.PlanId =
                _activePlanId;

            if (IsFinite(
                    _activeNodeUtAnchor))
            {
                held.NodeUniversalTimeAvailable =
                    true;

                held.NodeUniversalTimeSeconds =
                    _activeNodeUtAnchor;
            }

            if (IsFinite(
                    _activeNodeMissionTimeAnchor))
            {
                held.NodeMissionTimeSeconds =
                    _activeNodeMissionTimeAnchor;

                if (IsFinite(
                        held.IgnitionLeadSeconds))
                {
                    held.IgnitionMissionTimeSeconds =
                        _activeNodeMissionTimeAnchor -
                        held.IgnitionLeadSeconds;
                }
            }

            double timeToNode =
                ResolveHeldTimeToNode(
                    orbit,
                    epoch);

            held.TimeToNodeSeconds =
                timeToNode;

            if (IsFinite(
                    timeToNode) &&
                timeToNode < 0.0)
            {
                held.Available =
                    false;

                held.Status =
                    "MANEUVER WINDOW MISSED";

                AddEvidenceOnce(
                    held,
                    "Original reviewed maneuver epoch has passed; plan is held and cannot roll forward automatically.");

                AddEvidenceOnce(
                    held,
                    "Press COMPUTE to generate and review a new maneuver at the next valid apsis.");
            }

            return held;
        }

        private double ResolveHeldTimeToNode(
            OrbitModel orbit,
            ManeuverEpochTelemetryModel epoch)
        {
            if (IsFinite(
                    _activeNodeUtAnchor) &&
                epoch != null &&
                epoch.Available &&
                IsFinite(
                    epoch.UniversalTimeSeconds))
            {
                bool vesselMatches =
                    string.IsNullOrWhiteSpace(
                        _activeVesselId) ||
                    string.IsNullOrWhiteSpace(
                        epoch.VesselId) ||
                    string.Equals(
                        epoch.VesselId,
                        _activeVesselId,
                        StringComparison.Ordinal);

                if (vesselMatches)
                {
                    return
                        _activeNodeUtAnchor -
                        epoch.UniversalTimeSeconds;
                }
            }

            if (IsFinite(
                    _activeNodeMissionTimeAnchor) &&
                orbit != null &&
                orbit.Current != null &&
                IsFinite(
                    orbit.Current.MissionTimeSeconds))
            {
                return
                    _activeNodeMissionTimeAnchor -
                    orbit.Current.MissionTimeSeconds;
            }

            return double.NaN;
        }

        private static void AddEvidenceOnce(
            ManeuverPlanModel plan,
            string evidence)
        {
            if (plan == null ||
                plan.Evidence == null ||
                string.IsNullOrWhiteSpace(
                    evidence))
            {
                return;
            }

            for (int index = 0;
                 index < plan.Evidence.Count;
                 index++)
            {
                if (string.Equals(
                        plan.Evidence[index],
                        evidence,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }

            plan.Evidence.Add(
                evidence);
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
                    PlanSessionId +
                    "-" +
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

        private static string CreatePlanSessionId()
        {
            /*
             * Twelve hexadecimal characters provide a compact 48-bit
             * process-session identity. This is generated once per Mission
             * Control process and shared by every ManeuverPlanningSystem
             * instance in that process.
             */
            return
                Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 12)
                    .ToUpperInvariant();
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
