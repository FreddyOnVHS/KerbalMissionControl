using System;
using System.Collections.Generic;

namespace KMC.Engine.Propulsion
{
    public enum PropulsionEngineOperatingState
    {
        Unknown = 0,
        Armed,
        Ignited,
        Producing,
        Shutdown,
        Flameout
    }

    /// <summary>
    /// One raw per-engine telemetry observation keyed by KSP PartId.
    /// </summary>
    public sealed class PropulsionEngineTelemetryEntry
    {
        public uint PartId { get; set; }

        public PropulsionEngineOperatingState OperatingState
        {
            get;
            set;
        }

        public bool IsSolidBooster { get; set; }

        public double CurrentThrust { get; set; }

        /// <summary>
        /// Maximum thrust reported by the KSP engine module after its
        /// thrust-percentage limiter. This is configured/potential thrust,
        /// not proof that the engine can currently deliver it.
        /// </summary>
        public double MaximumThrust { get; set; }
    }

    /// <summary>
    /// Latest KMC-ENGINE1 packet supplied to KMC.Engine.
    /// </summary>
    public sealed class PropulsionTelemetryModel
    {
        public PropulsionTelemetryModel()
        {
            Entries =
                new List<PropulsionEngineTelemetryEntry>();
        }

        public bool TelemetryAvailable { get; set; }

        public DateTime SourceTimestampUtc { get; set; }

        public DateTime ReceivedUtc { get; set; }

        public List<PropulsionEngineTelemetryEntry> Entries
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Live state for one topology-owned engine after PartId matching.
    /// </summary>
    public sealed class PropulsionEngineLiveStateModel
    {
        public uint PartId { get; internal set; }

        public string PartTitle { get; internal set; }

        public bool TelemetryMatched { get; internal set; }

        public PropulsionEngineOperatingState OperatingState
        {
            get;
            internal set;
        }

        public bool IsSolidBooster { get; internal set; }

        public double CurrentThrust { get; internal set; }

        public double MaximumThrust { get; internal set; }

        /// <summary>
        /// True only for Armed, Ignited, or Producing. Shutdown, Flameout,
        /// Unknown, stale, and unmatched states are never assumed ready.
        /// </summary>
        public bool ReadyForThrust { get; internal set; }

        public bool Faulted
        {
            get
            {
                return
                    OperatingState ==
                    PropulsionEngineOperatingState.Flameout;
            }
        }
    }

    /// <summary>
    /// Operator-independent live propulsion analysis. It joins the latest
    /// KMC-ENGINE1 observations to the Engine-owned topology by PartId.
    /// </summary>
    public sealed class PropulsionLiveStateModel
    {
        public PropulsionLiveStateModel()
        {
            Engines =
                new List<PropulsionEngineLiveStateModel>();
        }

        public bool TelemetryAvailable { get; internal set; }

        public bool TelemetryFresh { get; internal set; }

        public double TelemetryAgeSeconds { get; internal set; }

        public DateTime SourceTimestampUtc { get; internal set; }

        public int TopologyEngineCount { get; internal set; }

        public int MatchedEngineCount { get; internal set; }

        public int UnmatchedTopologyEngineCount { get; internal set; }

        public int UnmatchedTelemetryEngineCount { get; internal set; }

        public bool CoverageComplete { get; internal set; }

        public int ArmedEngineCount { get; internal set; }

        public int IgnitedEngineCount { get; internal set; }

        public int ProducingEngineCount { get; internal set; }

        public int ShutdownEngineCount { get; internal set; }

        public int FlameoutEngineCount { get; internal set; }

        public int UnknownEngineCount { get; internal set; }

        /// <summary>
        /// Matched engines whose fresh live state is Armed, Ignited,
        /// or Producing.
        /// </summary>
        public int ReadyEngineCount { get; internal set; }

        public bool CurrentThrustKnown { get; internal set; }

        public double CurrentThrust { get; internal set; }

        /// <summary>
        /// Sum of MaximumThrust for fresh matched engines whose current state
        /// is Armed, Ignited, or Producing.
        /// </summary>
        public bool AvailableThrustKnown { get; internal set; }

        public double AvailableThrust { get; internal set; }

        /// <summary>
        /// Sum of reported MaximumThrust for all fresh matched engines.
        /// This remains potential/configured maximum and is not called
        /// available thrust.
        /// </summary>
        public bool PotentialMaximumThrustKnown { get; internal set; }

        public double PotentialMaximumThrust { get; internal set; }

        public bool FlightSummaryAvailable { get; internal set; }

        public double ThrottleCommand { get; internal set; }

        public int FlightEngineCount { get; internal set; }

        public int FlightIgnitedEngineCount { get; internal set; }

        public int FlightProducingEngineCount { get; internal set; }

        public int FlightFlameoutEngineCount { get; internal set; }

        public double FlightCurrentThrust { get; internal set; }

        public double FlightMaximumThrust { get; internal set; }

        public bool FlightEngineCountMatchesTopology
        {
            get;
            internal set;
        }

        public bool CurrentThrustAgreesWithFlightSummary
        {
            get;
            internal set;
        }

        public double CurrentThrustDifference
        {
            get;
            internal set;
        }

        public List<PropulsionEngineLiveStateModel> Engines
        {
            get;
            private set;
        }
    }
}
