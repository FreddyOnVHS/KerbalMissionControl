using System;
using System.Collections.Generic;
using KMC.Engine;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;
using KMC.Engine.Maneuver;
using KMC.Engine.Orbit;
using KMC.Engine.Propulsion;
using KMC.MissionControl.Debugging;
using KMC.MissionControl.Diagnostics;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Rendering.Propulsion;
using KMC.MissionControl.Telemetry;
using KMC.MissionControl.Transport;
using KMC.Shared;
using KMC.Shared.Topology;

namespace KMC.MissionControl
{
    /// <summary>
    /// Coordinates typed telemetry with KMC.Engine.
    /// Socket ownership lives in MissionControl transport classes.
    /// </summary>
    public sealed class MissionControlReceiver :
        IDisposable
    {
        private readonly EngineeringEngine _engineeringEngine;
        private readonly TelemetryTransport _transport;
        private readonly ManeuverLinkTransport _maneuverLink;
        private readonly TelemetryCache _cache;
        private readonly object _engineeringSyncRoot;

        private bool _running;
        private long _engineeringSequence;

        public MissionControlReceiver()
        {
            _engineeringEngine =
                new EngineeringEngine();

            _transport =
                new TelemetryTransport();

            _maneuverLink =
                new ManeuverLinkTransport();

            _cache =
                new TelemetryCache();

            _engineeringSyncRoot =
                new object();

            _transport.FlightTelemetryReceived +=
                OnFlightTelemetryReceived;

            _transport.TopologyReceived +=
                OnTopologyReceived;

            _transport.SystemsTelemetryReceived +=
                OnSystemsTelemetryReceived;

            _transport.EngineStateTelemetryReceived +=
                OnEngineStateTelemetryReceived;

            _transport.VelocityVectorTelemetryReceived +=
                OnVelocityVectorTelemetryReceived;

            _maneuverLink.EpochReceived +=
                OnManeuverEpochReceived;

            _maneuverLink.AcknowledgmentReceived +=
                OnManeuverAcknowledgmentReceived;
        }

        public event Action<TelemetryPacket> TelemetryReceived;
        public event Action<VesselTopology> TopologyReceived;
        public event Action<ManeuverUplinkAck> ManeuverAcknowledgmentReceived;

        public void Start()
        {
            if (_running)
            {
                return;
            }

            EngineeringSnapshotStore.Clear();
            ManeuverUplinkStatusStore.Clear();

            _engineeringEngine.ClearElectricalTelemetry();
            _engineeringEngine.ClearPropulsionTelemetry();
            _engineeringEngine.ClearVelocityVectorTelemetry();
            _engineeringEngine.ClearManeuverEpochTelemetry();

            EngineStateTelemetryStore.Clear();
            _cache.Clear();

            lock (_engineeringSyncRoot)
            {
                _engineeringSequence = 0;
            }

            _maneuverLink.Start();
            _transport.Start();

            _running = true;
        }

        public bool UploadLatestManeuver(
            out string resultText)
        {
            resultText = string.Empty;

            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore.TryGetLatest(out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.ManeuverPlan == null)
            {
                resultText = "NO ENGINE MANEUVER PLAN";
                ManeuverUplinkStatusStore.PublishRejected(
                    string.Empty,
                    resultText);
                return false;
            }

            ManeuverPlanModel plan =
                result.Snapshot.ManeuverPlan;

            if (!plan.Available)
            {
                resultText = "MANEUVER PLAN IS NOT AVAILABLE";
                ManeuverUplinkStatusStore.PublishRejected(
                    plan.PlanId,
                    resultText);
                return false;
            }

            if (!plan.NodeUniversalTimeAvailable ||
                double.IsNaN(plan.NodeUniversalTimeSeconds) ||
                double.IsInfinity(plan.NodeUniversalTimeSeconds))
            {
                resultText = "KSP UNIVERSAL TIME IS NOT AVAILABLE";
                ManeuverUplinkStatusStore.PublishRejected(
                    plan.PlanId,
                    resultText);
                return false;
            }

            if (string.IsNullOrWhiteSpace(plan.VesselId))
            {
                resultText = "VESSEL ID IS NOT AVAILABLE";
                ManeuverUplinkStatusStore.PublishRejected(
                    plan.PlanId,
                    resultText);
                return false;
            }

            ManeuverUplinkPacket packet =
                new ManeuverUplinkPacket
                {
                    VesselId = plan.VesselId,
                    PlanId = plan.PlanId,
                    NodeUniversalTimeSeconds =
                        plan.NodeUniversalTimeSeconds,
                    ProgradeDeltaVMetersPerSecond =
                        plan.ProgradeDeltaVMetersPerSecond,
                    NormalDeltaVMetersPerSecond =
                        plan.NormalDeltaVMetersPerSecond,
                    RadialDeltaVMetersPerSecond =
                        plan.RadialDeltaVMetersPerSecond
                };

            try
            {
                _maneuverLink.Send(packet);

                ManeuverUplinkStatusStore.PublishPending(
                    plan.PlanId,
                    plan.NodeUniversalTimeSeconds);

                resultText = "UPLINK SENT - AWAITING PLUGIN ACK";
                return true;
            }
            catch (Exception ex)
            {
                resultText =
                    "UPLINK FAILED: " +
                    ex.Message;

                ManeuverUplinkStatusStore.PublishRejected(
                    plan.PlanId,
                    resultText);

                return false;
            }
        }

        private void OnManeuverEpochReceived(
            ManeuverEpochPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            _engineeringEngine.PublishManeuverEpochTelemetry(
                new ManeuverEpochTelemetryModel
                {
                    Available = true,
                    SourceTimestampUtc = packet.TimestampUtc,
                    ReceivedUtc = DateTime.UtcNow,
                    VesselId = packet.VesselId ?? string.Empty,
                    VesselName = packet.VesselName ?? string.Empty,
                    UniversalTimeSeconds =
                        packet.UniversalTimeSeconds,
                    MissionTimeSeconds =
                        packet.MissionTimeSeconds
                });
        }

        private void OnManeuverAcknowledgmentReceived(
            ManeuverUplinkAck ack)
        {
            ManeuverUplinkStatusStore.PublishAck(
                ack);

            Action<ManeuverUplinkAck> handler =
                ManeuverAcknowledgmentReceived;

            if (handler != null)
            {
                handler(ack);
            }
        }

        private void OnVelocityVectorTelemetryReceived(
            VelocityVectorTelemetrySample sample)
        {
            if (sample == null)
            {
                return;
            }

            _engineeringEngine.PublishVelocityVectorTelemetry(
                new VelocityVectorTelemetryModel
                {
                    TelemetryAvailable = true,
                    SourceTimestampUtc = sample.SourceTimestampUtc,
                    ReceivedUtc = sample.ReceivedUtc,
                    VesselName = sample.VesselName ?? string.Empty,
                    SurfaceRightMetersPerSecond =
                        sample.SurfaceRightMetersPerSecond,
                    SurfaceNoseMetersPerSecond =
                        sample.SurfaceNoseMetersPerSecond,
                    SurfaceReferenceForwardMetersPerSecond =
                        sample.SurfaceReferenceForwardMetersPerSecond,
                    OrbitalRightMetersPerSecond =
                        sample.OrbitalRightMetersPerSecond,
                    OrbitalNoseMetersPerSecond =
                        sample.OrbitalNoseMetersPerSecond,
                    OrbitalReferenceForwardMetersPerSecond =
                        sample.OrbitalReferenceForwardMetersPerSecond
                });
        }

        private void OnEngineStateTelemetryReceived(
            DateTime sourceTimestampUtc,
            Dictionary<uint, EngineStateTelemetry> states)
        {
            EngineStateTelemetryStore.Publish(states);

            PropulsionTelemetryModel telemetry =
                new PropulsionTelemetryModel
                {
                    TelemetryAvailable = true,
                    SourceTimestampUtc = sourceTimestampUtc,
                    ReceivedUtc = DateTime.UtcNow
                };

            if (states != null)
            {
                foreach (
                    KeyValuePair<uint, EngineStateTelemetry> pair
                    in states)
                {
                    EngineStateTelemetry source =
                        pair.Value;

                    if (source == null)
                    {
                        continue;
                    }

                    telemetry.Entries.Add(
                        new PropulsionEngineTelemetryEntry
                        {
                            PartId = source.PartId,
                            OperatingState =
                                ConvertOperatingState(
                                    source.OperatingState),
                            IsSolidBooster = source.IsSolidBooster,
                            CurrentThrust = source.CurrentThrust,
                            MaximumThrust = source.MaximumThrust
                        });
                }
            }

            _engineeringEngine.PublishPropulsionTelemetry(
                telemetry);
        }

        private static PropulsionEngineOperatingState
            ConvertOperatingState(
                EngineOperatingState state)
        {
            switch (state)
            {
                case EngineOperatingState.Armed:
                    return PropulsionEngineOperatingState.Armed;

                case EngineOperatingState.Ignited:
                    return PropulsionEngineOperatingState.Ignited;

                case EngineOperatingState.Producing:
                    return PropulsionEngineOperatingState.Producing;

                case EngineOperatingState.Shutdown:
                    return PropulsionEngineOperatingState.Shutdown;

                case EngineOperatingState.Flameout:
                    return PropulsionEngineOperatingState.Flameout;

                default:
                    return PropulsionEngineOperatingState.Unknown;
            }
        }

        private void OnSystemsTelemetryReceived(
            SystemsTelemetrySample systems)
        {
            _cache.PublishSystems(systems);

            _engineeringEngine.PublishElectricalTelemetry(
                systems.ElectricChargeAmount,
                systems.ElectricChargeCapacity,
                systems.ReceivedUtc);

            ElectricalAttributionModel attribution =
                new ElectricalAttributionModel();

            attribution.TelemetryAvailable =
                systems.AttributionTelemetryAvailable;

            for (int index = 0;
                 index < systems.AttributionEntries.Count;
                 index++)
            {
                SystemsAttributionEntry source =
                    systems.AttributionEntries[index];

                attribution.Entries.Add(
                    new ElectricalAttributionEntry
                    {
                        Kind =
                            source.IsProducer
                                ? ElectricalAttributionKind.Producer
                                : ElectricalAttributionKind.Consumer,
                        PartId = source.PartId,
                        PartTitle = source.PartTitle,
                        Category = source.Category,
                        Evidence =
                            ParseEvidence(
                                source.Evidence),
                        CurrentRateKnown = source.CurrentKnown,
                        CurrentRateEcPerSecond =
                            source.CurrentRateEcPerSecond,
                        MaximumRateKnown = source.MaximumKnown,
                        MaximumRateEcPerSecond =
                            source.MaximumRateEcPerSecond,
                        Enabled = source.Enabled,
                        ActiveStateKnown = source.ActiveKnown,
                        Active = source.Active
                    });
            }

            attribution.Recalculate();

            _engineeringEngine.PublishElectricalAttribution(
                attribution);
        }

        private static ElectricalRateEvidence ParseEvidence(
            string value)
        {
            ElectricalRateEvidence parsed;

            if (Enum.TryParse(
                    value,
                    true,
                    out parsed))
            {
                return parsed;
            }

            return ElectricalRateEvidence.Unknown;
        }

        private void OnFlightTelemetryReceived(
            TelemetryPacket packet)
        {
            PropulsionDebugSnapshotStore
                .PublishTelemetry(packet);

            AnalyzeEngineering(packet);

            Action<TelemetryPacket> handler =
                TelemetryReceived;

            if (handler != null)
            {
                handler(packet);
            }
        }

        private void AnalyzeEngineering(
            TelemetryPacket packet)
        {
            VesselTopology topology =
                _cache.GetTopology();

            if (topology == null)
            {
                return;
            }

            long sequence;

            lock (_engineeringSyncRoot)
            {
                _engineeringSequence++;
                sequence = _engineeringSequence;
            }

            try
            {
                AnalysisPipelineResult result =
                    _engineeringEngine.Analyze(
                        sequence,
                        DateTime.UtcNow,
                        packet,
                        topology);

                EngineeringSnapshotStore.Publish(
                    result);
            }
            catch (Exception ex)
            {
                EngineeringSnapshotStore.ReportError(
                    ex);
            }
        }

        private void OnTopologyReceived(
            VesselTopology topology)
        {
            _cache.PublishTopology(topology);

            PropulsionDebugSnapshotStore
                .PublishTopology(topology);

            try
            {
                PropulsionRenderGraphBuilder builder =
                    new PropulsionRenderGraphBuilder();

                PropulsionRenderGraph graph =
                    builder.Build(topology);

                PropulsionGraphStore.Publish(graph);
                PropulsionGraphFileLogger.Write(graph);
            }
            catch (Exception ex)
            {
                PropulsionGraphFileLogger
                    .WriteError(ex);
            }

            Action<VesselTopology> handler =
                TopologyReceived;

            if (handler != null)
            {
                handler(topology);
            }
        }

        public void Stop()
        {
            if (!_running)
            {
                _transport.Stop();
                _maneuverLink.Stop();
                return;
            }

            _running = false;

            _transport.Stop();
            _maneuverLink.Stop();

            PropulsionGraphStore.Clear();
            PropulsionDebugSnapshotStore.Clear();
            EngineeringSnapshotStore.Clear();
            ManeuverUplinkStatusStore.Clear();
            EngineStateTelemetryStore.Clear();

            _engineeringEngine.ClearElectricalTelemetry();
            _engineeringEngine.ClearPropulsionTelemetry();
            _engineeringEngine.ClearVelocityVectorTelemetry();
            _engineeringEngine.ClearManeuverEpochTelemetry();

            _cache.Clear();
        }

        public void Dispose()
        {
            Stop();

            _transport.FlightTelemetryReceived -=
                OnFlightTelemetryReceived;

            _transport.TopologyReceived -=
                OnTopologyReceived;

            _transport.SystemsTelemetryReceived -=
                OnSystemsTelemetryReceived;

            _transport.EngineStateTelemetryReceived -=
                OnEngineStateTelemetryReceived;

            _transport.VelocityVectorTelemetryReceived -=
                OnVelocityVectorTelemetryReceived;

            _maneuverLink.EpochReceived -=
                OnManeuverEpochReceived;

            _maneuverLink.AcknowledgmentReceived -=
                OnManeuverAcknowledgmentReceived;

            _transport.Dispose();
            _maneuverLink.Dispose();
        }
    }
}
