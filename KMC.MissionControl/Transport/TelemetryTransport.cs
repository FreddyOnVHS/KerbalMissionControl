using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Shared;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Transport
{
    /// <summary>
    /// Owns all Mission Control UDP sockets.
    ///
    /// One socket is created per UNIQUE port. Incoming datagrams are
    /// demultiplexed by protocol, so overlapping KMC channel ports can never
    /// cause a duplicate bind inside Mission Control.
    /// </summary>
    public sealed class TelemetryTransport :
        IDisposable
    {
        private const int SystemsTelemetryPort = 5091;
        private const string SystemsProtocolId = "KMCSYS1";

        private readonly Dictionary<int, Channel> _channels =
            new Dictionary<int, Channel>();

        private volatile bool _running;

        public event Action<TelemetryPacket> FlightTelemetryReceived;
        public event Action<VesselTopology> TopologyReceived;
        public event Action<SystemsTelemetrySample> SystemsTelemetryReceived;

        public void Start()
        {
            if (_running)
            {
                return;
            }

            int[] requestedPorts =
            {
                TelemetryPacket.TelemetryPort,
                VesselTopologyPacketCodec.TopologyPort,
                SystemsTelemetryPort
            };

            _running =
                true;

            try
            {
                for (int i = 0;
                     i < requestedPorts.Length;
                     i++)
                {
                    EnsureChannel(
                        requestedPorts[i]);
                }

                foreach (Channel channel
                    in _channels.Values)
                {
                    channel.Thread.Start();
                }
            }
            catch
            {
                Stop();
                throw;
            }
        }

        private void EnsureChannel(
            int port)
        {
            if (_channels.ContainsKey(
                    port))
            {
                return;
            }

            UdpClient client;

            try
            {
                client =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Any,
                            port));
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException(
                    "KMC UDP transport could not bind port " +
                    port +
                    ". SocketError=" +
                    ex.SocketErrorCode +
                    " (" +
                    ex.ErrorCode +
                    ").",
                    ex);
            }

            Debug.WriteLine(
                "KMC.Transport BOUND | UDP " +
                port);

            Channel channel =
                new Channel();

            channel.Port =
                port;

            channel.Client =
                client;

            ThreadStart receiveStart =
                delegate
                {
                    ReceiveLoop(
                        channel);
                };

            channel.Thread =
                new Thread(
                    receiveStart);

            channel.Thread.IsBackground =
                true;

            channel.Thread.Name =
                "KMC UDP Transport " +
                port;

            _channels.Add(
                port,
                channel);
        }

        private void ReceiveLoop(
            Channel channel)
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
                        channel.Client.Receive(
                            ref sender);

                    Dispatch(
                        data);
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

                    throw;
                }
            }
        }

        private void Dispatch(
            byte[] data)
        {
            VesselTopology topology;

            if (VesselTopologyPacketCodec.TryDecode(
                    data,
                    out topology))
            {
                Action<VesselTopology> topologyHandler =
                    TopologyReceived;

                if (topologyHandler != null)
                {
                    topologyHandler(
                        topology);
                }

                return;
            }

            string text =
                Encoding.UTF8.GetString(
                    data);

            SystemsTelemetrySample systems;

            if (TryParseSystems(
                    text,
                    out systems))
            {
                Action<SystemsTelemetrySample> systemsHandler =
                    SystemsTelemetryReceived;

                if (systemsHandler != null)
                {
                    systemsHandler(
                        systems);
                }

                return;
            }

            TelemetryPacket packet;

            if (TelemetryPacket.TryParse(
                    text,
                    out packet))
            {
                Action<TelemetryPacket> telemetryHandler =
                    FlightTelemetryReceived;

                if (telemetryHandler != null)
                {
                    telemetryHandler(
                        packet);
                }
            }
        }

        private static bool TryParseSystems(
            string message,
            out SystemsTelemetrySample sample)
        {
            sample =
                null;

            if (string.IsNullOrWhiteSpace(
                    message))
            {
                return
                    false;
            }

            string[] fields =
                message.Split(
                    '|');

            if (fields.Length < 5 ||
                !string.Equals(
                    fields[0],
                    SystemsProtocolId,
                    StringComparison.Ordinal))
            {
                return
                    false;
            }

            double amount;
            double capacity;
            double thermal;

            if (!TryDouble(
                    fields[1],
                    out amount) ||
                !TryDouble(
                    fields[2],
                    out capacity) ||
                !TryDouble(
                    fields[3],
                    out thermal))
            {
                return
                    false;
            }

            sample =
                new SystemsTelemetrySample
                {
                    ReceivedUtc =
                        DateTime.UtcNow,

                    ElectricChargeAmount =
                        Math.Max(
                            0.0,
                            amount),

                    ElectricChargeCapacity =
                        Math.Max(
                            0.0,
                            capacity),

                    MaximumThermalRatio =
                        Math.Max(
                            0.0,
                            thermal),

                    IsDocked =
                        fields[4] ==
                        "1"
                };

            if (fields.Length >= 6 &&
                !string.IsNullOrWhiteSpace(
                    fields[5]))
            {
                ParseAttribution(
                    fields[5],
                    sample);
            }

            return
                true;
        }

        private static void ParseAttribution(
            string encoded,
            SystemsTelemetrySample sample)
        {
            try
            {
                byte[] bytes =
                    Convert.FromBase64String(
                        encoded);

                string plain =
                    Encoding.UTF8.GetString(
                        bytes);

                if (string.IsNullOrEmpty(
                        plain))
                {
                    return;
                }

                string[] entries =
                    plain.Split(
                        ';');

                for (int index = 0;
                     index < entries.Length;
                     index++)
                {
                    string[] fields =
                        entries[index].Split(
                            '~');

                    if (fields.Length < 12)
                    {
                        continue;
                    }

                    uint partId;
                    double currentRate;
                    double maximumRate;

                    if (!uint.TryParse(
                            fields[1],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out partId) ||
                        !TryDouble(
                            fields[5],
                            out currentRate) ||
                        !TryDouble(
                            fields[7],
                            out maximumRate))
                    {
                        continue;
                    }

                    sample.AttributionEntries.Add(
                        new SystemsAttributionEntry
                        {
                            IsProducer =
                                fields[0] ==
                                "P",

                            PartId =
                                partId,

                            Category =
                                fields[2] ??
                                string.Empty,

                            Evidence =
                                fields[3] ??
                                string.Empty,

                            CurrentKnown =
                                fields[4] ==
                                "1",

                            CurrentRateEcPerSecond =
                                Math.Max(
                                    0.0,
                                    currentRate),

                            MaximumKnown =
                                fields[6] ==
                                "1",

                            MaximumRateEcPerSecond =
                                Math.Max(
                                    0.0,
                                    maximumRate),

                            Enabled =
                                fields[8] ==
                                "1",

                            ActiveKnown =
                                fields[9] ==
                                "1",

                            Active =
                                fields[10] ==
                                "1",

                            PartTitle =
                                fields[11] ??
                                string.Empty
                        });
                }
            }
            catch
            {
                /*
                 * Attribution is optional telemetry. Malformed attribution
                 * must not invalidate the core KMCSYS1 systems packet.
                 */
            }
        }

        private static bool TryDouble(
            string value,
            out double result)
        {
            return
                double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result);
        }

        public void Stop()
        {
            _running =
                false;

            foreach (Channel channel
                in _channels.Values)
            {
                if (channel.Client != null)
                {
                    try
                    {
                        channel.Client.Close();
                    }
                    catch
                    {
                    }
                }
            }

            foreach (Channel channel
                in _channels.Values)
            {
                if (channel.Thread != null &&
                    channel.Thread.IsAlive &&
                    Thread.CurrentThread !=
                        channel.Thread)
                {
                    channel.Thread.Join(
                        1000);
                }
            }

            _channels.Clear();
        }

        public void Dispose()
        {
            Stop();
        }

        private sealed class Channel
        {
            public int Port;
            public UdpClient Client;
            public Thread Thread;
        }
    }
}
