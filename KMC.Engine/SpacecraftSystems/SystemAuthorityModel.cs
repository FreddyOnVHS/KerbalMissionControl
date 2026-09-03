using System;
using System.Collections.Generic;
using KMC.Shared;

namespace KMC.Engine.SpacecraftSystems
{
    /// <summary>
    /// Build 14.19.1 vessel-scoped KMC command-authority truth.
    ///
    /// This is the same explicit instructor/integration-test pattern used by
    /// the proven RCS authority foundation. The KSP plugin never decides that
    /// one of these systems has failed; it only executes the state KMC sends.
    /// </summary>
    public static class SystemAuthorityStore
    {
        private static readonly object SyncRoot =
            new object();

        private static readonly Dictionary<string, HashSet<SystemAuthorityKind>>
            InhibitedByVessel =
                new Dictionary<string, HashSet<SystemAuthorityKind>>(
                    StringComparer.Ordinal);

        public static void SetInstructorInhibit(
            string vesselId,
            SystemAuthorityKind authority,
            bool inhibited)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return;

            lock (SyncRoot)
            {
                HashSet<SystemAuthorityKind> set;

                if (!InhibitedByVessel.TryGetValue(
                        vesselId,
                        out set) ||
                    set == null)
                {
                    set =
                        new HashSet<SystemAuthorityKind>();

                    InhibitedByVessel[vesselId] =
                        set;
                }

                if (inhibited)
                    set.Add(authority);
                else
                    set.Remove(authority);

                if (set.Count == 0)
                    InhibitedByVessel.Remove(vesselId);
            }
        }

        public static bool IsInhibited(
            string vesselId,
            SystemAuthorityKind authority)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return false;

            lock (SyncRoot)
            {
                HashSet<SystemAuthorityKind> set;

                return
                    InhibitedByVessel.TryGetValue(
                        vesselId,
                        out set) &&
                    set != null &&
                    set.Contains(authority);
            }
        }

        public static void RestoreAll(
            string vesselId)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return;

            lock (SyncRoot)
                InhibitedByVessel.Remove(vesselId);
        }

        public static void ClearAll()
        {
            lock (SyncRoot)
                InhibitedByVessel.Clear();
        }
    }
}
