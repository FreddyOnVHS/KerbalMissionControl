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

        public double MaximumThrust { get; set; }
    }

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

        public int ActivationStage { get; internal set; }

        /// <summary>
        /// True when live flight-stage data proves that an Armed engine's
        /// activation stage has already been reached. Ignited/Producing are
        /// direct evidence and do not depend on this flag for readiness.
        /// </summary>
        public bool StageEligible { get; internal set; }

        public bool IsFutureStage { get; internal set; }

        /// <summary>
        /// Direct Ignited/Producing evidence is immediately ready.
        /// Armed requires the activation stage to have been reached.
        /// Shutdown, Flameout, Unknown, stale, and unmatched are not ready.
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

        public int ReadyEngineCount { get; internal set; }

        public int FutureStageEngineCount { get; internal set; }

        public bool CurrentThrustKnown { get; internal set; }

        public double CurrentThrust { get; internal set; }

        public bool AvailableThrustKnown { get; internal set; }

        public double AvailableThrust { get; internal set; }

        public bool PotentialMaximumThrustKnown { get; internal set; }

        public double PotentialMaximumThrust { get; internal set; }

        public bool FlightSummaryAvailable { get; internal set; }

        public int LiveCurrentStage { get; internal set; }

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
