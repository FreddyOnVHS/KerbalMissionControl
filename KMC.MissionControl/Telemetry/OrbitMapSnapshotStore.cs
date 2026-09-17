using System;
using KMC.Shared;

namespace KMC.MissionControl.Telemetry
{
    public enum OrbitMapFreshness
    {
        Unavailable,
        Live,
        Stale
    }

    public static class OrbitMapSnapshotStore
    {
        private static readonly object SyncRoot = new object();
        private static readonly TimeSpan LiveWindow = TimeSpan.FromSeconds(1.0);
        private static readonly TimeSpan UnavailableWindow = TimeSpan.FromSeconds(4.0);
        private static OrbitMapPacket _latest;
        private static DateTime _receivedUtc;
        private static long _receivedCount;
        private static long _rejectedCount;

        public static long ReceivedCount { get { lock (SyncRoot) return _receivedCount; } }
        public static long RejectedCount { get { lock (SyncRoot) return _rejectedCount; } }

        public static void SetLatest(OrbitMapPacket packet, DateTime receivedUtc)
        {
            if (packet == null) return;
            lock (SyncRoot)
            {
                _latest = packet;
                _receivedUtc = receivedUtc;
                _receivedCount++;
            }
        }

        public static void RecordRejected()
        {
            lock (SyncRoot) _rejectedCount++;
        }

        public static bool TryGetLatest(out OrbitMapPacket packet, out DateTime receivedUtc)
        {
            lock (SyncRoot)
            {
                packet = _latest;
                receivedUtc = _receivedUtc;
                return packet != null;
            }
        }

        public static OrbitMapFreshness GetFreshness(DateTime nowUtc)
        {
            lock (SyncRoot)
            {
                if (_latest == null || _receivedUtc == DateTime.MinValue) return OrbitMapFreshness.Unavailable;
                TimeSpan age = nowUtc - _receivedUtc;
                if (age <= LiveWindow) return OrbitMapFreshness.Live;
                if (age <= UnavailableWindow) return OrbitMapFreshness.Stale;
                return OrbitMapFreshness.Unavailable;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest = null;
                _receivedUtc = DateTime.MinValue;
                _receivedCount = 0;
                _rejectedCount = 0;
            }
        }
    }
}
