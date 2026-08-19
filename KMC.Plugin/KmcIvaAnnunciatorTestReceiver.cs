using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Shared;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// Build 14.18.2 loopback-only IVA annunciator test receiver.
    /// Test state is separate from telemetry and failure truth.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class KmcIvaAnnunciatorTestReceiver : MonoBehaviour
    {
        private sealed class TestState
        {
            public bool Active;
            public DateTime UpdatedUtc;
        }

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, Dictionary<IvaAnnunciatorTestId, TestState>> States =
            new Dictionary<string, Dictionary<IvaAnnunciatorTestId, TestState>>(StringComparer.Ordinal);
        private static readonly TimeSpan SafetyLease = TimeSpan.FromMinutes(30.0);

        private UdpClient _udp;
        private Thread _thread;
        private volatile bool _running;

        public void Awake()
        {
            lock (SyncRoot)
                States.Clear();

            try
            {
                _udp =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            IvaAnnunciatorTestPacket.CommandPort));

                _udp.Client.ReceiveTimeout = 500;
                _running = true;
                _thread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "KMC IVA Annunciator Test"
                };
                _thread.Start();

                Debug.Log(
                    "[KMC] IVA annunciator test receiver started on loopback UDP " +
                    IvaAnnunciatorTestPacket.CommandPort.ToString() +
                    ".");
            }
            catch (Exception ex)
            {
                _running = false;
                Debug.LogWarning(
                    "[KMC] IVA annunciator test receiver unavailable: " +
                    ex.GetType().Name +
                    " / " +
                    ex.Message);
            }
        }

        public void OnDestroy()
        {
            _running = false;
            try { if (_udp != null) _udp.Close(); } catch { }
            try { if (_thread != null && _thread.IsAlive) _thread.Join(750); } catch { }

            lock (SyncRoot)
                States.Clear();

            _udp = null;
            _thread = null;
        }

        public static bool IsActive(
            string vesselId,
            IvaAnnunciatorTestId testId)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return false;

            // A test override may exist only while the established KMC MFD
            // status heartbeat for this vessel is live. Link loss clears all
            // test state so a stale test cannot reappear after reconnect.
            KmcMfdStatusPacket status;
            if (!KmcMfdStatusReceiver.TryGetStatus(vesselId, out status))
            {
                lock (SyncRoot)
                    States.Remove(vesselId);
                return false;
            }

            lock (SyncRoot)
            {
                Dictionary<IvaAnnunciatorTestId, TestState> vesselStates;
                if (!States.TryGetValue(vesselId, out vesselStates) ||
                    vesselStates == null)
                    return false;

                TestState state;
                if (!vesselStates.TryGetValue(testId, out state) ||
                    state == null)
                    return false;

                if ((DateTime.UtcNow - state.UpdatedUtc) > SafetyLease)
                {
                    vesselStates.Remove(testId);
                    if (vesselStates.Count == 0)
                        States.Remove(vesselId);
                    return false;
                }

                return state.Active;
            }
        }

        private void ReceiveLoop()
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Loopback, 0);

            while (_running)
            {
                try
                {
                    byte[] bytes = _udp.Receive(ref remote);
                    if (bytes == null ||
                        bytes.Length == 0 ||
                        remote == null ||
                        !IPAddress.IsLoopback(remote.Address))
                        continue;

                    IvaAnnunciatorTestPacket packet;
                    if (!IvaAnnunciatorTestPacket.TryParse(
                            Encoding.UTF8.GetString(bytes),
                            out packet))
                        continue;

                    Apply(packet);
                }
                catch (SocketException ex)
                {
                    if (!_running)
                        return;
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                        continue;
                    Debug.LogWarning(
                        "[KMC] IVA annunciator test receive error: " +
                        ex.SocketErrorCode.ToString());
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (_running)
                        Debug.LogWarning(
                            "[KMC] IVA annunciator test receive exception: " +
                            ex.GetType().Name);
                }
            }
        }

        private static void Apply(
            IvaAnnunciatorTestPacket packet)
        {
            if (packet == null ||
                string.IsNullOrWhiteSpace(packet.VesselId))
                return;

            lock (SyncRoot)
            {
                if (packet.Operation == IvaAnnunciatorTestOperation.ClearAll)
                {
                    States.Remove(packet.VesselId);
                    return;
                }

                Dictionary<IvaAnnunciatorTestId, TestState> vesselStates;
                if (!States.TryGetValue(packet.VesselId, out vesselStates) ||
                    vesselStates == null)
                {
                    vesselStates =
                        new Dictionary<IvaAnnunciatorTestId, TestState>();
                    States[packet.VesselId] = vesselStates;
                }

                vesselStates[packet.TestId] =
                    new TestState
                    {
                        Active =
                            packet.Operation == IvaAnnunciatorTestOperation.On,
                        UpdatedUtc = DateTime.UtcNow
                    };
            }
        }
    }
}
