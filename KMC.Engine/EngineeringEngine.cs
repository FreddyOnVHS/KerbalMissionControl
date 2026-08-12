using System;
using KMC.Engine.Analysis;
using KMC.Engine.Ascent;
using KMC.Engine.Electrical;
using KMC.Engine.Maneuver;
using KMC.Engine.Guidance;
using KMC.Engine.Models;
using KMC.Engine.Orbit;
using KMC.Engine.Propulsion;
using KMC.Engine.Systems;
using KMC.Engine.SpacecraftSystems;
using KMC.Shared.Topology;

namespace KMC.Engine
{
    public sealed class EngineeringEngine
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly AscentFoundationSystem _ascentFoundationSystem;
        private readonly OrbitFoundationSystem _orbitFoundationSystem;
        private readonly ManeuverPlanningSystem _maneuverPlanningSystem;
        private readonly GuidanceSystem _guidanceSystem;
        private readonly SpacecraftSystemsSystem _spacecraftSystemsSystem;
        private readonly ElectricalFlowTracker _electricalFlowTracker;
        private readonly object _electricalAttributionSyncRoot;
        private ElectricalAttributionModel _latestElectricalAttribution;
        private readonly object _propulsionTelemetrySyncRoot;
        private PropulsionTelemetryModel _latestPropulsionTelemetry;
        private readonly object _velocityVectorTelemetrySyncRoot;
        private VelocityVectorTelemetryModel _latestVelocityVectorTelemetry;
        private readonly object _maneuverEpochSyncRoot;
        private ManeuverEpochTelemetryModel _latestManeuverEpochTelemetry;

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

