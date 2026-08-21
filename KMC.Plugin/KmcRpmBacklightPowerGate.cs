using System;
using System.Reflection;
using KMC.Shared;

namespace KMC.Plugin
{
    /// <summary>
    /// KMC Build 14.18.10 Rev B
    ///
    /// Profile-specific ASET/RPM cockpit backlight electrical gate.
    ///
    /// The ASET BackLight persistent is normally both the user's command and
    /// the effective input consumed by CUSTOM_ALCOR_BACKLIGHT_ON.
    ///
    /// KMC deliberately does NOT put RPM needsElectricCharge on the panel-light
    /// switch because RPM's native forced-shutdown path clears the persistent
    /// switch state. Instead this module:
    ///
    /// 1. observes the existing ASET BackLight persistent while ESS is powered;
    /// 2. remembers that command in KMC_BackLightSavedCommand;
    /// 3. forces only the effective BackLight persistent false while ESS is off;
    /// 4. restores the remembered command when ESS returns;
    /// 5. restores immediately when KMC status disappears (fail open).
    ///
    /// The module is attached only to the KMC Mk1 panel-light prop variant, so
    /// unsupported / unmapped ASET cockpits retain native behavior.
    ///
    /// No Unity Light, renderer, material, texture, mesh, or animation forcing
    /// is performed here. ASET remains responsible for visual illumination.
    /// </summary>
    public sealed class KmcRpmBacklightPowerGate :
        InternalModule
    {
        [KSPField]
        public string powerDomain =
            "ESS";

        [KSPField]
        public string effectivePersistenceName =
            "BackLight";

        [KSPField]
        public string savedCommandPersistenceName =
            "KMC_BackLightSavedCommand";

        [KSPField]
        public string outageMarkerPersistenceName =
            "KMC_BackLightPowerOutage";

        private object _rpmComputer;
        private MethodInfo _getPersistentBool;
        private MethodInfo _setPersistent;

        public void Start()
        {
            FindRpmComputer();
        }

        public override void OnUpdate()
        {
            if (!EnsureRpmComputer())
            {
                /*
                 * Reflection unavailable -> do nothing.
                 * Native ASET BackLight behavior therefore remains untouched.
                 */
                return;
            }

            bool powered =
                DeterminePowered();

            bool outageActive =
                GetPersistent(
                    outageMarkerPersistenceName,
                    false);

            if (!powered)
            {
                if (!outageActive)
                {
                    bool commandedOn =
                        GetPersistent(
                            effectivePersistenceName,
                            false);

                    SetPersistent(
                        savedCommandPersistenceName,
                        commandedOn);

                    SetPersistent(
                        outageMarkerPersistenceName,
                        true);
                }

                /*
                 * Gate only the effective ASET BackLight output. The saved KMC
                 * command is intentionally left untouched through the outage.
                 */
                if (GetPersistent(
                        effectivePersistenceName,
                        false))
                {
                    SetPersistent(
                        effectivePersistenceName,
                        false);
                }

                return;
            }

            if (outageActive)
            {
                bool restoreCommand =
                    GetPersistent(
                        savedCommandPersistenceName,
                        false);

                SetPersistent(
                    effectivePersistenceName,
                    restoreCommand);

                SetPersistent(
                    outageMarkerPersistenceName,
                    false);

                return;
            }

            /*
             * While powered, native ASET is authoritative. Track the user's
             * current BackLight command so the next outage can preserve it.
             */
            bool currentCommand =
                GetPersistent(
                    effectivePersistenceName,
                    false);

            SetPersistent(
                savedCommandPersistenceName,
                currentCommand);
        }

        public void OnDestroy()
        {
            /*
             * Fail open if this KMC module is destroyed while an outage gate
             * is active. Restore the remembered ASET command if reflection is
             * still available.
             */
            try
            {
                if (!EnsureRpmComputer())
                {
                    return;
                }

                bool outageActive =
                    GetPersistent(
                        outageMarkerPersistenceName,
                        false);

                if (!outageActive)
                {
                    return;
                }

                bool restoreCommand =
                    GetPersistent(
                        savedCommandPersistenceName,
                        false);

                SetPersistent(
                    effectivePersistenceName,
                    restoreCommand);

                SetPersistent(
                    outageMarkerPersistenceName,
                    false);
            }
            catch
            {
            }
        }

        private bool DeterminePowered()
        {
            if (part == null ||
                part.vessel == null)
            {
                return true;
            }

            KmcMfdStatusPacket status;

            if (!KmcMfdStatusReceiver.TryGetStatus(
                    part.vessel.id.ToString(),
                    out status))
            {
                /*
                 * KMC link/status loss fails open.
                 */
                return true;
            }

            string domain =
                (powerDomain ?? string.Empty)
                    .Trim()
                    .ToUpperInvariant();

            switch (domain)
            {
                case "MAIN_A":
                    return
                        IsBusPowered(
                            status.MainAVoltage,
                            status.MainAState);

                case "MAIN_B":
                    return
                        IsBusPowered(
                            status.MainBVoltage,
                            status.MainBState);

                case "ESS":
                case "ESSENTIAL":
                    return
                        IsBusPowered(
                            status.EssentialVoltage,
                            status.EssentialState);

                default:
                    return true;
            }
        }

        private static bool IsBusPowered(
            double voltage,
            string state)
        {
            if (double.IsNaN(voltage) ||
                double.IsInfinity(voltage) ||
                voltage < 18.0)
            {
                return false;
            }

            string normalized =
                string.IsNullOrWhiteSpace(state)
                    ? string.Empty
                    : state.Trim()
                        .ToUpperInvariant();

            return
                !string.Equals(
                    normalized,
                    "FAILED",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    normalized,
                    "UNPOWERED",
                    StringComparison.Ordinal);
        }

        private bool EnsureRpmComputer()
        {
            if (_rpmComputer != null &&
                _getPersistentBool != null &&
                _setPersistent != null)
            {
                return true;
            }

            FindRpmComputer();

            return
                _rpmComputer != null &&
                _getPersistentBool != null &&
                _setPersistent != null;
        }

        private void FindRpmComputer()
        {
            _rpmComputer = null;
            _getPersistentBool = null;
            _setPersistent = null;

            if (part == null ||
                part.Modules == null)
            {
                return;
            }

            for (int i = 0;
                 i < part.Modules.Count;
                 i++)
            {
                PartModule module =
                    part.Modules[i];

                if (module == null)
                {
                    continue;
                }

                Type type =
                    module.GetType();

                if (!string.Equals(
                        type.FullName,
                        "JSI.RasterPropMonitorComputer",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                const BindingFlags Flags =
                    BindingFlags.Instance |
                    BindingFlags.NonPublic;

                MethodInfo getBool =
                    type.GetMethod(
                        "GetPersistentVariable",
                        Flags,
                        null,
                        new[]
                        {
                            typeof(string),
                            typeof(bool),
                            typeof(bool)
                        },
                        null);

                MethodInfo set =
                    type.GetMethod(
                        "SetPersistentVariable",
                        Flags,
                        null,
                        new[]
                        {
                            typeof(string),
                            typeof(object),
                            typeof(bool)
                        },
                        null);

                if (getBool == null ||
                    set == null)
                {
                    continue;
                }

                _rpmComputer =
                    module;

                _getPersistentBool =
                    getBool;

                _setPersistent =
                    set;

                return;
            }
        }

        private bool GetPersistent(
            string name,
            bool defaultValue)
        {
            if (_rpmComputer == null ||
                _getPersistentBool == null ||
                string.IsNullOrWhiteSpace(name))
            {
                return defaultValue;
            }

            try
            {
                object result =
                    _getPersistentBool.Invoke(
                        _rpmComputer,
                        new object[]
                        {
                            name,
                            defaultValue,
                            false
                        });

                return
                    result is bool &&
                    (bool)result;
            }
            catch
            {
                return defaultValue;
            }
        }

        private void SetPersistent(
            string name,
            bool value)
        {
            if (_rpmComputer == null ||
                _setPersistent == null ||
                string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            try
            {
                _setPersistent.Invoke(
                    _rpmComputer,
                    new object[]
                    {
                        name,
                        value,
                        false
                    });
            }
            catch
            {
            }
        }
    }
}
