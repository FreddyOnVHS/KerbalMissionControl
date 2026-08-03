using System;
using UnityEngine;

namespace KMC.Plugin.Topology
{
    /// <summary>
    /// Flight-scene monitor that builds and logs a vessel topology snapshot
    /// whenever the active vessel structure changes.
    ///
    /// This is intentionally separate from TelemetrySender so Phase 1 can be
    /// validated without changing the existing telemetry protocol.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class VesselTopologyMonitor :
        MonoBehaviour
    {
        private const float ScanIntervalSeconds =
            0.50f;

        private readonly VesselTopologyService _service =
            new VesselTopologyService();

        private float _nextScanTime;

        public void Start()
        {
            _service.Reset();

            _nextScanTime =
                0.0f;

            Debug.Log(
                "[KMC] Vessel topology monitor started.");
        }

        public void Update()
        {
            if (Time.realtimeSinceStartup <
                _nextScanTime)
            {
                return;
            }

            _nextScanTime =
                Time.realtimeSinceStartup +
                ScanIntervalSeconds;

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null)
            {
                return;
            }

            try
            {
                if (!_service.Update(
                        vessel))
                {
                    return;
                }

                string report =
                    VesselTopologyDiagnostics
                        .CreateReport(
                            _service.Current);

                Debug.Log(
                    report);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Vessel topology update failed: " +
                    ex);
            }
        }

        public void OnDestroy()
        {
            _service.Reset();

            Debug.Log(
                "[KMC] Vessel topology monitor stopped.");
        }
    }
}
