using System;
using System.Collections.Generic;

namespace KMC.Engine.SpacecraftSystems
{
    /// <summary>
    /// KMC Build 14.18.7
    ///
    /// Engine-namespaced, vessel-scoped RCS authority truth.
    ///
    /// This is intentionally small. 14.18.7 supports:
    /// - real RCS hardware discovery published by the engineering cycle;
    /// - an explicit instructor inhibit used to validate downstream authority;
    /// - a reserved electrical-unpowered cause for Build 14.18.8.
    ///
    /// Axis-specific authority, thruster clusters and manifold/feed state are
    /// deliberately not modeled here yet.
    /// </summary>
    public sealed class RcsAuthoritySnapshot
    {
        public RcsAuthoritySnapshot()
        {
            VesselId = string.Empty;
            Known = false;
            RcsPartCount = 0;
            HardwareDetected = false;
            InstructorInhibited = false;
            ElectricalUnpowered = false;
            AuthorityAvailable = false;
            Detail = "UNKNOWN";
        }

        public string VesselId { get; internal set; }
        public bool Known { get; internal set; }
        public int RcsPartCount { get; internal set; }
        public bool HardwareDetected { get; internal set; }
        public bool InstructorInhibited { get; internal set; }

        /// <summary>
        /// Reserved 14.18.8 input. No 14.18.7 code asserts this automatically.
        /// </summary>
        public bool ElectricalUnpowered { get; internal set; }

        public bool AuthorityAvailable { get; internal set; }
        public string Detail { get; internal set; }

        internal RcsAuthoritySnapshot Clone()
        {
            return new RcsAuthoritySnapshot
            {
                VesselId = VesselId ?? string.Empty,
                Known = Known,
                RcsPartCount = RcsPartCount,
                HardwareDetected = HardwareDetected,
                InstructorInhibited = InstructorInhibited,
                ElectricalUnpowered = ElectricalUnpowered,
                AuthorityAvailable = AuthorityAvailable,
                Detail = Detail ?? string.Empty
            };
        }
    }

    public static class RcsAuthorityStore
    {
        private static readonly object SyncRoot = new object();

        private static readonly Dictionary<string, MutableRcsAuthorityState>
            ByVessel =
                new Dictionary<string, MutableRcsAuthorityState>(
                    StringComparer.Ordinal);

        public static void PublishHardware(
            string vesselId,
            int rcsPartCount)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
            {
                return;
            }

            lock (SyncRoot)
            {
                MutableRcsAuthorityState state =
                    GetOrCreate(vesselId);

                state.HardwareKnown = true;
                state.RcsPartCount =
                    Math.Max(0, rcsPartCount);
            }
        }

        public static void SetInstructorInhibit(
            string vesselId,
            bool inhibited)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
            {
                return;
            }

            lock (SyncRoot)
            {
                GetOrCreate(vesselId)
                    .InstructorInhibited = inhibited;
            }
        }

        /// <summary>
        /// Reserved integration point for Build 14.18.8.
        /// 14.18.7 never calls this automatically.
        /// </summary>
        public static void SetElectricalUnpowered(
            string vesselId,
            bool unpowered)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
            {
                return;
            }

            lock (SyncRoot)
            {
                GetOrCreate(vesselId)
                    .ElectricalUnpowered = unpowered;
            }
        }

        public static RcsAuthoritySnapshot GetSnapshot(
            string vesselId)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
            {
                return new RcsAuthoritySnapshot();
            }

            lock (SyncRoot)
            {
                MutableRcsAuthorityState state;

                if (!ByVessel.TryGetValue(
                        vesselId,
                        out state) ||
                    state == null)
                {
                    return new RcsAuthoritySnapshot
                    {
                        VesselId = vesselId
                    };
                }

                bool hardwareDetected =
                    state.HardwareKnown &&
                    state.RcsPartCount > 0;

                bool authorityAvailable =
                    hardwareDetected &&
                    !state.InstructorInhibited &&
                    !state.ElectricalUnpowered;

                string detail;

                if (!state.HardwareKnown)
                {
                    detail = "HARDWARE UNKNOWN";
                }
                else if (!hardwareDetected)
                {
                    detail = "NO RCS HARDWARE";
                }
                else if (state.InstructorInhibited)
                {
                    detail = "INSTRUCTOR INHIBIT";
                }
                else if (state.ElectricalUnpowered)
                {
                    detail = "CONTROL POWER UNAVAILABLE";
                }
                else
                {
                    detail = "AVAILABLE";
                }

                return new RcsAuthoritySnapshot
                {
                    VesselId = vesselId,
                    Known = state.HardwareKnown,
                    RcsPartCount = state.RcsPartCount,
                    HardwareDetected = hardwareDetected,
                    InstructorInhibited =
                        state.InstructorInhibited,
                    ElectricalUnpowered =
                        state.ElectricalUnpowered,
                    AuthorityAvailable =
                        authorityAvailable,
                    Detail = detail
                };
            }
        }

        public static void Reset(
            string vesselId)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
            {
                return;
            }

            lock (SyncRoot)
            {
                ByVessel.Remove(vesselId);
            }
        }

        public static void ClearAll()
        {
            lock (SyncRoot)
            {
                ByVessel.Clear();
            }
        }

        private static MutableRcsAuthorityState GetOrCreate(
            string vesselId)
        {
            MutableRcsAuthorityState state;

            if (!ByVessel.TryGetValue(
                    vesselId,
                    out state) ||
                state == null)
            {
                state =
                    new MutableRcsAuthorityState();

                ByVessel[vesselId] =
                    state;
            }

            return state;
        }

        private sealed class MutableRcsAuthorityState
        {
            public bool HardwareKnown;
            public int RcsPartCount;
            public bool InstructorInhibited;
            public bool ElectricalUnpowered;
        }
    }
}