        public EngineeringEngine(AnalysisPipeline pipeline)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            _pipeline = pipeline;
            _ascentFoundationSystem = new AscentFoundationSystem();
            _orbitFoundationSystem = new OrbitFoundationSystem();
            _maneuverPlanningSystem = new ManeuverPlanningSystem();
            _guidanceSystem = new GuidanceSystem();
            _spacecraftSystemsSystem =
                new SpacecraftSystemsSystem();
            _electricalFlowTracker = new ElectricalFlowTracker();
            _electricalAttributionSyncRoot = new object();
            _latestElectricalAttribution = new ElectricalAttributionModel();
            _propulsionTelemetrySyncRoot = new object();
            _latestPropulsionTelemetry = new PropulsionTelemetryModel();
            _velocityVectorTelemetrySyncRoot = new object();
            _latestVelocityVectorTelemetry = new VelocityVectorTelemetryModel();
            _maneuverEpochSyncRoot = new object();
            _latestManeuverEpochTelemetry = new ManeuverEpochTelemetryModel();
        }

        public void PublishElectricalTelemetry(double storedEc, double capacityEc, DateTime receivedUtc)
        {
            _electricalFlowTracker.AddSample(storedEc, capacityEc, receivedUtc);
        }

        public void ClearElectricalTelemetry()
        {
            _electricalFlowTracker.Clear();

            lock (_electricalAttributionSyncRoot)
            {
                _latestElectricalAttribution = new ElectricalAttributionModel();
            }
        }

        public void PublishElectricalAttribution(ElectricalAttributionModel attribution)
        {
            if (attribution == null)
            {
                return;
            }

            attribution.Recalculate();

            lock (_electricalAttributionSyncRoot)
            {
                _latestElectricalAttribution = CloneAttribution(attribution);
            }
        }

        public void PublishPropulsionTelemetry(PropulsionTelemetryModel telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            lock (_propulsionTelemetrySyncRoot)
            {
                _latestPropulsionTelemetry = ClonePropulsionTelemetry(telemetry);
            }
        }

        public void ClearPropulsionTelemetry()
        {
            lock (_propulsionTelemetrySyncRoot)
            {
                _latestPropulsionTelemetry = new PropulsionTelemetryModel();
            }
        }

        public void PublishVelocityVectorTelemetry(VelocityVectorTelemetryModel telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            lock (_velocityVectorTelemetrySyncRoot)
            {
                _latestVelocityVectorTelemetry = VelocityVectorTelemetryModel.Clone(telemetry);
            }
        }

        public void ClearVelocityVectorTelemetry()
        {
            lock (_velocityVectorTelemetrySyncRoot)
            {
                _latestVelocityVectorTelemetry = new VelocityVectorTelemetryModel();
            }
        }

        public void PublishManeuverEpochTelemetry(
            ManeuverEpochTelemetryModel telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            lock (_maneuverEpochSyncRoot)
            {
                _latestManeuverEpochTelemetry =
                    ManeuverEpochTelemetryModel.Clone(
                        telemetry);
            }
        }

        public void ClearManeuverEpochTelemetry()
        {
            lock (_maneuverEpochSyncRoot)
            {
                _latestManeuverEpochTelemetry =
                    new ManeuverEpochTelemetryModel();
            }
        }

        private ManeuverEpochTelemetryModel
            GetManeuverEpochTelemetry()
        {
            lock (_maneuverEpochSyncRoot)
            {
                return
                    ManeuverEpochTelemetryModel.Clone(
                        _latestManeuverEpochTelemetry);
            }
        }

        private ElectricalAttributionModel GetElectricalAttribution()
        {
            lock (_electricalAttributionSyncRoot)
            {
                return CloneAttribution(_latestElectricalAttribution);
            }
        }

        private PropulsionTelemetryModel GetPropulsionTelemetry()
        {
            lock (_propulsionTelemetrySyncRoot)
            {
                return ClonePropulsionTelemetry(_latestPropulsionTelemetry);
            }
        }

        private VelocityVectorTelemetryModel GetVelocityVectorTelemetry()
        {
            lock (_velocityVectorTelemetrySyncRoot)
            {
                return VelocityVectorTelemetryModel.Clone(_latestVelocityVectorTelemetry);
            }
        }

        private static ElectricalAttributionModel CloneAttribution(ElectricalAttributionModel source)
        {
            ElectricalAttributionModel clone = new ElectricalAttributionModel();

            if (source == null)
            {
                return clone;
            }

            clone.TelemetryAvailable = source.TelemetryAvailable;

            for (int index = 0; index < source.Entries.Count; index++)
            {
                ElectricalAttributionEntry entry = source.Entries[index];

                if (entry == null)
                {
                    continue;
                }

                clone.Entries.Add(
                    new ElectricalAttributionEntry
                    {
                        Kind = entry.Kind,
                        PartId = entry.PartId,
                        PartTitle = entry.PartTitle,
                        Category = entry.Category,
                        Evidence = entry.Evidence,
                        CurrentRateKnown = entry.CurrentRateKnown,
                        CurrentRateEcPerSecond = entry.CurrentRateEcPerSecond,
                        MaximumRateKnown = entry.MaximumRateKnown,
                        MaximumRateEcPerSecond = entry.MaximumRateEcPerSecond,
                        Enabled = entry.Enabled,
                        ActiveStateKnown = entry.ActiveStateKnown,
                        Active = entry.Active
                    });
            }

            clone.Recalculate();
            return clone;
        }

        private static PropulsionTelemetryModel ClonePropulsionTelemetry(PropulsionTelemetryModel source)
        {
            PropulsionTelemetryModel clone = new PropulsionTelemetryModel();

            if (source == null)
            {
                return clone;
            }

            clone.TelemetryAvailable = source.TelemetryAvailable;
            clone.SourceTimestampUtc = source.SourceTimestampUtc;
            clone.ReceivedUtc = source.ReceivedUtc;

            for (int index = 0; index < source.Entries.Count; index++)
            {
                PropulsionEngineTelemetryEntry entry = source.Entries[index];

                if (entry == null)
                {
                    continue;
                }

                clone.Entries.Add(
                    new PropulsionEngineTelemetryEntry
                    {
                        PartId = entry.PartId,
                        OperatingState = entry.OperatingState,
                        IsSolidBooster = entry.IsSolidBooster,
                        CurrentThrust = entry.CurrentThrust,
                        MaximumThrust = entry.MaximumThrust
                    });
            }

            return clone;
        }

        public AscentModel GetLatestAscentFoundation()
        {
            return _ascentFoundationSystem.GetLatest();
        }

        public OrbitModel GetLatestOrbitFoundation()
        {
            return _orbitFoundationSystem.GetLatest();
        }

        public ManeuverPlanModel GetLatestManeuverPlan()
        {
            return _maneuverPlanningSystem.GetLatest();
        }

        public SpacecraftSystemsModel GetLatestSpacecraftSystems()
        {
            return
                _spacecraftSystemsSystem.GetLatest();
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

            VesselModel vessel = new VesselModel(topology);
            AnalysisPipelineResult result = _pipeline.Execute(telemetry, vessel);

            _spacecraftSystemsSystem.Update(
                vessel,
                receivedUtc);

            result.Snapshot.SpacecraftSystems =
                _spacecraftSystemsSystem.GetLatest();

            _ascentFoundationSystem.Update(
                telemetryPacket as KMC.Shared.TelemetryPacket,
                receivedUtc,
                result.Snapshot.Propulsion);

            result.Snapshot.Ascent = _ascentFoundationSystem.GetLatest();

            _orbitFoundationSystem.Update(
                telemetryPacket as KMC.Shared.TelemetryPacket,
                receivedUtc,
                result.Snapshot.Ascent,
                GetVelocityVectorTelemetry());

            result.Snapshot.Orbit = _orbitFoundationSystem.GetLatest();

            _maneuverPlanningSystem.Update(
                result.Snapshot.Orbit,
                GetManeuverEpochTelemetry(),
                receivedUtc);

            result.Snapshot.ManeuverPlan = _maneuverPlanningSystem.GetLatest();

            _guidanceSystem.Update(
                result.Snapshot.Orbit,
                result.Snapshot.ManeuverPlan,
                telemetryPacket as KMC.Shared.TelemetryPacket,
                receivedUtc);

            result.Snapshot.Guidance =
                _guidanceSystem.GetLatest();

            return result;
        }
    }
}
