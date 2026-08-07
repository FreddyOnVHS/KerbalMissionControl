using KMC.Shared.Topology;

namespace KMC.MissionControl.Transport
{
    /// <summary>
    /// Thread-safe latest-value cache between UDP transport and engineering.
    /// It contains data only; it owns no sockets and performs no analysis.
    /// </summary>
    public sealed class TelemetryCache
    {
        private readonly object _syncRoot =
            new object();

        private VesselTopology _topology;
        private SystemsTelemetrySample _systems;

        public void PublishTopology(
            VesselTopology topology)
        {
            lock (_syncRoot)
            {
                _topology =
                    topology;
            }
        }

        public void PublishSystems(
            SystemsTelemetrySample systems)
        {
            lock (_syncRoot)
            {
                _systems =
                    systems;
            }
        }

        public VesselTopology GetTopology()
        {
            lock (_syncRoot)
            {
                return
                    _topology;
            }
        }

        public SystemsTelemetrySample GetSystems()
        {
            lock (_syncRoot)
            {
                return
                    _systems;
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _topology =
                    null;

                _systems =
                    null;
            }
        }
    }
}
