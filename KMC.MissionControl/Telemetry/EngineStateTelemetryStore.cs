using System.Collections.Generic;
namespace KMC.MissionControl.Telemetry
{
    public static class EngineStateTelemetryStore
    {
        private static readonly object Sync = new object();
        private static Dictionary<uint, EngineStateTelemetry> _engines = new Dictionary<uint, EngineStateTelemetry>();
        private static long _revision;
        public static void Publish(Dictionary<uint, EngineStateTelemetry> engines)
        { lock (Sync) { _engines = engines ?? new Dictionary<uint, EngineStateTelemetry>(); _revision++; } }
        public static EngineStateTelemetry GetEngine(uint id)
        { lock (Sync) { EngineStateTelemetry v; return _engines.TryGetValue(id, out v) ? v : null; } }
        public static long GetRevision() { lock (Sync) return _revision; }
        public static void Clear() { Publish(new Dictionary<uint, EngineStateTelemetry>()); }
    }
}
