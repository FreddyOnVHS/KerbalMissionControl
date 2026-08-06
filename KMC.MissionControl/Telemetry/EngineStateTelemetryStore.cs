using System.Collections.Generic;

namespace KMC.MissionControl.Telemetry
{
    public static class EngineStateTelemetryStore
    {
        private static readonly object Sync =
            new object();

        private static Dictionary<
            uint,
            EngineStateTelemetry> _engines =
                new Dictionary<
                    uint,
                    EngineStateTelemetry>();

        private static long _revision;

        public static void Publish(
            Dictionary<
                uint,
                EngineStateTelemetry> engines)
        {
            lock (Sync)
            {
                _engines =
                    engines ??
                    new Dictionary<
                        uint,
                        EngineStateTelemetry>();

                _revision++;
            }
        }

        public static EngineStateTelemetry GetEngine(
            uint id)
        {
            lock (Sync)
            {
                EngineStateTelemetry value;

                return
                    _engines.TryGetValue(
                        id,
                        out value)
                        ? value
                        : null;
            }
        }

        /// <summary>
        /// Returns a thread-safe point-in-time copy of all current engine
        /// telemetry. The returned dictionary can be enumerated without
        /// holding the store lock.
        /// </summary>
        public static Dictionary<
            uint,
            EngineStateTelemetry> GetSnapshot()
        {
            lock (Sync)
            {
                Dictionary<
                    uint,
                    EngineStateTelemetry> snapshot =
                        new Dictionary<
                            uint,
                            EngineStateTelemetry>();

                foreach (
                    KeyValuePair<
                        uint,
                        EngineStateTelemetry> pair
                    in _engines)
                {
                    EngineStateTelemetry source =
                        pair.Value;

                    if (source == null)
                    {
                        continue;
                    }

                    snapshot[pair.Key] =
                        new EngineStateTelemetry
                        {
                            PartId =
                                source.PartId,

                            OperatingState =
                                source.OperatingState,

                            IsSolidBooster =
                                source.IsSolidBooster,

                            CurrentThrust =
                                source.CurrentThrust,

                            MaximumThrust =
                                source.MaximumThrust
                        };
                }

                return snapshot;
            }
        }

        public static long GetRevision()
        {
            lock (Sync)
            {
                return _revision;
            }
        }

        public static void Clear()
        {
            Publish(
                new Dictionary<
                    uint,
                    EngineStateTelemetry>());
        }
    }
}
