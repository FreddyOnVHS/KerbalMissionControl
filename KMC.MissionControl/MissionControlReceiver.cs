using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.MissionControl.Diagnostics;
using KMC.MissionControl.Rendering.Propulsion;
using KMC.Shared;
using KMC.Shared.Topology;

namespace KMC.MissionControl
{
    public sealed class MissionControlReceiver :
        IDisposable
    {
        private UdpClient _telemetryClient;
        private UdpClient _topologyClient;

        private Thread _telemetryThread;
        private Thread _topologyThread;

        private volatile bool _running;

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

            _running = true;

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
                        Encoding.UTF8.GetString(data);

                    TelemetryPacket packet;

                    if (TelemetryPacket.TryParse(
                            message,
                            out packet))
                    {
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

                    PropulsionRenderGraph graph =
                        builder.Build(topology);

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
            _running = false;

            CloseClient(ref _telemetryClient);
            CloseClient(ref _topologyClient);

            JoinThread(ref _telemetryThread);
            JoinThread(ref _topologyThread);
        }

        private static void CloseClient(
            ref UdpClient client)
        {
            if (client == null)
            {
                return;
            }

            client.Close();
            client = null;
        }

        private static void JoinThread(
            ref Thread thread)
        {
            if (thread != null &&
                thread.IsAlive &&
                Thread.CurrentThread != thread)
            {
                thread.Join(1000);
            }

            thread = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
