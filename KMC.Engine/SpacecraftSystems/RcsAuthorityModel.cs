using System;
using System.Collections.Generic;

namespace KMC.Engine.SpacecraftSystems
{
    /// <summary>
    /// KMC Build 14.19.1 source-tree restoration of the proven RCS authority
    /// model used by Builds 14.18.7 / 14.18.8.
    ///
    /// RCS authority is vehicle-scoped. Hardware truth and KMC electrical
    /// control-power truth are independent inputs. Missing electrical evidence
    /// fails open.
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
            ElectricalPowerKnown = false;
            ElectricalPowered = true;
            ElectricalBusId = "BUS_ESS";
            ElectricalVoltage = 0.0;
            AuthorityAvailable = false;
            Detail = "UNKNOWN";
        }

        public string VesselId { get; internal set; }
        public bool Known { get; internal set; }
        public int RcsPartCount { get; internal set; }
        public bool HardwareDetected { get; internal set; }
        public bool InstructorInhibited { get; internal set; }
        public bool ElectricalPowerKnown { get; internal set; }
        public bool ElectricalPowered { get; internal set; }
        public string ElectricalBusId { get; internal set; }
        public double ElectricalVoltage { get; internal set; }
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
                ElectricalPowerKnown = ElectricalPowerKnown,
                ElectricalPowered = ElectricalPowered,
                ElectricalBusId = ElectricalBusId ?? string.Empty,
                ElectricalVoltage = ElectricalVoltage,
                AuthorityAvailable = AuthorityAvailable,
                Detail = Detail ?? string.Empty
            };
        }
    }

    public static class RcsAuthorityStore
    {
        private static readonly object SyncRoot =
            new object();

        private static readonly Dictionary<string, MutableRcsAuthorityState>
            ByVessel =
                new Dictionary<string, MutableRcsAuthorityState>(
                    StringComparer.Ordinal);

        public static void PublishHardware(
            string vesselId,
            int rcsPartCount)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return;

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
                return;

            lock (SyncRoot)
            {
                GetOrCreate(vesselId)
                    .InstructorInhibited = inhibited;
            }
        }

        public static void PublishElectricalPower(
            string vesselId,
            bool known,
            bool powered,
            string busId,
            double voltage)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return;

            lock (SyncRoot)
            {
                MutableRcsAuthorityState state =
                    GetOrCreate(vesselId);

                state.ElectricalPowerKnown = known;
                state.ElectricalPowered =
                    !known || powered;
                state.ElectricalBusId =
                    busId ?? string.Empty;
                state.ElectricalVoltage =
                    double.IsNaN(voltage) ||
                    double.IsInfinity(voltage)
                        ? 0.0
                        : Math.Max(0.0, voltage);
            }
        }

        public static void SetElectricalUnpowered(
            string vesselId,
            bool unpowered)
        {
            PublishElectricalPower(
                vesselId,
                true,
                !unpowered,
                "BUS_ESS",
                0.0);
        }

        public static RcsAuthoritySnapshot GetSnapshot(
            string vesselId)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return new RcsAuthoritySnapshot();

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

                bool electricalAvailable =
                    !state.ElectricalPowerKnown ||
                    state.ElectricalPowered;

                bool authorityAvailable =
                    hardwareDetected &&
                    !state.InstructorInhibited &&
                    electricalAvailable;

                string detail;

                if (!state.HardwareKnown)
                    detail = "HARDWARE UNKNOWN";
                else if (!hardwareDetected)
                    detail = "NO RCS HARDWARE";
                else if (state.InstructorInhibited)
                    detail = "INSTRUCTOR INHIBIT";
                else if (state.ElectricalPowerKnown &&
                         !state.ElectricalPowered)
                    detail = "CONTROL POWER UNAVAILABLE";
                else
                    detail = "AVAILABLE";

                return new RcsAuthoritySnapshot
                {
                    VesselId = vesselId,
                    Known = state.HardwareKnown,
                    RcsPartCount = state.RcsPartCount,
                    HardwareDetected = hardwareDetected,
                    InstructorInhibited =
                        state.InstructorInhibited,
                    ElectricalPowerKnown =
                        state.ElectricalPowerKnown,
                    ElectricalPowered =
                        state.ElectricalPowered,
                    ElectricalBusId =
                        state.ElectricalBusId ?? string.Empty,
                    ElectricalVoltage =
                        state.ElectricalVoltage,
                    AuthorityAvailable =
                        authorityAvailable,
                    Detail = detail
                };
            }
        }

        public static void Reset(string vesselId)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return;

            lock (SyncRoot)
                ByVessel.Remove(vesselId);
        }

        public static void ClearAll()
        {
            lock (SyncRoot)
                ByVessel.Clear();
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
                state = new MutableRcsAuthorityState();
                state.ElectricalPowered = true;
                state.ElectricalBusId = "BUS_ESS";
                ByVessel[vesselId] = state;
            }

            return state;
        }

        private sealed class MutableRcsAuthorityState
        {
            public bool HardwareKnown;
            public int RcsPartCount;
            public bool InstructorInhibited;
            public bool ElectricalPowerKnown;
            public bool ElectricalPowered;
            public string ElectricalBusId;
            public double ElectricalVoltage;
        }
    }
}
