using System;
using KMC.Engine.Analysis;
using KMC.Engine.Ascent;
using KMC.Engine.Electrical;
using KMC.Engine.Models;
using KMC.Engine.Propulsion;
using KMC.Engine.Systems;
using KMC.Shared.Topology;

namespace KMC.Engine
{
    public sealed class EngineeringEngine
    {
        private readonly AnalysisPipeline _pipeline;

        private readonly AscentFoundationSystem
            _ascentFoundationSystem;

        private readonly ElectricalFlowTracker
            _electricalFlowTracker;

        private readonly object
            _electricalAttributionSyncRoot;

        private ElectricalAttributionModel
            _latestElectricalAttribution;

        private readonly object
            _propulsionTelemetrySyncRoot;

        private PropulsionTelemetryModel
            _latestPropulsionTelemetry;

        public EngineeringEngine()
            : this(
                new AnalysisPipeline(
                    new IEngineeringSystem[]
                    {
                        new CapabilitySystem(),
                        new PowerSystem(),
                        new PropulsionSystem()
                    }))
        {
        }

        public EngineeringEngine(
            AnalysisPipeline pipeline)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(
                    nameof(pipeline));
            }

            _pipeline =
                pipeline;

            _ascentFoundationSystem =
                new AscentFoundationSystem();

            _electricalFlowTracker =
                new ElectricalFlowTracker();

            _electricalAttributionSyncRoot =
                new object();

            _latestElectricalAttribution =
                new ElectricalAttributionModel();

            _propulsionTelemetrySyncRoot =
                new object();

            _latestPropulsionTelemetry =
                new PropulsionTelemetryModel();
        }

        public void PublishElectricalTelemetry(
            double storedEc,
            double capacityEc,
            DateTime receivedUtc)
        {
            _electricalFlowTracker.AddSample(
                storedEc,
                capacityEc,
                receivedUtc);
        }

        public void ClearElectricalTelemetry()
        {
            _electricalFlowTracker.Clear();

            lock (_electricalAttributionSyncRoot)
            {
                _latestElectricalAttribution =
                    new ElectricalAttributionModel();
            }
        }

        public void PublishElectricalAttribution(
            ElectricalAttributionModel attribution)
        {
            if (attribution == null)
            {
                return;
            }

            attribution.Recalculate();

            lock (_electricalAttributionSyncRoot)
            {
                _latestElectricalAttribution =
                    CloneAttribution(
                        attribution);
            }
        }

        public void PublishPropulsionTelemetry(
            PropulsionTelemetryModel telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            lock (_propulsionTelemetrySyncRoot)
            {
                _latestPropulsionTelemetry =
                    ClonePropulsionTelemetry(
                        telemetry);
            }
        }

        public void ClearPropulsionTelemetry()
        {
            lock (_propulsionTelemetrySyncRoot)
            {
                _latestPropulsionTelemetry =
                    new PropulsionTelemetryModel();
            }
        }

        private ElectricalAttributionModel
            GetElectricalAttribution()
        {
            lock (_electricalAttributionSyncRoot)
            {
                return
                    CloneAttribution(
                        _latestElectricalAttribution);
            }
        }

        private PropulsionTelemetryModel
            GetPropulsionTelemetry()
        {
            lock (_propulsionTelemetrySyncRoot)
            {
                return
                    ClonePropulsionTelemetry(
                        _latestPropulsionTelemetry);
            }
        }

        private static ElectricalAttributionModel CloneAttribution(
            ElectricalAttributionModel source)
        {
            ElectricalAttributionModel clone =
                new ElectricalAttributionModel();

            if (source == null)
            {
                return clone;
            }

            clone.TelemetryAvailable =
                source.TelemetryAvailable;

            for (int index = 0;
                 index < source.Entries.Count;
                 index++)
            {
                ElectricalAttributionEntry entry =
                    source.Entries[index];

                if (entry == null)
                {
                    continue;
                }

                clone.Entries.Add(
                    new ElectricalAttributionEntry
                    {
                        Kind =
                            entry.Kind,

                        PartId =
                            entry.PartId,

                        PartTitle =
                            entry.PartTitle,

                        Category =
                            entry.Category,

                        Evidence =
                            entry.Evidence,

                        CurrentRateKnown =
                            entry.CurrentRateKnown,

                        CurrentRateEcPerSecond =
                            entry.CurrentRateEcPerSecond,

                        MaximumRateKnown =
                            entry.MaximumRateKnown,

                        MaximumRateEcPerSecond =
                            entry.MaximumRateEcPerSecond,

                        Enabled =
                            entry.Enabled,

                        ActiveStateKnown =
                            entry.ActiveStateKnown,

                        Active =
                            entry.Active
                    });
            }

            clone.Recalculate();

            return clone;
        }

        private static PropulsionTelemetryModel
            ClonePropulsionTelemetry(
                PropulsionTelemetryModel source)
        {
            PropulsionTelemetryModel clone =
                new PropulsionTelemetryModel();

            if (source == null)
            {
                return clone;
            }

            clone.TelemetryAvailable =
                source.TelemetryAvailable;

            clone.SourceTimestampUtc =
                source.SourceTimestampUtc;

            clone.ReceivedUtc =
                source.ReceivedUtc;

            for (int index = 0;
                 index < source.Entries.Count;
                 index++)
            {
                PropulsionEngineTelemetryEntry entry =
                    source.Entries[index];

                if (entry == null)
                {
                    continue;
                }

                clone.Entries.Add(
                    new PropulsionEngineTelemetryEntry
                    {
                        PartId =
                            entry.PartId,

                        OperatingState =
                            entry.OperatingState,

                        IsSolidBooster =
                            entry.IsSolidBooster,

                        CurrentThrust =
                            entry.CurrentThrust,

                        MaximumThrust =
                            entry.MaximumThrust
                    });
            }

            return clone;
        }

        public AscentModel GetLatestAscentFoundation()
        {
            return
                _ascentFoundationSystem
                    .GetLatest();
        }

        public AnalysisPipelineResult Analyze(
            long sequence,
            DateTime receivedUtc,
            object telemetryPacket,
            VesselTopology topology)
        {
            TelemetrySnapshot telemetry =
                new TelemetrySnapshot(
                    sequence,
                    receivedUtc,
                    telemetryPacket,
                    _electricalFlowTracker.GetLatest(),
                    GetElectricalAttribution(),
                    GetPropulsionTelemetry());

            VesselModel vessel =
                new VesselModel(
                    topology);

            AnalysisPipelineResult result =
                _pipeline.Execute(
                    telemetry,
                    vessel);

            _ascentFoundationSystem.Update(
                telemetryPacket as KMC.Shared.TelemetryPacket,
                receivedUtc,
                result.Snapshot.Propulsion);

            result.Snapshot.Ascent =
                _ascentFoundationSystem
                    .GetLatest();

            return result;
        }
    }
}
