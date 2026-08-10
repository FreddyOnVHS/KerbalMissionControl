using System;
using System.Diagnostics;
using KMC.Engine.Orbit;

namespace KMC.Engine.Maneuver
{
    /// <summary>
    /// Stateful Engine owner for maneuver planning. Build 11.0 exposes exactly
    /// one objective: circularization at the next apoapsis.
    /// </summary>
    public sealed class ManeuverPlanningSystem
    {
        private readonly object _syncRoot = new object();
        private readonly CircularizeAtApoapsisPlanner _planner = new CircularizeAtApoapsisPlanner();

        private ManeuverPlanModel _latest = new ManeuverPlanModel();
        private string _activePlanId = string.Empty;
        private string _activeVesselName = string.Empty;
        private long _activeNodeSecond = long.MinValue;
        private int _planSequence;
        private DateTime _lastDiagnosticUtc = DateTime.MinValue;

        public void Update(OrbitModel orbit, DateTime receivedUtc)
        {
            ManeuverPlanModel next = _planner.Calculate(orbit);

            if (next.Available)
            {
                AssignStablePlanId(next, orbit);
            }

            lock (_syncRoot)
            {
                _latest = next;
            }

            WriteDiagnosticIfDue(next, receivedUtc);
        }

        public ManeuverPlanModel GetLatest()
        {
            lock (_syncRoot)
            {
                return ManeuverPlanModel.Clone(_latest);
            }
        }

        private void AssignStablePlanId(ManeuverPlanModel plan, OrbitModel orbit)
        {
            string vesselName =
                orbit != null && orbit.Current != null
                    ? orbit.Current.VesselName ?? string.Empty
                    : string.Empty;

            long nodeSecond =
                IsFinite(plan.NodeMissionTimeSeconds)
                    ? (long)Math.Round(plan.NodeMissionTimeSeconds)
                    : long.MinValue;

            bool samePlan =
                string.Equals(vesselName, _activeVesselName, StringComparison.Ordinal) &&
                _activeNodeSecond != long.MinValue &&
                nodeSecond != long.MinValue &&
                Math.Abs(nodeSecond - _activeNodeSecond) <= 2;

            if (!samePlan || string.IsNullOrEmpty(_activePlanId))
            {
                _planSequence++;
                _activeVesselName = vesselName;
                _activeNodeSecond = nodeSecond;
                _activePlanId = "MNV-11.0-" + _planSequence.ToString("D4");
            }
            else
            {
                _activeNodeSecond = nodeSecond;
            }

            plan.PlanId = _activePlanId;
        }

        private void WriteDiagnosticIfDue(ManeuverPlanModel plan, DateTime receivedUtc)
        {
            if (_lastDiagnosticUtc != DateTime.MinValue &&
                (receivedUtc - _lastDiagnosticUtc).TotalSeconds < 1.0)
            {
                return;
            }

            _lastDiagnosticUtc = receivedUtc;

            Debug.WriteLine(
                "KMC.Engine MANEUVER PLAN" +
                " | Available=" + plan.Available +
                " | PlanId=" + plan.PlanId +
                " | Objective=" + plan.Objective +
                " | Status=" + plan.Status +
                " | NodeMET=" + Format(plan.NodeMissionTimeSeconds, "0.0") + "s" +
                " | NodeUTAvailable=" + plan.NodeUniversalTimeAvailable +
                " | TNode=" + Format(plan.TimeToNodeSeconds, "0.0") + "s" +
                " | ProgradeDV=" + Format(plan.ProgradeDeltaVMetersPerSecond, "0.00") + "m/s" +
                " | NormalDV=" + Format(plan.NormalDeltaVMetersPerSecond, "0.00") + "m/s" +
                " | RadialDV=" + Format(plan.RadialDeltaVMetersPerSecond, "0.00") + "m/s" +
                " | TotalDV=" + Format(plan.TotalDeltaVMetersPerSecond, "0.00") + "m/s" +
                " | Burn=" + Format(plan.EstimatedBurnDurationSeconds, "0.00") + "s" +
                " | IgnLead=" + Format(plan.IgnitionLeadSeconds, "0.00") + "s" +
                " | IgnMET=" + Format(plan.IgnitionMissionTimeSeconds, "0.0") + "s" +
                " | PredAp=" + Format(plan.PredictedApoapsisMeters, "0") + "m" +
                " | PredPe=" + Format(plan.PredictedPeriapsisMeters, "0") + "m" +
                " | PredInc=" + Format(plan.PredictedInclinationDegrees, "0.000") + "deg" +
                " | PredEcc=" + Format(plan.PredictedEccentricity, "0.000000") +
                " | PredPeriod=" + Format(plan.PredictedPeriodSeconds, "0.0") + "s");
        }

        private static string Format(double value, string format)
        {
            return IsFinite(value) ? value.ToString(format) : "N/A";
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
