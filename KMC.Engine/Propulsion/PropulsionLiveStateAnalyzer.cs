using System;
using System.Collections.Generic;
using KMC.Shared;

namespace KMC.Engine.Propulsion
{
    internal static class PropulsionLiveStateAnalyzer
    {
        private const double MaximumTelemetryAgeSeconds =
            1.0;

        private const double MaximumFutureSkewSeconds =
            1.0;

        public static PropulsionLiveStateModel Analyze(
            PropulsionTopologyModel topology,
            PropulsionTelemetryModel telemetry,
            DateTime analysisUtc,
            object flightPacket)
        {
            PropulsionLiveStateModel model =
                new PropulsionLiveStateModel();

            if (topology == null)
            {
                return model;
            }

            model.TopologyEngineCount =
                topology.EngineCount;

            TelemetryPacket flight =
                flightPacket as TelemetryPacket;

            if (flight != null)
            {
                model.FlightSummaryAvailable =
                    true;

                model.LiveCurrentStage =
                    flight.CurrentStage;

                model.ThrottleCommand =
                    flight.Throttle;

                model.FlightEngineCount =
                    flight.EngineCount;

                model.FlightIgnitedEngineCount =
                    flight.IgnitedEngineCount;

                model.FlightProducingEngineCount =
                    flight.ProducingThrustEngineCount;

                model.FlightFlameoutEngineCount =
                    flight.FlameoutEngineCount;

                model.FlightCurrentThrust =
                    Math.Max(
                        0.0,
                        flight.CurrentThrust);

                model.FlightMaximumThrust =
                    Math.Max(
                        0.0,
                        flight.MaximumThrust);

                model.FlightEngineCountMatchesTopology =
                    flight.EngineCount ==
                    topology.EngineCount;
            }

            Dictionary<uint, PropulsionEngineTelemetryEntry>
                byPartId =
                    new Dictionary<
                        uint,
                        PropulsionEngineTelemetryEntry>();

            if (telemetry != null)
            {
                model.TelemetryAvailable =
                    telemetry.TelemetryAvailable;

                model.SourceTimestampUtc =
                    telemetry.SourceTimestampUtc;

                DateTime receivedUtc =
                    telemetry.ReceivedUtc.Kind ==
                        DateTimeKind.Utc
                            ? telemetry.ReceivedUtc
                            : telemetry.ReceivedUtc
                                .ToUniversalTime();

                DateTime normalizedAnalysisUtc =
                    analysisUtc.Kind ==
                        DateTimeKind.Utc
                            ? analysisUtc
                            : analysisUtc
                                .ToUniversalTime();

                if (telemetry.TelemetryAvailable &&
                    receivedUtc != DateTime.MinValue)
                {
                    double age =
                        (normalizedAnalysisUtc -
                         receivedUtc)
                            .TotalSeconds;

                    model.TelemetryAgeSeconds =
                        age;

                    model.TelemetryFresh =
                        age <=
                            MaximumTelemetryAgeSeconds &&
                        age >=
                            -MaximumFutureSkewSeconds;
                }

                for (int index = 0;
                     index < telemetry.Entries.Count;
                     index++)
                {
                    PropulsionEngineTelemetryEntry entry =
                        telemetry.Entries[index];

                    if (entry != null)
                    {
                        byPartId[entry.PartId] =
                            entry;
                    }
                }
            }

            HashSet<uint> topologyIds =
                new HashSet<uint>();

