using System;
using KMC.Engine;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;
using KMC.MissionControl.Debugging;
using KMC.MissionControl.Diagnostics;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Rendering.Propulsion;
using KMC.MissionControl.Transport;
using KMC.Shared;
using KMC.Shared.Topology;

namespace KMC.MissionControl
{
    /// <summary>
    /// Coordinates typed telemetry with KMC.Engine.
    ///
    /// Socket ownership lives exclusively in TelemetryTransport.
    /// </summary>
    public sealed class MissionControlReceiver :
        IDisposable
    {
        private readonly EngineeringEngine _engineeringEngine;
        private readonly TelemetryTransport _transport;
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
        }

        public event Action<TelemetryPacket> TelemetryReceived;
        public event Action<VesselTopology> TopologyReceived;

        public void Start()
        {
            if (_running)
            {
                return;
            }

            EngineeringSnapshotStore.Clear();
            _engineeringEngine.ClearElectricalTelemetry();
            _cache.Clear();

            lock (_engineeringSyncRoot)
            {
                _engineeringSequence =
                    0;
            }

            _transport.Start();

            _running =
                true;
        }

        private void OnSystemsTelemetryReceived(
            SystemsTelemetrySample systems)
        {
            _cache.PublishSystems(
                systems);

            _engineeringEngine.PublishElectricalTelemetry(
                systems.ElectricChargeAmount,
                systems.ElectricChargeCapacity,
                systems.ReceivedUtc);

            ElectricalAttributionModel attribution =
                new ElectricalAttributionModel();

            attribution.TelemetryAvailable =
                systems.AttributionEntries.Count >
                0;

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

                        PartId =
                            source.PartId,

                        PartTitle =
                            source.PartTitle,

                        Category =
                            source.Category,

                        Evidence =
                            ParseEvidence(
                                source.Evidence),

                        CurrentRateKnown =
                            source.CurrentKnown,

                        CurrentRateEcPerSecond =
                            source.CurrentRateEcPerSecond,

                        MaximumRateKnown =
                            source.MaximumKnown,

                        MaximumRateEcPerSecond =
                            source.MaximumRateEcPerSecond,

                        Enabled =
                            source.Enabled,

                        ActiveStateKnown =
                            source.ActiveKnown,

                        Active =
                            source.Active
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
                return
                    parsed;
            }

            return
                ElectricalRateEvidence.Unknown;
        }

        private void OnFlightTelemetryReceived(
            TelemetryPacket packet)
        {
            PropulsionDebugSnapshotStore
                .PublishTelemetry(
                    packet);

            AnalyzeEngineering(
                packet);

            Action<TelemetryPacket> handler =
                TelemetryReceived;

            if (handler != null)
            {
                handler(
                    packet);
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

                sequence =
                    _engineeringSequence;
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
            _cache.PublishTopology(
                topology);

            PropulsionDebugSnapshotStore
                .PublishTopology(
                    topology);

            try
            {
                PropulsionRenderGraphBuilder builder =
                    new PropulsionRenderGraphBuilder();

                PropulsionRenderGraph graph =
                    builder.Build(
                        topology);

                PropulsionGraphStore.Publish(
                    graph);

                PropulsionGraphFileLogger.Write(
                    graph);
            }
            catch (Exception ex)
            {
                PropulsionGraphFileLogger
                    .WriteError(
                        ex);
            }

            Action<VesselTopology> handler =
                TopologyReceived;

            if (handler != null)
            {
                handler(
                    topology);
            }
        }

        public void Stop()
        {
            if (!_running)
            {
                _transport.Stop();
                return;
            }

            _running =
                false;

            _transport.Stop();

            PropulsionGraphStore.Clear();
            PropulsionDebugSnapshotStore.Clear();
            EngineeringSnapshotStore.Clear();

            _engineeringEngine.ClearElectricalTelemetry();
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

            _transport.Dispose();
        }
    }
}
