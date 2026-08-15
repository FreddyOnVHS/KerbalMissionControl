using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Shared;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// Build 14.15.1 KSP-side cache for Mission Control's read-only MFD status.
    ///
    /// The cache is vessel-qualified and lease-based. If Mission Control stops
    /// publishing, IVA variables become unavailable after 2.5 real seconds
    /// instead of displaying stale electrical state.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class KmcMfdStatusReceiver :
        MonoBehaviour
    {
        private static readonly object StatusSync =
            new object();

        private static readonly TimeSpan LeaseDuration =
            TimeSpan.FromSeconds(2.50);

        private static KmcMfdStatusPacket _latestPacket;
        private static DateTime _latestReceivedUtc =
            DateTime.MinValue;

        private UdpClient _receiveClient;
        private Thread _receiveThread;
        private volatile bool _running;

        public void Start()
        {
            try
            {
                ClearStatus();

                _receiveClient =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            KmcMfdStatusPacket.StatusPort));

                _running = true;

                _receiveThread =
                    new Thread(
                        ReceiveLoop)
                    {
                        IsBackground = true,
                        Name = "KMC MFD Status"
                    };

                _receiveThread.Start();

                Debug.Log(
                    "[KMC] MFD status receiver started on UDP " +
                    KmcMfdStatusPacket.StatusPort.ToString() +
                    ".");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] MFD status receiver start failed: " +
                    ex);
            }
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
                        _receiveClient.Receive(
                            ref sender);

                    string text =
                        Encoding.UTF8.GetString(
                            data);

                    KmcMfdStatusPacket packet;

                    if (!KmcMfdStatusPacket.TryParse(
                            text,
                            out packet))
                    {
                        continue;
                    }

                    lock (StatusSync)
                    {
                        _latestPacket =
                            packet;

                        _latestReceivedUtc =
                            DateTime.UtcNow;
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
                catch (Exception ex)
                {
                    Debug.LogError(
                        "[KMC] MFD status receive failed: " +
                        ex.GetType().Name);
                }
            }
        }

        internal static bool TryGetStatus(
            string vesselId,
            out KmcMfdStatusPacket packet)
        {
            packet = null;

            if (string.IsNullOrWhiteSpace(
                    vesselId))
            {
                return false;
            }

            lock (StatusSync)
            {
                if (_latestPacket == null ||
                    _latestReceivedUtc ==
                        DateTime.MinValue ||
                    DateTime.UtcNow -
                        _latestReceivedUtc >
                        LeaseDuration ||
                    !string.Equals(
                        _latestPacket.VesselId,
                        vesselId,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                /*
                 * Packet instances are replaced, never mutated after receipt.
                 * Returning this immutable-in-practice snapshot is safe.
                 */
                packet =
                    _latestPacket;

                return true;
            }
        }

        private static void ClearStatus()
        {
            lock (StatusSync)
            {
                _latestPacket = null;
                _latestReceivedUtc =
                    DateTime.MinValue;
            }
        }

        public void OnDestroy()
        {
            _running = false;

            UdpClient client =
                _receiveClient;

            _receiveClient = null;

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

            Thread thread =
                _receiveThread;

            _receiveThread = null;

            if (thread != null &&
                thread.IsAlive)
            {
                try
                {
                    thread.Join(250);
                }
                catch
                {
                }
            }

            ClearStatus();
        }
    }
}
