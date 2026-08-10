using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Shared;

namespace KMC.MissionControl.Transport
{
    /// <summary>
    /// Owns Build 11.2 maneuver epoch and maneuver uplink sockets.
    /// Incoming network work stays off the UI thread; KSP mutation remains
    /// exclusively inside KMC.Plugin.
    /// </summary>
    public sealed class ManeuverLinkTransport :
        IDisposable
    {
        private UdpClient _epochClient;
        private UdpClient _ackClient;
        private UdpClient _commandClient;
        private Thread _epochThread;
        private Thread _ackThread;
        private volatile bool _running;

        public event Action<ManeuverEpochPacket> EpochReceived;
        public event Action<ManeuverUplinkAck> AcknowledgmentReceived;

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _epochClient =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Any,
                        ManeuverEpochPacket.TelemetryPort));

            _ackClient =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Any,
                        ManeuverUplinkPacket.AckPort));

            _commandClient =
                new UdpClient();

            Debug.WriteLine(
                "KMC.Transport BOUND | UDP " +
                ManeuverEpochPacket.TelemetryPort);

            Debug.WriteLine(
                "KMC.Transport BOUND | UDP " +
                ManeuverUplinkPacket.AckPort);

            _running = true;

            _epochThread =
                new Thread(
                    EpochReceiveLoop);

            _epochThread.IsBackground = true;
            _epochThread.Name = "KMC Maneuver Epoch";

            _ackThread =
                new Thread(
                    AckReceiveLoop);

            _ackThread.IsBackground = true;
            _ackThread.Name = "KMC Maneuver ACK";

            _epochThread.Start();
            _ackThread.Start();
        }

        public void Send(
            ManeuverUplinkPacket packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(
                    nameof(packet));
            }

            if (!_running ||
                _commandClient == null)
            {
                throw new InvalidOperationException(
                    "Maneuver link is not running.");
            }

            byte[] data =
                Encoding.UTF8.GetBytes(
                    packet.Serialize());

            _commandClient.Send(
                data,
                data.Length,
                new IPEndPoint(
                    IPAddress.Loopback,
                    ManeuverUplinkPacket.CommandPort));

            Debug.WriteLine(
                "KMC.MissionControl MANEUVER UPLINK SENT" +
                " | PlanId=" + packet.PlanId +
                " | VesselId=" + packet.VesselId +
                " | NodeUT=" +
                    packet.NodeUniversalTimeSeconds.ToString("0.0") +
                " | ProgradeDV=" +
                    packet.ProgradeDeltaVMetersPerSecond.ToString("0.00") +
                " | NormalDV=" +
                    packet.NormalDeltaVMetersPerSecond.ToString("0.00") +
                " | RadialDV=" +
                    packet.RadialDeltaVMetersPerSecond.ToString("0.00"));
        }

        private void EpochReceiveLoop()
        {
            ReceiveLoop(
                _epochClient,
                HandleEpoch);
        }

        private void AckReceiveLoop()
        {
            ReceiveLoop(
                _ackClient,
                HandleAck);
        }

        private void ReceiveLoop(
            UdpClient client,
            Action<string> handler)
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
                        client.Receive(
                            ref sender);

                    handler(
                        Encoding.UTF8.GetString(
                            data));
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

        private void HandleEpoch(
            string text)
        {
            ManeuverEpochPacket packet;

            if (!ManeuverEpochPacket.TryParse(
                    text,
                    out packet))
            {
                return;
            }

            Action<ManeuverEpochPacket> handler =
                EpochReceived;

            if (handler != null)
            {
                handler(packet);
            }
        }

        private void HandleAck(
            string text)
        {
            ManeuverUplinkAck ack;

            if (!ManeuverUplinkAck.TryParse(
                    text,
                    out ack))
            {
                return;
            }

            Debug.WriteLine(
                "KMC.MissionControl MANEUVER ACK" +
                " | PlanId=" + ack.PlanId +
                " | VesselId=" + ack.VesselId +
                " | Status=" + ack.Status +
                " | NodeUT=" +
                    (double.IsNaN(ack.NodeUniversalTimeSeconds)
                        ? "N/A"
                        : ack.NodeUniversalTimeSeconds.ToString("0.0")) +
                " | Detail=" + ack.Detail);

            Action<ManeuverUplinkAck> handler =
                AcknowledgmentReceived;

            if (handler != null)
            {
                handler(ack);
            }
        }

        public void Stop()
        {
            _running = false;

            CloseClient(ref _epochClient);
            CloseClient(ref _ackClient);
            CloseClient(ref _commandClient);

            JoinThread(ref _epochThread);
            JoinThread(ref _ackThread);
        }

        public void Dispose()
        {
            Stop();
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
                thread.IsAlive)
            {
                thread.Join(250);
            }

            thread = null;
        }
    }
}
