using System;
using System.Globalization;
using System.Reflection;
using KMC.Shared;

namespace KMC.Plugin
{
    /// <summary>
    /// Build 14.16.7 RasterPropMonitor external-variable handler.
    ///
    /// RPM discovers this ordinary KSP PartModule through an
    /// RPMCVARIABLEHANDLER config node. KMC therefore has no compile-time
    /// dependency on RasterPropMonitor.dll.
    ///
    /// Physical MFD power gating is intentionally delegated to RPM's native
    /// needsElectricCharge/resourceName mechanism.  KMC supplies only the
    /// logical MAIN A / MAIN B / ESS power variables.
    /// </summary>
    public sealed class KmcRpmVariableHandler :
        PartModule
    {
        private const double MinimumPoweredBusVoltage =
            18.0;

        public object ProcessVariable(
            string variableName)
        {
            KmcMfdStatusPacket status;

            bool available =
                TryGetStatus(
                    out status);

            switch (variableName)
            {
                case "KMC_AVAILABLE":
                    return
                        available
                            ? 1.0
                            : 0.0;

                case "KMC_LINK":
                    return
                        available
                            ? "ONLINE"
                            : "NO KMC LINK";

                /*
                 * Power-domain variables fail OPEN when KMC status is absent.
                 * That preserves normal IVA usability if Mission Control is
                 * closed or the status lease expires.
                 */
                case "KMC_MAIN_A_POWERED":
                    return
                        !available ||
                        IsBusPowered(
                            status.MainAVoltage,
                            status.MainAState)
                            ? 1.0
                            : 0.0;

                case "KMC_MAIN_B_POWERED":
                    return
                        !available ||
                        IsBusPowered(
                            status.MainBVoltage,
                            status.MainBState)
                            ? 1.0
                            : 0.0;

                case "KMC_ESS_POWERED":
                    return
                        !available ||
                        IsBusPowered(
                            status.EssentialVoltage,
                            status.EssentialState)
                            ? 1.0
                            : 0.0;

                case "KMC_MAIN_A_V":
                    return
                        available
                            ? FormatVoltage(
                                status.MainAVoltage)
                            : "--";

                case "KMC_MAIN_A_STATE":
                    return
                        available
                            ? SafeText(
                                status.MainAState)
                            : "--";

                case "KMC_MAIN_A_SOURCE":
                    return
                        available
                            ? SafeText(
                                status.MainASource)
                            : "--";

                case "KMC_MAIN_B_V":
                    return
                        available
                            ? FormatVoltage(
                                status.MainBVoltage)
                            : "--";

                case "KMC_MAIN_B_STATE":
                    return
                        available
                            ? SafeText(
                                status.MainBState)
                            : "--";

                case "KMC_MAIN_B_SOURCE":
                    return
                        available
                            ? SafeText(
                                status.MainBSource)
                            : "--";

                case "KMC_ESS_V":
                    return
                        available
                            ? FormatVoltage(
                                status.EssentialVoltage)
                            : "--";

                case "KMC_ESS_STATE":
                    return
                        available
                            ? SafeText(
                                status.EssentialState)
                            : "--";

                case "KMC_ESS_SOURCE":
                    return
                        available
                            ? SafeText(
                                status.EssentialSource)
                            : "--";

                case "KMC_BAT_A_STATE":
                    return
                        available
                            ? SafeText(
                                status.BatteryAState)
                            : "--";

                case "KMC_BAT_B_STATE":
                    return
                        available
                            ? SafeText(
                                status.BatteryBState)
                            : "--";

                default:
                    return null;
            }
        }

        private bool TryGetStatus(
            out KmcMfdStatusPacket status)
        {
            status = null;

            if (part == null ||
                part.vessel == null)
            {
                return false;
            }

            return
                KmcMfdStatusReceiver.TryGetStatus(
                    part.vessel.id.ToString(),
                    out status);
        }

        private static bool IsBusPowered(
            double voltage,
            string state)
        {
            if (double.IsNaN(voltage) ||
                double.IsInfinity(voltage) ||
                voltage <
                    MinimumPoweredBusVoltage)
            {
                return false;
            }

            string normalized =
                SafeText(
                    state);

            return
                !string.Equals(
                    normalized,
                    "UNPOWERED",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    normalized,
                    "FAILED",
                    StringComparison.Ordinal);
        }

        private static string FormatVoltage(
            double voltage)
        {
            return
                voltage.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture) +
                " V";
        }

