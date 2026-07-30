using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Shared;

namespace KMC.MissionControl
{
    public sealed class MissionControlReceiver : IDisposable
    {
        private UdpClient _udpClient;
        private Thread _receiveThread;
        private volatile bool _running;

        public event Action<TelemetryPacket> TelemetryReceived;

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _udpClient = new UdpClient(
                new IPEndPoint(IPAddress.Any, TelemetryPacket.TelemetryPort));

            _running = true;

            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "KMC Telemetry Receiver"
            };

            _receiveThread.Start();
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);

                    byte[] data = _udpClient.Receive(ref sender);

                    string message = Encoding.UTF8.GetString(data);

                    TelemetryPacket packet;

                    if (TelemetryPacket.TryParse(message, out packet))
                    {
                        Action<TelemetryPacket> handler = TelemetryReceived;

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

        public void Stop()
        {
            _running = false;

            if (_udpClient != null)
            {
                _udpClient.Close();
                _udpClient = null;
            }

            if (_receiveThread != null &&
                _receiveThread.IsAlive &&
                Thread.CurrentThread != _receiveThread)
            {
                _receiveThread.Join(1000);
            }

            _receiveThread = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}