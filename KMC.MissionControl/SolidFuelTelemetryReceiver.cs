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
        private const string ProtocolId = "KMC-SOLID2";

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

            if (parts.Length != 14 ||
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
            double leftAmount;
            double leftCapacity;
            int leftBurning;
            double rightAmount;
            double rightCapacity;
            int rightBurning;

            int index =
                1;

            if (!TryLong(
                    parts[index++],
                    out ticks) ||
                !TryDouble(
                    parts[index++],
                    out totalAmount) ||
                !TryDouble(
                    parts[index++],
                    out totalCapacity) ||
                !TryDouble(
                    parts[index++],
                    out activeAmount) ||
                !TryDouble(
                    parts[index++],
                    out activeCapacity) ||
                !TryInt(
                    parts[index++],
                    out boosterCount) ||
                !TryInt(
                    parts[index++],
                    out burningCount) ||
                !TryDouble(
                    parts[index++],
                    out leftAmount) ||
                !TryDouble(
                    parts[index++],
                    out leftCapacity) ||
                !TryInt(
                    parts[index++],
                    out leftBurning) ||
                !TryDouble(
                    parts[index++],
                    out rightAmount) ||
                !TryDouble(
                    parts[index++],
                    out rightCapacity) ||
                !TryInt(
                    parts[index++],
                    out rightBurning))
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
                        burningCount,

                    LeftAmount =
                        leftAmount,

                    LeftCapacity =
                        leftCapacity,

                    LeftBurning =
                        leftBurning != 0,

                    RightAmount =
                        rightAmount,

                    RightCapacity =
                        rightCapacity,

                    RightBurning =
                        rightBurning != 0
                };

            return true;
        }

        private static bool TryDouble(
            string value,
            out double result)
        {
            return double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static bool TryInt(
            string value,
            out int result)
        {
            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static bool TryLong(
            string value,
            out long result)
        {
            return long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result);
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
