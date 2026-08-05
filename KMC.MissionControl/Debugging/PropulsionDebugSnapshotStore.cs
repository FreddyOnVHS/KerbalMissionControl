using System;
using KMC.Shared;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Debugging
{
    public static class PropulsionDebugSnapshotStore
    {
        private static readonly object SyncRoot =
            new object();

        private static TelemetryPacket _telemetry;
        private static VesselTopology _topology;

        public static void PublishTelemetry(
            TelemetryPacket packet)
        {
            lock (SyncRoot)
            {
                _telemetry =
                    packet;
            }
        }

        public static void PublishTopology(
            VesselTopology topology)
        {
            lock (SyncRoot)
            {
                _topology =
                    topology;
            }
        }

        public static TelemetryPacket GetTelemetry()
        {
            lock (SyncRoot)
            {
                return _telemetry;
            }
        }

        public static VesselTopology GetTopology()
        {
            lock (SyncRoot)
            {
                return _topology;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _telemetry =
                    null;

                _topology =
                    null;
            }
        }
    }
}
