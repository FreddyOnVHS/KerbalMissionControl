using System;
using System.Collections.Generic;
using System.Drawing;

namespace KMC.MissionControl.Cards
{
    /// <summary>
    /// Central diagnostic store for card draw counts, bounds, dirty reasons,
    /// and render duration. This is intentionally independent of the visual
    /// performance overlay so future developer tools can consume it directly.
    /// </summary>
    public static class CardDiagnosticsRegistry
    {
        private static readonly object SyncRoot =
            new object();

        private static readonly Dictionary<string, CardDiagnosticSnapshot>
            Snapshots =
                new Dictionary<string, CardDiagnosticSnapshot>(
                    StringComparer.Ordinal);

        public static void RecordDraw(
            string id,
            Rectangle bounds,
            CardDirtyState dirtyStateBeforeDraw,
            long drawCount,
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
                        Id =
                            id,

                        Bounds =
                            bounds,

                        DirtyStateBeforeDraw =
                            dirtyStateBeforeDraw,

                        DrawCount =
                            drawCount,

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

                foreach (CardDiagnosticSnapshot snapshot
                    in Snapshots.Values)
                {
                    result.Add(
                        new CardDiagnosticSnapshot
                        {
                            Id =
                                snapshot.Id,

                            Bounds =
                                snapshot.Bounds,

                            DirtyStateBeforeDraw =
                                snapshot.DirtyStateBeforeDraw,

                            DrawCount =
                                snapshot.DrawCount,

                            LastDrawMilliseconds =
                                snapshot.LastDrawMilliseconds,

                            AverageDrawMilliseconds =
                                snapshot.AverageDrawMilliseconds
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
