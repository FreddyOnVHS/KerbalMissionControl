using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using KMC.Shared;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// Build 14.13.4 KSP executor for KMC-owned normal electrical load.
    ///
    /// This is intentionally independent of FailureEffectReceiver. Normal
    /// spacecraft avionics load is not a failure leak.
    ///
    /// Consumption follows KSP universal time so pause and time warp behave
    /// like ordinary resource use. The lease itself expires on real time so a
    /// dead Mission Control connection cannot continue draining EC forever.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class KmcElectricalLoadReceiver :
        MonoBehaviour
    {
        private const float LeaseSeconds = 2.50f;
        private const double MaximumAcceptedRateEcPerSecond = 2.0;

        private readonly object _syncRoot =
            new object();

        private UdpClient _receiveClient;
        private Thread _receiveThread;
        private volatile bool _running;

        private ElectricalLoadLeasePacket _pending;

        private string _leasedVesselId =
            string.Empty;

        private double _leasedRateEcPerSecond;
        private float _lastLeaseRealtime;
        private double _lastUniversalTime;
        private bool _universalTimeInitialized;

        private string _lastLoggedVesselId =
            string.Empty;

        private double _lastLoggedRate =
            double.NaN;

        public void Start()
        {
            try
            {
                _receiveClient =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            ElectricalLoadLeasePacket.CommandPort));

                _running = true;

                _receiveThread =
                    new Thread(
                        ReceiveLoop)
                    {
                        IsBackground = true,
                        Name = "KMC Electrical Load"
                    };

                _receiveThread.Start();

                Debug.Log(
                    "[KMC] Electrical load lease receiver started on UDP " +
                    ElectricalLoadLeasePacket.CommandPort.ToString() +
                    ".");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Electrical load receiver start failed: " +
                    ex);
            }
        }

        public void Update()
        {
            AcceptPendingLease();
            ApplyLeasedLoad();
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

                    ElectricalLoadLeasePacket packet;

                    if (!ElectricalLoadLeasePacket.TryParse(
                            text,
                            out packet) ||
                        packet.EcPerSecond >
                            MaximumAcceptedRateEcPerSecond)
                    {
                        continue;
                    }

                    lock (_syncRoot)
                    {
                        /*
                         * Only the newest state matters. This avoids an
                         * accumulating command queue if KSP briefly stalls.
                         */
                        _pending =
                            packet;
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
                        "[KMC] Electrical load receive failed: " +
                        ex);
                }
            }
        }

        private void AcceptPendingLease()
        {
            ElectricalLoadLeasePacket packet =
                null;

            lock (_syncRoot)
            {
                if (_pending != null)
                {
                    packet =
                        _pending;

                    _pending =
                        null;
                }
            }

            if (packet == null)
            {
                return;
            }

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null ||
                !string.Equals(
                    vessel.id.ToString(),
                    packet.VesselId,
                    StringComparison.Ordinal))
            {
                /*
                 * Never consume from a different active vessel. The Mission
                 * Control sender will issue the proper vessel-qualified lease
                 * after the active-vessel snapshot catches up.
                 */
                return;
            }

            bool vesselChanged =
                !string.Equals(
                    _leasedVesselId,
                    packet.VesselId,
                    StringComparison.Ordinal);

            _leasedVesselId =
                packet.VesselId;

            _leasedRateEcPerSecond =
                Math.Max(
                    0.0,
                    packet.EcPerSecond);

            _lastLeaseRealtime =
                Time.realtimeSinceStartup;

            if (vesselChanged ||
                !_universalTimeInitialized)
            {
                ResetUniversalTimeBaseline();
            }

            if (!string.Equals(
                    _lastLoggedVesselId,
                    _leasedVesselId,
                    StringComparison.Ordinal) ||
                double.IsNaN(
                    _lastLoggedRate) ||
                Math.Abs(
                    _lastLoggedRate -
                    _leasedRateEcPerSecond) >
                    0.0005)
            {
                _lastLoggedVesselId =
                    _leasedVesselId;

                _lastLoggedRate =
                    _leasedRateEcPerSecond;

                Debug.Log(
                    "[KMC] ELECTRICAL LOAD LEASE" +
                    " | VesselId=" +
                    _leasedVesselId +
                    " | Rate=" +
                    _leasedRateEcPerSecond.ToString("0.000") +
                    " EC/s");
            }
        }

        private void ApplyLeasedLoad()
        {
            if (string.IsNullOrWhiteSpace(
                    _leasedVesselId))
            {
                return;
            }

            float nowRealtime =
                Time.realtimeSinceStartup;

            if (nowRealtime -
                    _lastLeaseRealtime >
                LeaseSeconds)
            {
                _leasedVesselId =
                    string.Empty;

                _leasedRateEcPerSecond =
                    0.0;

                _universalTimeInitialized =
                    false;

                Debug.Log(
                    "[KMC] ELECTRICAL LOAD LEASE EXPIRED" +
                    " | Action=KMC OWNED EC LOAD STOPPED");

                return;
            }

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null ||
                vessel.rootPart == null ||
                !string.Equals(
                    vessel.id.ToString(),
                    _leasedVesselId,
                    StringComparison.Ordinal))
            {
                _universalTimeInitialized =
                    false;

                return;
            }

            double nowUniversalTime =
                Planetarium.GetUniversalTime();

            if (!_universalTimeInitialized)
            {
                _lastUniversalTime =
                    nowUniversalTime;

                _universalTimeInitialized =
                    true;

                return;
            }

            double elapsedGameSeconds =
                nowUniversalTime -
                _lastUniversalTime;

            _lastUniversalTime =
                nowUniversalTime;

            if (elapsedGameSeconds <= 0.0 ||
                _leasedRateEcPerSecond <= 0.0)
            {
                return;
            }

            /*
             * A single-frame UT jump can occur around scene transitions. We
             * never "catch up" more than five in-game minutes in one request;
             * normal time warp remains fully represented by smaller successive
             * UT deltas.
             */
            elapsedGameSeconds =
                Math.Min(
                    elapsedGameSeconds,
                    300.0);

            double requested =
                _leasedRateEcPerSecond *
                elapsedGameSeconds;

            if (requested <= 0.0000001)
            {
                return;
            }

            try
            {
                MethodInfo requestResource =
                    FindRequestResourceMethod(
                        vessel.rootPart.GetType());

                if (requestResource == null)
                {
                    return;
                }

                requestResource.Invoke(
                    vessel.rootPart,
                    new object[]
                    {
                        "ElectricCharge",
                        requested
                    });
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Electrical load EC request failed: " +
                    ex.GetType().Name);
            }
        }

        private void ResetUniversalTimeBaseline()
        {
            _lastUniversalTime =
                Planetarium.GetUniversalTime();

            _universalTimeInitialized =
                true;
        }

        private static MethodInfo FindRequestResourceMethod(
            Type type)
        {
            while (type != null)
            {
                MethodInfo[] methods =
                    type.GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                for (int index = 0;
                     index < methods.Length;
                     index++)
                {
                    MethodInfo method =
                        methods[index];

                    if (!string.Equals(
                            method.Name,
                            "RequestResource",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters =
                        method.GetParameters();

                    if (parameters.Length == 2 &&
                        parameters[0].ParameterType ==
                            typeof(string) &&
                        parameters[1].ParameterType ==
                            typeof(double))
                    {
                        return method;
                    }
                }

                type =
                    type.BaseType;
            }

            return null;
        }

        public void OnDestroy()
        {
            _running = false;

            UdpClient client =
                _receiveClient;

            _receiveClient =
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

            Thread thread =
                _receiveThread;

            _receiveThread =
                null;

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
        }
    }
}
