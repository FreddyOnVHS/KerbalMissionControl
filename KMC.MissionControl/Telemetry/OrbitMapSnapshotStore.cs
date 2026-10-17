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

    /// <summary>
    /// Central KSP mission-time formatter. Uses the calendar convention
    /// reported by the live KSP OrbitMap telemetry stream.
    /// </summary>
    public static class KspTimeDisplay
    {
        private const double KerbinDaySeconds = 21600.0;
        private const double EarthDaySeconds = 86400.0;

        public static double GetDayLengthSeconds()
        {
            OrbitMapPacket packet;
            DateTime receivedUtc;

            if (OrbitMapSnapshotStore.TryGetLatest(
                    out packet,
                    out receivedUtc) &&
                packet != null &&
                IsSupportedDayLength(
                    packet.CalendarDayLengthSeconds))
            {
                return
                    packet.CalendarDayLengthSeconds;
            }

            return
                KerbinDaySeconds;
        }

        public static string FormatDuration(
            double seconds)
        {
            if (double.IsNaN(seconds) ||
                double.IsInfinity(seconds))
            {
                return "---";
            }

            bool negative =
                seconds < 0.0;

            double magnitude =
                Math.Abs(seconds);

            long totalSeconds =
                (long)Math.Round(
                    magnitude);

            long daySeconds =
                (long)Math.Round(
                    GetDayLengthSeconds());

            long days =
                totalSeconds /
                daySeconds;

            long remainder =
                totalSeconds %
                daySeconds;

            long hours =
                remainder /
                3600;

            long minutes =
                (remainder % 3600) /
                60;

            long secondsPart =
                remainder %
                60;

            string text =
                days.ToString() +
                "d " +
                hours.ToString("00") +
                "h " +
                minutes.ToString("00") +
                "m " +
                secondsPart.ToString("00") +
                "s";

            return
                negative
                    ? "-" + text
                    : text;
        }

        private static bool IsSupportedDayLength(
            double seconds)
        {
            return
                Math.Abs(
                    seconds -
                    KerbinDaySeconds) <
                    0.5 ||
                Math.Abs(
                    seconds -
                    EarthDaySeconds) <
                    0.5;
        }
    }
}
