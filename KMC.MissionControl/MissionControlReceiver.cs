using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Engine;
using KMC.Engine.Analysis;
using KMC.MissionControl.Debugging;
using KMC.MissionControl.Diagnostics;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Rendering.Propulsion;
using KMC.Shared;
using KMC.Shared.Topology;

namespace KMC.MissionControl
{
    public sealed class MissionControlReceiver :
        IDisposable
    {
        private readonly EngineeringEngine _engineeringEngine;
        private readonly object _engineeringSyncRoot;

        private UdpClient _telemetryClient;
        private UdpClient _topologyClient;
        private Thread _telemetryThread;
        private Thread _topologyThread;
        private volatile bool _running;
        private VesselTopology _latestTopology;
        private long _engineeringSequence;

        public MissionControlReceiver()
        {
            _engineeringEngine =
                new EngineeringEngine();

            _engineeringSyncRoot =
                new object();
        }

        public event Action<TelemetryPacket>
            TelemetryReceived;

        public event Action<VesselTopology>
            TopologyReceived;

        public void Start()
        {
            if (_running)
            {
                return;
            }

            EngineeringSnapshotStore.Clear();

            lock (_engineeringSyncRoot)
            {
                _latestTopology =
                    null;

                _engineeringSequence =
                    0;
            }

            _telemetryClient =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Any,
                        TelemetryPacket.TelemetryPort));

            _topologyClient =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Any,
                        VesselTopologyPacketCodec
                            .TopologyPort));

            _running =
                true;

            _telemetryThread =
                CreateThread(
                    TelemetryReceiveLoop,
                    "KMC Telemetry Receiver");

            _topologyThread =
                CreateThread(
                    TopologyReceiveLoop,
                    "KMC Topology Receiver");

            _telemetryThread.Start();
            _topologyThread.Start();
        }

        private static Thread CreateThread(
            ThreadStart action,
            string name)
        {
            return new Thread(action)
            {
                IsBackground = true,
                Name = name
            };
        }

        private void TelemetryReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender =
                        new IPEndPoint(
                            IPAddress.Any,
                            0);

                    byte[] data =
                        _telemetryClient.Receive(
                            ref sender);

                    string message =
                        Encoding.UTF8.GetString(
                            data);

                    TelemetryPacket packet;

                    if (TelemetryPacket.TryParse(
                            message,
                            out packet))
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
                            handler(packet);
                        }
                    }
                }
                catch (SocketException)
                {
                    if (_running)
                    {
                        throw;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private void AnalyzeEngineering(
            TelemetryPacket packet)
        {
            VesselTopology topology;
            long sequence;

            lock (_engineeringSyncRoot)
            {
                topology =
                    _latestTopology;

                if (topology == null)
                {
                    return;
                }

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
                /*
                 * Engineering analysis must never take down the UDP receiver.
                 * Milestone 7.1 is observational: failures are surfaced to the
                 * debugger while the existing Mission Control path continues.
                 */
                EngineeringSnapshotStore.ReportError(
                    ex);
            }
        }

        private void TopologyReceiveLoop()
        {
            PropulsionRenderGraphBuilder builder =
                new PropulsionRenderGraphBuilder();

            while (_running)
            {
                try
                {
                    IPEndPoint sender =
                        new IPEndPoint(
                            IPAddress.Any,
                            0);

                    byte[] data =
                        _topologyClient.Receive(
                            ref sender);

                    VesselTopology topology;

                    if (!VesselTopologyPacketCodec
                        .TryDecode(
                            data,
                            out topology))
                    {
                        continue;
                    }

                    lock (_engineeringSyncRoot)
                    {
                        _latestTopology =
                            topology;
                    }

                    PropulsionDebugSnapshotStore
                        .PublishTopology(
                            topology);

                    PropulsionRenderGraph graph =
                        builder.Build(
                            topology);

                    PropulsionGraphStore.Publish(
                        graph);

                    PropulsionGraphFileLogger.Write(
                        graph);

                    Action<VesselTopology> handler =
                        TopologyReceived;

                    if (handler != null)
                    {
                        handler(topology);
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (_running)
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    PropulsionGraphFileLogger
                        .WriteError(ex);
                }
            }
        }

        public void Stop()
        {
            _running =
                false;

            PropulsionGraphStore.Clear();
            PropulsionDebugSnapshotStore.Clear();
            EngineeringSnapshotStore.Clear();

            lock (_engineeringSyncRoot)
            {
                _latestTopology =
                    null;
            }

            CloseClient(
                ref _telemetryClient);

            CloseClient(
                ref _topologyClient);

            JoinThread(
                ref _telemetryThread);

            JoinThread(
                ref _topologyThread);
        }

        private static void CloseClient(
            ref UdpClient client)
        {
            if (client == null)
            {
                return;
            }

            client.Close();

            client =
                null;
        }

        private static void JoinThread(
            ref Thread thread)
        {
            if (thread != null &&
                thread.IsAlive &&
                Thread.CurrentThread != thread)
            {
                thread.Join(
                    1000);
            }

            thread =
                null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
