using KMC.Shared.Topology;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace KMC.MissionControl.Telemetry
{
    internal sealed class VesselSchematicSnapshot
    {
        public VesselTopology Topology { get; set; }
        public DateTime ReceivedUtc { get; set; }
    }

    internal static class VesselSchematicTelemetryStore
    {
        private static readonly object SyncRoot =
            new object();

        private static VesselSchematicSnapshot _latest;

        public static void Publish(
            VesselTopology topology,
            DateTime receivedUtc)
        {
            if (topology == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _latest =
                    new VesselSchematicSnapshot
                    {
                        Topology = topology,
                        ReceivedUtc = receivedUtc
                    };
            }
        }

        public static VesselSchematicSnapshot GetLatest()
        {
            lock (SyncRoot)
            {
                return _latest;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest = null;
            }
        }
    }

    internal sealed class VesselSchematicTelemetryReceiver :
        IDisposable
    {
        private UdpClient _client;
        private Thread _thread;
        private volatile bool _running;

        public event Action<VesselTopology> SnapshotReceived;

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _client =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Loopback,
                        VesselTopologyPacketCodec.TopologyPort));

            _running = true;

            _thread =
                new Thread(
                    ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "KMC Vessel Schematic Telemetry"
                };

            _thread.Start();
        }

        private void ReceiveLoop()
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
                        _client.Receive(
                            ref sender);

                    VesselTopology topology;

                    if (!VesselTopologyPacketCodec.TryDecode(
                            data,
                            out topology) ||
                        topology == null)
                    {
                        continue;
                    }

                    DateTime receivedUtc =
                        DateTime.UtcNow;

                    VesselSchematicTelemetryStore.Publish(
                        topology,
                        receivedUtc);

                    Action<VesselTopology> handler =
                        SnapshotReceived;

                    if (handler != null)
                    {
                        handler(topology);
                    }
                }
                catch (SocketException)
                {
                    if (!_running)
                    {
                        return;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch
                {
                    if (!_running)
                    {
                        return;
                    }
                }
            }
        }

        public void Dispose()
        {
            _running = false;

            if (_client != null)
            {
                try
                {
                    _client.Close();
                }
                catch
                {
                }

                _client = null;
            }

            if (_thread != null &&
                _thread.IsAlive)
            {
                _thread.Join(250);
            }

            _thread = null;
        }
    }
}