using System;
using System.Collections.Generic;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// KMC Build 14.18.6 Rev B development proof-of-concept.
    ///
    /// Proves downstream RCS authority enforcement:
    /// - KSP InputLockManager blocks normal stock input paths.
    /// - Actual ModuleRCS / ModuleRCSFX PartModules are disabled while the
    ///   temporary KMC test condition is active.
    /// - IVA controls are NOT patched or frozen. They may still visually move
    ///   and may even set the vessel RCS action-group state ON.
    /// - The test succeeds if the RCS hardware itself cannot produce thrust.
    ///
    /// Each affected PartModule's prior enabled state is remembered and
    /// restored exactly when the test condition clears.
    ///
    /// This is NOT yet connected to production KMC RCS failure truth.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class KmcRcsControlAuthorityTest : MonoBehaviour
    {
        private const string LockId =
            "KMC_14_18_6_RCS_AUTHORITY_TEST";

        private const int WindowId = 0x4B4D431A;

        private readonly Dictionary<PartModule, bool>
            _priorModuleEnabled =
                new Dictionary<PartModule, bool>();

        private Rect _windowRect =
            new Rect(20f, 620f, 470f, 390f);

        private bool _visible = true;
        private bool _authorityRemoved;
        private bool _rcsControlTypeResolved;
        private ControlTypes _rcsControlType;
        private Guid _enforcedVesselId = Guid.Empty;

        private int _detectedRcsModules;
        private int _disabledRcsModules;
        private string _status = "READY";

        public void Start()
        {
            ResolveRcsControlType();

            Debug.Log(
                "[KMC RCS AUTHORITY TEST] " +
                "Build 14.18.6 Rev B started.");
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                _visible = !_visible;
            }

            if (_authorityRemoved)
            {
                EnforceAuthorityRemoval();
            }
        }

        public void OnGUI()
        {
            if (!_visible)
                return;

            _windowRect =
                GUILayout.Window(
                    WindowId,
                    _windowRect,
                    DrawWindow,
                    "KMC RCS HARDWARE AUTHORITY TEST");
        }

        private void DrawWindow(int windowId)
        {
            Vessel vessel =
                FlightGlobals.ActiveVessel;

            GUILayout.Label(
                "BUILD 14.18.6 REV B / DEVELOPMENT PROOF");

            if (vessel == null)
            {
                GUILayout.Label("NO ACTIVE VESSEL");
                GUILayout.Label("F9: SHOW / HIDE");
                GUI.DragWindow();
                return;
            }

            GUILayout.Space(5f);

            GUILayout.Label(
                "RCS CONTROL TYPE: " +
                (_rcsControlTypeResolved
                    ? _rcsControlType.ToString()
                    : "NOT RESOLVED"));

            GUILayout.Label(
                "KMC RCS AUTHORITY: " +
                (_authorityRemoved
                    ? "REMOVED / HARDWARE INHIBITED"
                    : "AVAILABLE"));

            GUILayout.Label(
                "INPUT LOCK ACTIVE: " +
                GetInputLockState());

            GUILayout.Label(
                "VESSEL RCS BLOCKED: " +
                GetVesselBlockedState(vessel));

            GUILayout.Label(
                "RCS ACTION GROUP: " +
                GetRcsActionGroupState(vessel));

            GUILayout.Label(
                "RCS MODULES DETECTED: " +
                _detectedRcsModules);

            GUILayout.Label(
                "RCS MODULES DISABLED: " +
                _disabledRcsModules);

            GUILayout.Space(8f);

            if (!_authorityRemoved)
            {
                if (GUILayout.Button(
                    "REMOVE RCS HARDWARE AUTHORITY",
                    GUILayout.Height(38f)))
                {
                    BeginAuthorityRemoval();
                }
            }
            else
            {
                if (GUILayout.Button(
                    "RESTORE RCS HARDWARE AUTHORITY",
                    GUILayout.Height(38f)))
                {
                    RestoreAuthority();
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label("TEST INTENT:");
            GUILayout.Label(
                "Keyboard R should remain blocked.");
            GUILayout.Label(
                "IVA RCS switch MAY move / show ON.");
            GUILayout.Label(
                "Even if RCS ACTION GROUP becomes ON, jets must NOT fire.");
            GUILayout.Label(
                "No RCS plume / thrust / monoprop consumption should occur.");

            GUILayout.Space(6f);
            GUILayout.Label("STATUS: " + _status);
            GUILayout.Label("F9: SHOW / HIDE");

            GUI.DragWindow();
        }

        private void ResolveRcsControlType()
        {
            try
            {
                object parsed =
                    Enum.Parse(
                        typeof(ControlTypes),
                        "RCS",
                        true);

                _rcsControlType =
                    (ControlTypes)parsed;

                _rcsControlTypeResolved = true;
                _status =
                    "RCS CONTROL TYPE RESOLVED";
            }
            catch (Exception ex)
            {
                _rcsControlTypeResolved = false;
                _status =
                    "RCS CONTROL TYPE NOT FOUND";

                Debug.LogError(
                    "[KMC RCS AUTHORITY TEST] " +
                    "Could not resolve ControlTypes.RCS: " +
                    ex);
            }
        }

        private void BeginAuthorityRemoval()
        {
            if (!_rcsControlTypeResolved)
            {
                _status =
                    "CANNOT APPLY - RCS CONTROL TYPE NOT RESOLVED";
                return;
            }

            _authorityRemoved = true;
            EnforceAuthorityRemoval();

            _status =
                "RCS HARDWARE AUTHORITY REMOVED";

            Debug.Log(
                "[KMC RCS AUTHORITY TEST] " +
                "RCS authority removal requested.");
        }

        private void EnforceAuthorityRemoval()
        {
            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null)
                return;

            if (_enforcedVesselId != Guid.Empty &&
                _enforcedVesselId != vessel.id)
            {
                RestoreTrackedModules();
            }

            _enforcedVesselId =
                vessel.id;

            try
            {
                InputLockManager.SetControlLock(
                    _rcsControlType,
                    LockId);
            }
            catch (Exception ex)
            {
                _status =
                    "INPUT LOCK APPLY FAILED";

                Debug.LogError(
                    "[KMC RCS AUTHORITY TEST] " +
                    "SetControlLock failed: " +
                    ex);
            }

            DisableRcsHardware(vessel);
        }

        private void DisableRcsHardware(
            Vessel vessel)
        {
            int detected = 0;
            int disabled = 0;

            if (vessel.parts != null)
            {
                for (int p = 0;
                     p < vessel.parts.Count;
                     p++)
                {
                    Part part =
                        vessel.parts[p];

                    if (part == null ||
                        part.Modules == null)
                    {
                        continue;
                    }

                    for (int m = 0;
                         m < part.Modules.Count;
                         m++)
                    {
                        PartModule module =
                            part.Modules[m];

                        if (!IsRcsModule(module))
                            continue;

                        detected++;

                        if (!_priorModuleEnabled.ContainsKey(
                                module))
                        {
                            _priorModuleEnabled[module] =
                                module.enabled;
                        }

                        if (module.enabled)
                        {
                            module.enabled = false;
                        }

                        if (!module.enabled)
                        {
                            disabled++;
                        }
                    }
                }
            }

            _detectedRcsModules = detected;
            _disabledRcsModules = disabled;
        }

        private static bool IsRcsModule(
            PartModule module)
        {
            if (module == null)
                return false;

            string moduleName =
                module.moduleName ??
                string.Empty;

            string typeName =
                module.GetType().Name ??
                string.Empty;

            return
                string.Equals(
                    moduleName,
                    "ModuleRCS",
                    StringComparison.Ordinal) ||
                string.Equals(
                    moduleName,
                    "ModuleRCSFX",
                    StringComparison.Ordinal) ||
                string.Equals(
                    typeName,
                    "ModuleRCS",
                    StringComparison.Ordinal) ||
                string.Equals(
                    typeName,
                    "ModuleRCSFX",
                    StringComparison.Ordinal);
        }

        private void RestoreAuthority()
        {
            try
            {
                InputLockManager.RemoveControlLock(
                    LockId);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC RCS AUTHORITY TEST] " +
                    "RemoveControlLock failed: " +
                    ex);
            }

            RestoreTrackedModules();

            _authorityRemoved = false;
            _enforcedVesselId = Guid.Empty;
            _detectedRcsModules = 0;
            _disabledRcsModules = 0;
            _status =
                "RCS HARDWARE AUTHORITY RESTORED";

            Debug.Log(
                "[KMC RCS AUTHORITY TEST] " +
                "RCS authority restored.");
        }

        private void RestoreTrackedModules()
        {
            foreach (
                KeyValuePair<PartModule, bool> pair
                in _priorModuleEnabled)
            {
                PartModule module =
                    pair.Key;

                if (module == null)
                    continue;

                try
                {
                    module.enabled =
                        pair.Value;
                }
                catch
                {
                }
            }

            _priorModuleEnabled.Clear();
        }

        private string GetInputLockState()
        {
            if (!_rcsControlTypeResolved)
                return "UNKNOWN";

            try
            {
                return
                    InputLockManager.IsLocked(
                        _rcsControlType)
                        ? "YES"
                        : "NO";
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        private static string GetVesselBlockedState(
            Vessel vessel)
        {
            if (vessel == null)
                return "UNKNOWN";

            try
            {
                return
                    vessel.ActionControlBlocked(
                        KSPActionGroup.RCS)
                        ? "YES"
                        : "NO";
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        private static string GetRcsActionGroupState(
            Vessel vessel)
        {
            if (vessel == null ||
                vessel.ActionGroups == null)
            {
                return "UNKNOWN";
            }

            try
            {
                return
                    vessel.ActionGroups[
                        KSPActionGroup.RCS]
                        ? "ON"
                        : "OFF";
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        public void OnDestroy()
        {
            try
            {
                InputLockManager.RemoveControlLock(
                    LockId);
            }
            catch
            {
            }

            RestoreTrackedModules();

            _authorityRemoved = false;
            _enforcedVesselId = Guid.Empty;

            Debug.Log(
                "[KMC RCS AUTHORITY TEST] " +
                "Destroyed; KMC named lock removed and " +
                "tracked RCS module states restored.");
        }
    }
}
