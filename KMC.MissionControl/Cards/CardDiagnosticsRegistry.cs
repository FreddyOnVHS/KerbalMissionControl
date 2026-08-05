using System;
using System.Collections.Generic;
using System.Drawing;

namespace KMC.MissionControl.Cards
{
    public static class CardDiagnosticsRegistry
    {
        private static readonly object SyncRoot =
            new object();

        private static readonly Dictionary<string, CardDiagnosticSnapshot>
            Snapshots =
                new Dictionary<string, CardDiagnosticSnapshot>(
                    StringComparer.Ordinal);

        public static void Record(
            string id,
            Rectangle bounds,
            CardDirtyState dirtyStateBeforeDraw,
            long drawCount,
            long presentationCount,
            long cacheHitCount,
            long bitmapAllocationCount,
            long cachedBitmapBytes,
            double lastDrawMilliseconds,
            double averageDrawMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            lock (SyncRoot)
            {
                Snapshots[id] =
                    new CardDiagnosticSnapshot
                    {
                        Id = id,
                        Bounds = bounds,
                        DirtyStateBeforeDraw =
                            dirtyStateBeforeDraw,
                        DrawCount = drawCount,
                        PresentationCount =
                            presentationCount,
                        CacheHitCount =
                            cacheHitCount,
                        BitmapAllocationCount =
                            bitmapAllocationCount,
                        CachedBitmapBytes =
                            cachedBitmapBytes,
                        LastDrawMilliseconds =
                            lastDrawMilliseconds,
                        AverageDrawMilliseconds =
                            averageDrawMilliseconds
                    };
            }
        }

        public static IList<CardDiagnosticSnapshot> GetSnapshots()
        {
            lock (SyncRoot)
            {
                List<CardDiagnosticSnapshot> result =
                    new List<CardDiagnosticSnapshot>(
                        Snapshots.Count);

                foreach (CardDiagnosticSnapshot source
                    in Snapshots.Values)
                {
                    result.Add(
                        new CardDiagnosticSnapshot
                        {
                            Id = source.Id,
                            Bounds = source.Bounds,
                            DirtyStateBeforeDraw =
                                source.DirtyStateBeforeDraw,
                            DrawCount =
                                source.DrawCount,
                            PresentationCount =
                                source.PresentationCount,
                            CacheHitCount =
                                source.CacheHitCount,
                            BitmapAllocationCount =
                                source.BitmapAllocationCount,
                            CachedBitmapBytes =
                                source.CachedBitmapBytes,
                            LastDrawMilliseconds =
                                source.LastDrawMilliseconds,
                            AverageDrawMilliseconds =
                                source.AverageDrawMilliseconds
                        });
                }

                result.Sort(
                    delegate(
                        CardDiagnosticSnapshot left,
                        CardDiagnosticSnapshot right)
                    {
                        return string.Compare(
                            left.Id,
                            right.Id,
                            StringComparison.Ordinal);
                    });

                return result;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                Snapshots.Clear();
            }
        }
    }
}
