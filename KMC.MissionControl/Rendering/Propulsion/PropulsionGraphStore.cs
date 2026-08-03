using System;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Thread-safe handoff between the UDP receiver and the WinForms renderer.
    /// Graph snapshots are immutable after publication.
    /// </summary>
    public static class PropulsionGraphStore
    {
        private static readonly object SyncRoot =
            new object();

        private static PropulsionRenderGraph _current;

        public static void Publish(
            PropulsionRenderGraph graph)
        {
            lock (SyncRoot)
            {
                _current = graph;
            }
        }

        public static PropulsionRenderGraph GetCurrent()
        {
            lock (SyncRoot)
            {
                return _current;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _current = null;
            }
        }
    }
}
