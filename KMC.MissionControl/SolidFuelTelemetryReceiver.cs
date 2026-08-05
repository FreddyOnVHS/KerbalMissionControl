using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.MissionControl.Telemetry;

namespace KMC.MissionControl
{
    public sealed class SolidFuelTelemetryReceiver :
        IDisposable
    {
        private const int Port = 5057;
        private const string ProtocolId = "KMC-SOLID1";

        private UdpClient _client;
        private Thread _thread;
        private volatile bool _running;

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _client =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Any,
                        Port));

            _running =
                true;

            _thread =
                new Thread(
                    ReceiveLoop)
                {
                    IsBackground =
                        true,

                    Name =
                        "KMC Solid Fuel Receiver"
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

                    string message =
                        Encoding.UTF8.GetString(
                            data);

                    SolidFuelTelemetrySnapshot snapshot;

                    if (TryParse(
                            message,
                            out snapshot))
                    {
                        SolidFuelTelemetryStore.Publish(
                            snapshot);
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (!_running)
                    {
                        return;
                    }
                }
            }
        }

        private static bool TryParse(
            string message,
            out SolidFuelTelemetrySnapshot snapshot)
        {
            snapshot =
                null;

            if (string.IsNullOrWhiteSpace(
                    message))
            {
                return false;
            }

            string[] parts =
                message.Split('|');

            if (parts.Length != 8 ||
                parts[0] !=
                    ProtocolId)
            {
                return false;
            }

            long ticks;
            double totalAmount;
            double totalCapacity;
            double activeAmount;
            double activeCapacity;
            int boosterCount;
            int burningCount;

            if (!long.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out ticks) ||
                !double.TryParse(
                    parts[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out totalAmount) ||
                !double.TryParse(
                    parts[3],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out totalCapacity) ||
                !double.TryParse(
                    parts[4],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out activeAmount) ||
                !double.TryParse(
                    parts[5],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out activeCapacity) ||
                !int.TryParse(
                    parts[6],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out boosterCount) ||
                !int.TryParse(
                    parts[7],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out burningCount))
            {
                return false;
            }

            snapshot =
                new SolidFuelTelemetrySnapshot
                {
                    TimestampUtc =
                        new DateTime(
                            ticks,
                            DateTimeKind.Utc),

                    TotalAmount =
                        totalAmount,

                    TotalCapacity =
                        totalCapacity,

                    ActiveAmount =
                        activeAmount,

                    ActiveCapacity =
                        activeCapacity,

                    BoosterCount =
                        boosterCount,

                    BurningBoosterCount =
                        burningCount
                };

            return true;
        }

        public void Stop()
        {
            _running =
                false;

            SolidFuelTelemetryStore.Clear();

            if (_client != null)
            {
                _client.Close();
                _client =
                    null;
            }

            if (_thread != null &&
                _thread.IsAlive &&
                Thread.CurrentThread !=
                    _thread)
            {
                _thread.Join(
                    1000);
            }

            _thread =
                null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
