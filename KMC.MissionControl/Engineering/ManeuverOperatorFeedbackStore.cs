using System;

namespace KMC.MissionControl.Engineering
{
    public sealed class ManeuverOperatorFeedbackSnapshot
    {
        public string Text { get; set; }
        public bool Success { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    /// <summary>
    /// Short-lived operator feedback for MNV header actions.
    /// This is presentation state only; it does not own maneuver planning
    /// or uplink truth.
    /// </summary>
    public static class ManeuverOperatorFeedbackStore
    {
        private static readonly object SyncRoot =
            new object();

        private static ManeuverOperatorFeedbackSnapshot _latest;

        public static void Publish(
            string text,
            bool success)
        {
            lock (SyncRoot)
            {
                _latest =
                    new ManeuverOperatorFeedbackSnapshot
                    {
                        Text =
                            text ??
                            string.Empty,

                        Success =
                            success,

                        TimestampUtc =
                            DateTime.UtcNow
                    };
            }
        }

        public static bool TryGetLatest(
            out ManeuverOperatorFeedbackSnapshot snapshot)
        {
            lock (SyncRoot)
            {
                if (_latest == null)
                {
                    snapshot = null;
                    return false;
                }

                /*
                 * Keep action feedback visible long enough for the operator
                 * to see it, but never let stale UI text become maneuver truth.
                 */
                if (DateTime.UtcNow - _latest.TimestampUtc >
                    TimeSpan.FromSeconds(8.0))
                {
                    snapshot = null;
                    return false;
                }

                snapshot =
                    new ManeuverOperatorFeedbackSnapshot
                    {
                        Text =
                            _latest.Text ??
                            string.Empty,

                        Success =
                            _latest.Success,

                        TimestampUtc =
                            _latest.TimestampUtc
                    };

                return true;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest =
                    null;
            }
        }
    }
}