            for (int index = 0;
                 index < topology.Engines.Count;
                 index++)
            {
                PropulsionEngineModel engine =
                    topology.Engines[index];

                topologyIds.Add(
                    engine.PartId);

                PropulsionEngineLiveStateModel live =
                    new PropulsionEngineLiveStateModel
                    {
                        PartId =
                            engine.PartId,

                        PartTitle =
                            engine.PartTitle,

                        OperatingState =
                            PropulsionEngineOperatingState.Unknown,

                        IsSolidBooster =
                            engine.IsSolidBooster,

                        ActivationStage =
                            engine.ActivationStage
                    };

                PropulsionEngineTelemetryEntry entry;

                if (byPartId.TryGetValue(
                        engine.PartId,
                        out entry))
                {
                    live.TelemetryMatched =
                        true;

                    live.OperatingState =
                        entry.OperatingState;

                    live.IsSolidBooster =
                        entry.IsSolidBooster;

                    live.CurrentThrust =
                        Math.Max(
                            0.0,
                            entry.CurrentThrust);

                    live.MaximumThrust =
                        Math.Max(
                            0.0,
                            entry.MaximumThrust);

                    model.MatchedEngineCount++;
                }
                else
                {
                    model.UnmatchedTopologyEngineCount++;
                }

                if (model.TelemetryFresh &&
                    live.TelemetryMatched)
                {
                    CountState(
                        model,
                        live);

                    live.StageEligible =
                        IsStageEligible(
                            engine.ActivationStage,
                            model.FlightSummaryAvailable,
                            model.LiveCurrentStage);

                    live.IsFutureStage =
                        live.OperatingState ==
                            PropulsionEngineOperatingState.Armed &&
                        model.FlightSummaryAvailable &&
                        !live.StageEligible;

                    live.ReadyForThrust =
                        IsReadyForThrust(
                            live.OperatingState,
                            live.StageEligible);

                    if (live.IsFutureStage)
                    {
                        model.FutureStageEngineCount++;
                    }

                    model.CurrentThrust +=
                        live.CurrentThrust;

                    model.PotentialMaximumThrust +=
                        live.MaximumThrust;

                    if (live.ReadyForThrust)
                    {
                        model.ReadyEngineCount++;

                        model.AvailableThrust +=
                            live.MaximumThrust;
                    }
                }

                model.Engines.Add(
                    live);
            }

            foreach (
                KeyValuePair<
                    uint,
                    PropulsionEngineTelemetryEntry> pair
                in byPartId)
            {
                if (!topologyIds.Contains(
                        pair.Key))
                {
                    model.UnmatchedTelemetryEngineCount++;
                }
            }

            model.CoverageComplete =
                model.TelemetryFresh &&
                model.MatchedEngineCount ==
                    topology.EngineCount &&
                model.UnmatchedTelemetryEngineCount ==
                    0;

            model.CurrentThrustKnown =
                model.CoverageComplete;

            model.AvailableThrustKnown =
                model.CoverageComplete &&
                model.FlightSummaryAvailable;

            model.PotentialMaximumThrustKnown =
                model.CoverageComplete;

            if (model.CurrentThrustKnown &&
                model.FlightSummaryAvailable)
            {
                model.CurrentThrustDifference =
                    model.CurrentThrust -
                    model.FlightCurrentThrust;

                double tolerance =
                    Math.Max(
                        0.5,
                        Math.Max(
                            model.CurrentThrust,
                            model.FlightCurrentThrust) *
                        0.01);

                model.CurrentThrustAgreesWithFlightSummary =
                    Math.Abs(
                        model.CurrentThrustDifference) <=
                    tolerance;
            }

            return model;
        }

        private static void CountState(
            PropulsionLiveStateModel model,
            PropulsionEngineLiveStateModel engine)
        {
            switch (engine.OperatingState)
            {
                case PropulsionEngineOperatingState.Armed:
                    model.ArmedEngineCount++;
                    break;

                case PropulsionEngineOperatingState.Ignited:
                    model.IgnitedEngineCount++;
                    break;

                case PropulsionEngineOperatingState.Producing:
                    model.ProducingEngineCount++;
                    break;

                case PropulsionEngineOperatingState.Shutdown:
                    model.ShutdownEngineCount++;
                    break;

                case PropulsionEngineOperatingState.Flameout:
                    model.FlameoutEngineCount++;
                    break;

                default:
                    model.UnknownEngineCount++;
                    break;
            }
        }

        private static bool IsStageEligible(
            int activationStage,
            bool flightSummaryAvailable,
            int liveCurrentStage)
        {
            if (!flightSummaryAvailable)
            {
                return false;
            }

            if (activationStage < 0)
            {
                return false;
            }

            /*
             * KSP stage numbers count downward.
             *
             * Example:
             * current=7, activation=6 -> future stage
             * current=6, activation=6 -> reached
             * current=5, activation=6 -> already reached
             */
            return
                activationStage >=
                liveCurrentStage;
        }

        private static bool IsReadyForThrust(
            PropulsionEngineOperatingState state,
            bool stageEligible)
        {
            if (state ==
                    PropulsionEngineOperatingState.Producing ||
                state ==
                    PropulsionEngineOperatingState.Ignited)
            {
                return true;
            }

            if (state ==
                    PropulsionEngineOperatingState.Armed)
            {
                return
                    stageEligible;
            }

            return false;
        }
    }
}
