using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace KMC.MissionControl.Transport
{
    public sealed class VelocityVectorTelemetrySample
    {
        public DateTime SourceTimestampUtc { get; set; }

        public DateTime ReceivedUtc { get; set; }

        public string VesselName { get; set; } =
            string.Empty;

        public double SurfaceRightMetersPerSecond { get; set; }

        public double SurfaceNoseMetersPerSecond { get; set; }

        public double SurfaceReferenceForwardMetersPerSecond
        {
            get;
            set;
        }

        public double OrbitalRightMetersPerSecond { get; set; }

        public double OrbitalNoseMetersPerSecond { get; set; }

        public double OrbitalReferenceForwardMetersPerSecond
        {
            get;
            set;
        }
    }

    public sealed class OrbitNormalTelemetrySample
    {
        public DateTime SourceTimestampUtc { get; set; }

        public DateTime ReceivedUtc { get; set; }

        public string VesselName { get; set; } =
            string.Empty;

        public double RightComponent { get; set; }

        public double NoseComponent { get; set; }

        public double ReferenceForwardComponent { get; set; }
    }

    public sealed class RadialTelemetrySample
    {
        public DateTime SourceTimestampUtc { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public string VesselName { get; set; } = string.Empty;
        public double RightComponent { get; set; }
        public double NoseComponent { get; set; }
        public double ReferenceForwardComponent { get; set; }
    }

    /// <summary>
    /// MissionControl-owned receiver for Build 13.4 KMC-NORM1.
    /// Uses its own UDP 5098 socket so the existing KMC-VEL1 transport and
    /// parser remain frozen and backward-compatible.
    /// </summary>
    public sealed class OrbitNormalTelemetryReceiver :
        IDisposable
    {
        private const int Port =
            5098;

        private const string ProtocolId =
            "KMC-NORM1";

        private UdpClient _client;
        private Thread _thread;
        private volatile bool _running;

        public event Action<OrbitNormalTelemetrySample>
            SampleReceived;

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
                    ReceiveLoop);

            _thread.IsBackground =
                true;

            _thread.Name =
                "KMC Orbit Normal UDP " +
                Port;

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

                    OrbitNormalTelemetrySample sample;

                    if (!TryParse(
                            data,
                            out sample))
                    {
                        continue;
                    }

                    Action<OrbitNormalTelemetrySample> handler =
                        SampleReceived;

                    if (handler != null)
                    {
                        handler(
                            sample);
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
            byte[] data,
            out OrbitNormalTelemetrySample sample)
        {
            sample =
                null;

            if (data == null ||
                data.Length == 0)
            {
                return false;
            }

            string message =
                Encoding.UTF8.GetString(
                    data);

            string[] fields =
                message.Split(
                    '|');

            if (fields.Length != 6 ||
                !string.Equals(
                    fields[0],
                    ProtocolId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            long ticks;
            double right;
            double nose;
            double forward;

            if (!long.TryParse(
                    fields[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out ticks) ||
                !double.TryParse(
                    fields[3],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out right) ||
                !double.TryParse(
                    fields[4],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out nose) ||
                !double.TryParse(
                    fields[5],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out forward))
            {
                return false;
            }

            DateTime sourceUtc;

            try
            {
                sourceUtc =
                    new DateTime(
                        ticks,
                        DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            sample =
                new OrbitNormalTelemetrySample
                {
                    SourceTimestampUtc =
                        sourceUtc,

                    ReceivedUtc =
                        DateTime.UtcNow,

                    VesselName =
                        Uri.UnescapeDataString(
                            fields[2]),

                    RightComponent =
                        right,

                    NoseComponent =
                        nose,

                    ReferenceForwardComponent =
                        forward
                };

            return true;
        }

        public void Dispose()
        {
            _running =
                false;

            UdpClient client =
                _client;

            _client =
                null;

            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                }
            }
        }
    }
    /// <summary>
    /// MissionControl-owned receiver for Build 13.5 KMC-RAD1 on UDP 5099.
    /// </summary>
    public sealed class RadialTelemetryReceiver : IDisposable
    {
        private const int Port = 5099;
        private const string ProtocolId = "KMC-RAD1";
        private UdpClient _client;
        private Thread _thread;
        private volatile bool _running;

        public event Action<RadialTelemetrySample> SampleReceived;

        public void Start()
        {
            if (_running) return;
            _client = new UdpClient(new IPEndPoint(IPAddress.Any, Port));
            _running = true;
            _thread = new Thread(ReceiveLoop);
            _thread.IsBackground = true;
            _thread.Name = "KMC Radial UDP " + Port;
            _thread.Start();
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _client.Receive(ref sender);
                    RadialTelemetrySample sample;
                    if (!TryParse(data, out sample)) continue;
                    Action<RadialTelemetrySample> handler = SampleReceived;
                    if (handler != null) handler(sample);
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { if (!_running) return; }
            }
        }

        private static bool TryParse(byte[] data, out RadialTelemetrySample sample)
        {
            sample = null;
            if (data == null || data.Length == 0) return false;
            string[] fields = Encoding.UTF8.GetString(data).Split('|');
            if (fields.Length != 6 ||
                !string.Equals(fields[0], ProtocolId, StringComparison.Ordinal)) return false;

            long ticks; double right, nose, forward;
            if (!long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) ||
                !double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out right) ||
                !double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out nose) ||
                !double.TryParse(fields[5], NumberStyles.Float, CultureInfo.InvariantCulture, out forward)) return false;

            DateTime sourceUtc;
            try { sourceUtc = new DateTime(ticks, DateTimeKind.Utc); }
            catch (ArgumentOutOfRangeException) { return false; }

            sample = new RadialTelemetrySample
            {
                SourceTimestampUtc = sourceUtc,
                ReceivedUtc = DateTime.UtcNow,
                VesselName = Uri.UnescapeDataString(fields[2]),
                RightComponent = right,
                NoseComponent = nose,
                ReferenceForwardComponent = forward
            };
            return true;
        }

        public void Dispose()
        {
            _running = false;
            UdpClient client = _client;
            _client = null;
            if (client != null) { try { client.Close(); } catch { } }
        }
    }

}
