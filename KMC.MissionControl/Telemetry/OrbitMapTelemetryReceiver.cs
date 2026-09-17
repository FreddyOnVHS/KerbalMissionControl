using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Shared;

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
            _client = new UdpClient(new IPEndPoint(IPAddress.Loopback, OrbitMapPacket.TelemetryPort));
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
                    OrbitMapSnapshotStore.SetLatest(packet, DateTime.UtcNow);
                    Action<OrbitMapPacket> handler = SnapshotReceived;
                    if (handler != null) handler(packet);
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { if (!_running) return; OrbitMapSnapshotStore.RecordRejected(); }
                catch { OrbitMapSnapshotStore.RecordRejected(); }
            }
        }

        public void Dispose() { Stop(); }
    }
}