        private static string SafeText(
            string value)
        {
            return
                string.IsNullOrWhiteSpace(
                    value)
                    ? "--"
                    : value.Trim()
                        .ToUpperInvariant();
        }
    }

    /// <summary>
    /// Build 14.16.8 RPM wake helper.
    ///
    /// RPM's native power-depletion path correctly clears the display, but a
    /// non-mutable page may remain on that cleared RenderTexture after power
    /// returns until a page/button action invalidates its render state.
    ///
    /// This InternalModule is attached to the same physical KMC MFD prop.  It
    /// detects a KMC bus OFF -> ON transition and asks RPM to invalidate its
    /// one-shot render state using the public OnApplicationPause(false) method.
    /// RPM then redraws itself naturally on its next LateUpdate.
    ///
    /// No RasterPropMonitor assembly reference is required.
    /// </summary>
    public sealed class KmcRpmPowerWake :
        InternalModule
    {
        [KSPField]
        public string powerDomain =
            "MAIN_A";

        private bool _hasState;
        private bool _lastPowered = true;

        private object _rpmModule;
        private FieldInfo _textRendererField;
        private FieldInfo _firstRenderCompleteField;
        private FieldInfo _textRefreshRequiredField;
        private FieldInfo _refreshDrawCountdownField;
        private FieldInfo _refreshTextCountdownField;

        private object _textRenderer;
        private FieldInfo _cachedTextField;
        private FieldInfo _cachedOverlayTextField;

        public void Start()
        {
            FindRpmInternals();
        }

        public override void OnUpdate()
        {
            bool powered =
                DeterminePowered();

            if (!_hasState)
            {
                _hasState = true;
                _lastPowered = powered;
                return;
            }

            if (!_lastPowered &&
                powered)
            {
                InvalidateRpmRenderCache();
            }

            _lastPowered = powered;
        }

        private bool DeterminePowered()
        {
            if (part == null ||
                part.vessel == null)
            {
                /*
                 * Preserve KMC's fail-open behavior when the vessel or status
                 * lease is unavailable.
                 */
                return true;
            }

            KmcMfdStatusPacket status;

            if (!KmcMfdStatusReceiver.TryGetStatus(
                    part.vessel.id.ToString(),
                    out status))
            {
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

        private void FindRpmInternals()
        {
            _rpmModule = null;
            _textRendererField = null;
            _firstRenderCompleteField = null;
            _textRefreshRequiredField = null;
            _refreshDrawCountdownField = null;
            _refreshTextCountdownField = null;
            _textRenderer = null;
            _cachedTextField = null;
            _cachedOverlayTextField = null;

            if (internalProp == null ||
                internalProp.gameObject == null)
            {
                return;
            }

            InternalModule[] modules =
                internalProp.gameObject
                    .GetComponents<InternalModule>();

            for (int i = 0;
                i < modules.Length;
                ++i)
            {
                InternalModule module =
                    modules[i];

                if (module == null)
                {
                    continue;
                }

                Type rpmType =
                    module.GetType();

                if (!string.Equals(
                        rpmType.FullName,
                        "JSI.RasterPropMonitor",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                const BindingFlags Flags =
                    BindingFlags.Instance |
                    BindingFlags.NonPublic;

                FieldInfo textRendererField =
                    rpmType.GetField(
                        "textRenderer",
                        Flags);

                FieldInfo firstRenderCompleteField =
                    rpmType.GetField(
                        "firstRenderComplete",
                        Flags);

                FieldInfo textRefreshRequiredField =
                    rpmType.GetField(
                        "textRefreshRequired",
                        Flags);

                FieldInfo refreshDrawCountdownField =
                    rpmType.GetField(
                        "refreshDrawCountdown",
                        Flags);

                FieldInfo refreshTextCountdownField =
                    rpmType.GetField(
                        "refreshTextCountdown",
                        Flags);

                if (textRendererField == null ||
                    firstRenderCompleteField == null ||
                    textRefreshRequiredField == null ||
                    refreshDrawCountdownField == null ||
                    refreshTextCountdownField == null)
                {
                    continue;
                }

                object textRenderer =
                    textRendererField.GetValue(
                        module);

                if (textRenderer == null)
                {
                    continue;
                }

                Type textRendererType =
                    textRenderer.GetType();

                FieldInfo cachedTextField =
                    textRendererType.GetField(
                        "cachedText",
                        Flags);

                FieldInfo cachedOverlayTextField =
                    textRendererType.GetField(
                        "cachedOverlayText",
                        Flags);

                if (cachedTextField == null ||
                    cachedOverlayTextField == null)
                {
                    continue;
                }

                _rpmModule =
                    module;
                _textRendererField =
                    textRendererField;
                _firstRenderCompleteField =
                    firstRenderCompleteField;
                _textRefreshRequiredField =
                    textRefreshRequiredField;
                _refreshDrawCountdownField =
                    refreshDrawCountdownField;
                _refreshTextCountdownField =
                    refreshTextCountdownField;

                _textRenderer =
                    textRenderer;
                _cachedTextField =
                    cachedTextField;
                _cachedOverlayTextField =
                    cachedOverlayTextField;

                return;
            }
        }

        private void InvalidateRpmRenderCache()
        {
            if (_rpmModule == null ||
                _textRenderer == null ||
                _cachedTextField == null ||
                _cachedOverlayTextField == null ||
                _firstRenderCompleteField == null ||
                _textRefreshRequiredField == null ||
                _refreshDrawCountdownField == null ||
                _refreshTextCountdownField == null)
            {
                FindRpmInternals();
            }

            if (_rpmModule == null ||
                _textRenderer == null ||
                _cachedTextField == null ||
                _cachedOverlayTextField == null ||
                _firstRenderCompleteField == null ||
                _textRefreshRequiredField == null ||
                _refreshDrawCountdownField == null ||
                _refreshTextCountdownField == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[KMC] RPM recovery cache invalidation unavailable | PROP=" +
                    GetPropName() +
                    " | DOMAIN=" +
                    NormalizeDomain());
                return;
            }

            try
            {
                /*
                 * RPM clears its RenderTexture while resourceDepleted is true,
                 * but TextRenderer retains the last rendered page strings.
                 *
                 * If power returns to the SAME page, UpdateText() otherwise sees
                 * identical text and returns false, so RenderScreen() skips the
                 * repaint and the monitor remains black until a page change.
                 *
                 * Nulling both text caches guarantees UpdateText() reports dirty
                 * on the next normal RPM render pass.  The remaining RPM fields
                 * reproduce the same invalidation state RPM establishes during
                 * its own page-change / recovery lifecycle.
                 */
                _cachedTextField.SetValue(
                    _textRenderer,
                    null);

                _cachedOverlayTextField.SetValue(
                    _textRenderer,
                    null);

                _firstRenderCompleteField.SetValue(
                    _rpmModule,
                    false);

                _textRefreshRequiredField.SetValue(
                    _rpmModule,
                    true);

                _refreshDrawCountdownField.SetValue(
                    _rpmModule,
                    0);

                _refreshTextCountdownField.SetValue(
                    _rpmModule,
                    0);

                UnityEngine.Debug.Log(
                    "[KMC] RPM display cache invalidated for recovery | PROP=" +
                    GetPropName() +
                    " | DOMAIN=" +
                    NormalizeDomain());
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    "[KMC] RPM recovery cache invalidation failed | PROP=" +
                    GetPropName() +
                    " | DOMAIN=" +
                    NormalizeDomain() +
                    " | " +
                    ex.GetType().Name);
            }
        }

        private string NormalizeDomain()
        {
            return
                (powerDomain ?? string.Empty)
                    .Trim()
                    .ToUpperInvariant();
        }

        private string GetPropName()
        {
            return
                internalProp == null ||
                string.IsNullOrWhiteSpace(
                    internalProp.propName)
                    ? "<unknown>"
                    : internalProp.propName;
        }
    }
}
