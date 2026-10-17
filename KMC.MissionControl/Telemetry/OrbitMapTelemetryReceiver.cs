using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Shared;
using KMC.Engine.Orbit;

namespace KMC.MissionControl.Telemetry
{
    public sealed class OrbitMapTelemetryReceiver : IDisposable
    {
        private UdpClient _client;
        private Thread _thread;
        private volatile bool _running;
        public event Action<OrbitMapPacket> SnapshotReceived;

        public void Start()
        {
            if (_running) return;
            try
            {
                _client =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            OrbitMapPacket.TelemetryPort));
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException(
                    "KMC UDP BIND FAILED | ORBIT MAP | UDP " +
                    OrbitMapPacket.TelemetryPort +
                    " | " +
                    ex.SocketErrorCode +
                    " | " +
                    ex.Message,
                    ex);
            }
            _running = true;
            _thread = new Thread(ReceiveLoop);
            _thread.IsBackground = true;
            _thread.Name = "KMC Orbit Map Telemetry";
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            if (_client != null) { _client.Close(); _client = null; }
            if (_thread != null && _thread.IsAlive) _thread.Join(500);
            _thread = null;
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _client.Receive(ref sender);
                    string text = Encoding.UTF8.GetString(data);
                    OrbitMapPacket packet;
                    if (!OrbitMapPacket.TryParse(text, out packet))
                    {
                        OrbitMapSnapshotStore.RecordRejected();
                        continue;
                    }
                    DateTime receivedUtc =
                        DateTime.UtcNow;

                    OrbitMapSnapshotStore.SetLatest(
                        packet,
                        receivedUtc);

                    PublishAuthoritativeManeuverVectors(
                        packet,
                        receivedUtc);

                    Action<OrbitMapPacket> handler = SnapshotReceived;
                    if (handler != null) handler(packet);
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { if (!_running) return; OrbitMapSnapshotStore.RecordRejected(); }
                catch { OrbitMapSnapshotStore.RecordRejected(); }
            }
        }

        private static void PublishAuthoritativeManeuverVectors(
            OrbitMapPacket packet,
            DateTime receivedUtc)
        {
            if (packet == null ||
                packet.ManeuverNodes == null ||
                string.IsNullOrWhiteSpace(
                    packet.VesselId))
            {
                return;
            }

            for (int index = 0;
                 index < packet.ManeuverNodes.Count;
                 index++)
            {
                OrbitMapManeuverNode node =
                    packet.ManeuverNodes[index];

                if (node == null ||
                    !node.AuthoritativeBurnVectorAvailable)
                {
                    continue;
                }

                ManeuverVectorTelemetryStore.Publish(
                    new ManeuverVectorTelemetryModel
                    {
                        TelemetryAvailable = true,
                        SourceTimestampUtc =
                            packet.TimestampUtc,
                        ReceivedUtc =
                            receivedUtc,
                        VesselId =
                            packet.VesselId,
                        NodeUniversalTimeSeconds =
                            node.UniversalTimeSeconds,
                        RightComponent =
                            node.AuthoritativeBurnRight,
                        NoseComponent =
                            node.AuthoritativeBurnNose,
                        ReferenceForwardComponent =
                            node.AuthoritativeBurnReferenceForward,
                        BurnMagnitudeMetersPerSecond =
                            node.TotalDeltaVMetersPerSecond
                    });
            }
        }
        public void Dispose() { Stop(); }
    }
}
