using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// Temporary development-only IVA control authority inspector.
    ///
    /// Captures the active IVA and active vessel before and after one cockpit
    /// control operation, then reports what actually changed.
    ///
    /// Read-only by design:
    /// - does not invoke actions, events, or action groups
    /// - does not alter KMC failures/electrical truth
    /// - does not change IVA transforms/materials/renderers
    /// - does not send network traffic
    ///
    /// Toggle window: Ctrl+Shift+C
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class KmcIvaControlAuthorityInspector : MonoBehaviour
    {
        private const int WindowId = 0x4B4D4319;

        private Rect _windowRect = new Rect(40f, 120f, 640f, 500f);
        private bool _visible;
        private bool _hotkeyWasDown;
        private Vector2 _scroll;

        private Snapshot _before;
        private Snapshot _after;
        private string _status = "READY";
        private string _lastReportPath = string.Empty;

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private static readonly string[] PreferredMemberNames =
        {
            "variableName",
            "actionName",
            "action",
            "actionGroup",
            "switchTransform",
            "controlledTransform",
            "animationName",
            "state",
            "currentState",
            "isOn",
            "enabled",
            "active",
            "value",
            "mode",
            "resourceName",
            "needsElectricCharge",
            "propName",
            "propID"
        };

        public void Start()
        {
            try
            {
                ScreenMessages.PostScreenMessage(
                    "KMC Control Authority Inspector: Ctrl+Shift+C",
                    5f,
                    ScreenMessageStyle.UPPER_CENTER);

                Debug.Log(
                    "[KMC CONTROL AUTHORITY INSPECTOR] Started. Toggle with Ctrl+Shift+C.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC CONTROL AUTHORITY INSPECTOR] Start failed: " + ex);
            }
        }

        public void Update()
        {
            bool hotkeyDown =
                (Input.GetKey(KeyCode.LeftControl) ||
                 Input.GetKey(KeyCode.RightControl)) &&
                (Input.GetKey(KeyCode.LeftShift) ||
                 Input.GetKey(KeyCode.RightShift)) &&
                Input.GetKey(KeyCode.C);

            if (hotkeyDown && !_hotkeyWasDown)
            {
                _visible = !_visible;
            }

            _hotkeyWasDown = hotkeyDown;
        }

        public void OnGUI()
        {
            if (!_visible)
                return;

            _windowRect = GUILayout.Window(
                WindowId,
                _windowRect,
                DrawWindow,
                "KMC IVA CONTROL AUTHORITY INSPECTOR");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Label("DEVELOPMENT / READ-ONLY DIAGNOSTIC");
            GUILayout.Label(
                "Capture BEFORE, operate exactly one IVA switch/button, then capture AFTER.");

            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("CAPTURE BEFORE", GUILayout.Height(36f)))
            {
                CaptureBefore();
            }

            if (GUILayout.Button("CAPTURE AFTER", GUILayout.Height(36f)))
            {
                CaptureAfter();
            }

            GUILayout.EndHorizontal();

            if (GUILayout.Button("COMPARE + WRITE REPORT", GUILayout.Height(36f)))
            {
                CompareAndWrite();
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("WRITE CURRENT INVENTORY", GUILayout.Height(30f)))
            {
                WriteCurrentInventory();
            }

            if (GUILayout.Button("CLEAR", GUILayout.Height(30f)))
            {
                _before = null;
                _after = null;
                _status = "SNAPSHOTS CLEARED";
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("STATUS: " + _status);
            GUILayout.Label("BEFORE: " + DescribeSnapshot(_before));
            GUILayout.Label("AFTER:  " + DescribeSnapshot(_after));

            if (!string.IsNullOrEmpty(_lastReportPath))
            {
                GUILayout.Space(5f);
                GUILayout.Label("LAST REPORT:");

                _scroll = GUILayout.BeginScrollView(
                    _scroll,
                    GUILayout.Height(125f));

                GUILayout.TextArea(_lastReportPath);
                GUILayout.EndScrollView();
            }

            GUILayout.Space(5f);
            GUILayout.Label("Output folder: <KSP>/KMC_ControlAuthorityInspector");
            GUILayout.Label(
                "The tool observes state only. It never operates the control for you.");

            GUI.DragWindow();
        }

        private void CaptureBefore()
        {
            try
            {
                _before = CaptureSnapshot("BEFORE");
                _status = "BEFORE CAPTURED";
            }
            catch (Exception ex)
            {
                HandleError("BEFORE capture failed", ex);
            }
        }

        private void CaptureAfter()
        {
            try
            {
                _after = CaptureSnapshot("AFTER");
                _status = "AFTER CAPTURED";
            }
            catch (Exception ex)
            {
                HandleError("AFTER capture failed", ex);
            }
        }

        private void CompareAndWrite()
        {
            try
            {
                if (_before == null)
                {
                    _status = "CAPTURE BEFORE FIRST";
                    return;
                }

                if (_after == null)
                {
                    _status = "CAPTURE AFTER FIRST";
                    return;
                }

                string report = CreateComparisonReport(_before, _after);
                _lastReportPath = WriteReport("COMPARE", report);
                _status = "COMPARE REPORT WRITTEN";

                ScreenMessages.PostScreenMessage(
                    "KMC control authority report written",
                    5f,
                    ScreenMessageStyle.UPPER_CENTER);
            }
            catch (Exception ex)
            {
                HandleError("Compare failed", ex);
            }
        }

        private void WriteCurrentInventory()
        {
            try
            {
                Snapshot current = CaptureSnapshot("CURRENT");
                string report = CreateInventoryReport(current);
                _lastReportPath = WriteReport("INVENTORY", report);
                _status = "CURRENT INVENTORY WRITTEN";
            }
            catch (Exception ex)
            {
                HandleError("Inventory failed", ex);
            }
        }

        private static Snapshot CaptureSnapshot(string label)
        {
            Snapshot snapshot = new Snapshot();
            snapshot.Label = label ?? string.Empty;
            snapshot.TimestampUtc = DateTime.UtcNow;

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel != null)
            {
                snapshot.VesselName = vessel.vesselName ?? string.Empty;
                snapshot.VesselId = vessel.id.ToString();

                CaptureActionGroups(vessel, snapshot);
                CaptureVesselState(vessel, snapshot);
                CapturePartModules(vessel, snapshot);
            }

            CaptureLiveIva(snapshot);

            return snapshot;
        }

        private static void CaptureActionGroups(Vessel vessel, Snapshot snapshot)
        {
            if (vessel == null || vessel.ActionGroups == null)
                return;

            Array values = Enum.GetValues(typeof(KSPActionGroup));

            foreach (object value in values)
            {
                KSPActionGroup group = (KSPActionGroup)value;

                try
                {
                    bool active = vessel.ActionGroups[group];
                    snapshot.ActionGroups[group.ToString()] = active ? "1" : "0";
                }
                catch
                {
                    // Some aggregate/sentinel enum values are not queryable.
                }
            }
        }

        private static void CaptureVesselState(Vessel vessel, Snapshot snapshot)
        {
            AddState(snapshot.VesselState, "vessel.situation", vessel.situation.ToString());
            AddState(snapshot.VesselState, "vessel.Landed", vessel.Landed ? "1" : "0");
            AddState(snapshot.VesselState, "vessel.Splashed", vessel.Splashed ? "1" : "0");

            object ctrlState = null;

            try
            {
                ctrlState = vessel.ctrlState;
            }
            catch
            {
                ctrlState = null;
            }

            if (ctrlState != null)
            {
                CaptureScalarObject(
                    ctrlState,
                    "ctrlState",
                    snapshot.VesselState,
                    80,
                    false);
            }

            object autopilot = null;

            try
            {
                autopilot = vessel.Autopilot;
            }
            catch
            {
                autopilot = null;
            }

            if (autopilot != null)
            {
                CaptureScalarObject(
                    autopilot,
                    "autopilot",
                    snapshot.VesselState,
                    80,
                    true);
            }
        }

        private static void CapturePartModules(Vessel vessel, Snapshot snapshot)
        {
            if (vessel == null || vessel.parts == null)
                return;

            for (int p = 0; p < vessel.parts.Count; p++)
            {
                Part part = vessel.parts[p];
                if (part == null)
                    continue;

                string partKey =
                    "PART[" +
                    p.ToString(CultureInfo.InvariantCulture) +
                    "] " +
                    SafePartName(part);

                snapshot.PartState[partKey + "|enabled"] =
                    part.enabled ? "1" : "0";

                if (part.Modules == null)
                    continue;

                for (int m = 0; m < part.Modules.Count; m++)
                {
                    PartModule module = part.Modules[m];
                    if (module == null)
                        continue;

                    string prefix =
                        partKey +
                        "|MODULE[" +
                        m.ToString(CultureInfo.InvariantCulture) +
                        "] " +
                        module.GetType().FullName;

                    CaptureScalarObject(
                        module,
                        prefix,
                        snapshot.PartState,
                        120,
                        true);
                }
            }
        }

        private static void CaptureLiveIva(Snapshot snapshot)
        {
            UnityEngine.Object[] found =
                Resources.FindObjectsOfTypeAll(typeof(InternalProp));

            if (found == null)
                return;

            for (int i = 0; i < found.Length; i++)
            {
                InternalProp prop = found[i] as InternalProp;

                if (prop == null ||
                    prop.gameObject == null ||
                    !LooksLikeLiveIvaProp(prop))
                {
                    continue;
                }

                string propName = GetMemberText(prop, "propName");
                string propId = GetMemberText(prop, "propID");

                if (string.IsNullOrEmpty(propName))
                    propName = prop.name ?? prop.gameObject.name;

                string propPrefix =
                    "PROP[" +
                    propId +
                    "] " +
                    propName;

                snapshot.IvaProps.Add(propPrefix);

                MonoBehaviour[] behaviours =
                    prop.GetComponentsInChildren<MonoBehaviour>(true);

                for (int b = 0; b < behaviours.Length; b++)
                {
                    MonoBehaviour behaviour = behaviours[b];
                    if (behaviour == null)
                        continue;

                    string prefix =
                        propPrefix +
                        "|BEHAVIOUR[" +
                        b.ToString(CultureInfo.InvariantCulture) +
                        "] " +
                        behaviour.GetType().FullName;

                    snapshot.IvaModules.Add(prefix);

                    CapturePreferredMembers(
                        behaviour,
                        prefix,
                        snapshot.IvaState);

                    CaptureScalarObject(
                        behaviour,
                        prefix,
                        snapshot.IvaState,
                        80,
                        false);
                }
            }
        }

        private static bool LooksLikeLiveIvaProp(InternalProp prop)
        {
            Transform cursor = prop.transform;

            while (cursor != null)
            {
                Component[] components = cursor.GetComponents<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];

                    if (component == null)
                        continue;

                    if (string.Equals(
                        component.GetType().Name,
                        "InternalModel",
                        StringComparison.Ordinal))
                    {
                        return cursor.gameObject.activeInHierarchy;
                    }
                }

                cursor = cursor.parent;
            }

            return false;
        }

        private static void CapturePreferredMembers(
            object obj,
            string prefix,
            Dictionary<string, string> output)
        {
            for (int i = 0; i < PreferredMemberNames.Length; i++)
            {
                string name = PreferredMemberNames[i];
                string text = GetMemberText(obj, name);

                if (!string.IsNullOrEmpty(text))
                {
                    output[prefix + "|" + name] = text;
                }
            }
        }

        private static void CaptureScalarObject(
            object obj,
            string prefix,
            Dictionary<string, string> output,
            int limit,
            bool publicOnly)
        {
            if (obj == null)
                return;

            Type type = obj.GetType();
            BindingFlags flags = publicOnly
                ? BindingFlags.Instance | BindingFlags.Public
                : InstanceFlags;

            int count = 0;

            FieldInfo[] fields = type.GetFields(flags);

            for (int i = 0; i < fields.Length && count < limit; i++)
            {
                FieldInfo field = fields[i];

                if (field == null ||
                    field.IsStatic ||
                    !IsInterestingScalarType(field.FieldType))
                {
                    continue;
                }

                try
                {
                    object value = field.GetValue(obj);
                    output[prefix + "|FIELD:" + field.Name] =
                        ScalarToString(value);
                    count++;
                }
                catch
                {
                }
            }

            PropertyInfo[] properties = type.GetProperties(flags);

            for (int i = 0; i < properties.Length && count < limit; i++)
            {
                PropertyInfo property = properties[i];

                if (property == null ||
                    !property.CanRead ||
                    property.GetIndexParameters().Length != 0 ||
                    !IsInterestingScalarType(property.PropertyType))
                {
                    continue;
                }

                MethodInfo getter = property.GetGetMethod(!publicOnly);

                if (getter == null || getter.IsStatic)
                    continue;

                try
                {
                    object value = property.GetValue(obj, null);
                    output[prefix + "|PROP:" + property.Name] =
                        ScalarToString(value);
                    count++;
                }
                catch
                {
                }
            }
        }

        private static bool IsInterestingScalarType(Type type)
        {
            if (type == null)
                return false;

            if (type == typeof(string) ||
                type == typeof(bool) ||
                type == typeof(byte) ||
                type == typeof(sbyte) ||
                type == typeof(short) ||
                type == typeof(ushort) ||
                type == typeof(int) ||
                type == typeof(uint) ||
                type == typeof(long) ||
                type == typeof(ulong) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(decimal))
            {
                return true;
            }

            return type.IsEnum;
        }

        private static string ScalarToString(object value)
        {
            if (value == null)
                return "<null>";

            IFormattable formattable = value as IFormattable;

            if (formattable != null)
            {
                return formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        private static string GetMemberText(object obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name))
                return string.Empty;

            Type type = obj.GetType();

            try
            {
                FieldInfo field = type.GetField(name, InstanceFlags);

                if (field != null && !field.IsStatic)
                {
                    object value = field.GetValue(obj);
                    return value != null ? value.ToString() : string.Empty;
                }
            }
            catch
            {
            }

            try
            {
                PropertyInfo property = type.GetProperty(name, InstanceFlags);

                if (property != null &&
                    property.CanRead &&
                    property.GetIndexParameters().Length == 0)
                {
                    object value = property.GetValue(obj, null);
                    return value != null ? value.ToString() : string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string SafePartName(Part part)
        {
            if (part == null)
                return "<null>";

            try
            {
                if (part.partInfo != null &&
                    !string.IsNullOrEmpty(part.partInfo.name))
                {
                    return part.partInfo.name;
                }
            }
            catch
            {
            }

            return part.name ?? "<unnamed>";
        }

        private static void AddState(
            Dictionary<string, string> state,
            string key,
            string value)
        {
            state[key] = value ?? string.Empty;
        }

        private static string CreateComparisonReport(
            Snapshot before,
            Snapshot after)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("KMC IVA CONTROL AUTHORITY INSPECTOR");
            sb.AppendLine("BEFORE VS AFTER COMPARISON");
            sb.AppendLine("Generated UTC: " +
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("Before UTC: " +
                before.TimestampUtc.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("After UTC: " +
                after.TimestampUtc.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("Vessel: " + before.VesselName);
            sb.AppendLine("Vessel ID: " + before.VesselId);
            sb.AppendLine();

            int actionChanges = AppendDiffSection(
                sb,
                "ACTION GROUP CHANGES",
                before.ActionGroups,
                after.ActionGroups);

            int vesselChanges = AppendDiffSection(
                sb,
                "VESSEL / FLIGHT CONTROL CHANGES",
                before.VesselState,
                after.VesselState);

            int partChanges = AppendDiffSection(
                sb,
                "PART / PARTMODULE CHANGES",
                before.PartState,
                after.PartState);

            int ivaChanges = AppendDiffSection(
                sb,
                "IVA PROP / MODULE CHANGES",
                before.IvaState,
                after.IvaState);

            sb.AppendLine("============================================================");
            sb.AppendLine("SUMMARY");
            sb.AppendLine("Action-group changes: " + actionChanges);
            sb.AppendLine("Vessel/control changes: " + vesselChanges);
            sb.AppendLine("Part/module changes: " + partChanges);
            sb.AppendLine("IVA prop/module changes: " + ivaChanges);
            sb.AppendLine();
            sb.AppendLine("Interpretation:");
            sb.AppendLine(
                "This report records observed state differences only. " +
                "Operate exactly one cockpit control between captures.");
            sb.AppendLine(
                "A KMC override risk exists only if a cockpit control causes " +
                "the real KSP system to enter a state that contradicts an active KMC failure.");

            return sb.ToString();
        }

        private static int AppendDiffSection(
            StringBuilder sb,
            string title,
            Dictionary<string, string> before,
            Dictionary<string, string> after)
        {
            sb.AppendLine("============================================================");
            sb.AppendLine(title);

            SortedSet<string> keys = new SortedSet<string>(
                StringComparer.Ordinal);

            foreach (string key in before.Keys)
                keys.Add(key);

            foreach (string key in after.Keys)
                keys.Add(key);

            int changes = 0;

            foreach (string key in keys)
            {
                string a;
                string b;

                bool hasA = before.TryGetValue(key, out a);
                bool hasB = after.TryGetValue(key, out b);

                if (hasA && hasB &&
                    string.Equals(a, b, StringComparison.Ordinal))
                {
                    continue;
                }

                changes++;

                sb.AppendLine();
                sb.AppendLine(key);
                sb.AppendLine("  BEFORE: " + (hasA ? a : "<missing>"));
                sb.AppendLine("  AFTER:  " + (hasB ? b : "<missing>"));
            }

            if (changes == 0)
            {
                sb.AppendLine();
                sb.AppendLine("<no changes>");
            }

            sb.AppendLine();
            return changes;
        }

        private static string CreateInventoryReport(Snapshot snapshot)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("KMC IVA CONTROL AUTHORITY INSPECTOR");
            sb.AppendLine("CURRENT CONTROL INVENTORY");
            sb.AppendLine("Generated UTC: " +
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("Vessel: " + snapshot.VesselName);
            sb.AppendLine("Vessel ID: " + snapshot.VesselId);
            sb.AppendLine();

            sb.AppendLine("============================================================");
            sb.AppendLine("LIVE IVA PROPS");

            for (int i = 0; i < snapshot.IvaProps.Count; i++)
                sb.AppendLine(snapshot.IvaProps[i]);

            sb.AppendLine();
            sb.AppendLine("============================================================");
            sb.AppendLine("LIVE IVA MONOBEHAVIOURS");

            for (int i = 0; i < snapshot.IvaModules.Count; i++)
                sb.AppendLine(snapshot.IvaModules[i]);

            sb.AppendLine();
            sb.AppendLine("============================================================");
            sb.AppendLine("ACTION GROUP STATE");

            foreach (KeyValuePair<string, string> kvp in snapshot.ActionGroups)
                sb.AppendLine(kvp.Key + " = " + kvp.Value);

            return sb.ToString();
        }

        private static string WriteReport(string kind, string report)
        {
            string root = KSPUtil.ApplicationRootPath;
            string folder = Path.Combine(
                root,
                "KMC_ControlAuthorityInspector");

            Directory.CreateDirectory(folder);

            string fileName =
                "KMC_ControlAuthority_" +
                kind +
                "_" +
                DateTime.UtcNow.ToString(
                    "yyyyMMdd_HHmmss",
                    CultureInfo.InvariantCulture) +
                ".txt";

            string path = Path.Combine(folder, fileName);

            File.WriteAllText(
                path,
                report ?? string.Empty,
                Encoding.UTF8);

            Debug.Log(
                "[KMC CONTROL AUTHORITY INSPECTOR] Wrote " +
                path);

            return path;
        }

        private static string DescribeSnapshot(Snapshot snapshot)
        {
            if (snapshot == null)
                return "<none>";

            return
                snapshot.Label +
                " / " +
                snapshot.VesselName +
                " / " +
                snapshot.IvaProps.Count +
                " IVA props";
        }

        private void HandleError(string message, Exception ex)
        {
            _status = message.ToUpperInvariant();

            Debug.LogError(
                "[KMC CONTROL AUTHORITY INSPECTOR] " +
                message +
                ": " +
                ex);

            try
            {
                ScreenMessages.PostScreenMessage(
                    "KMC Control Authority Inspector error - see KSP.log",
                    5f,
                    ScreenMessageStyle.UPPER_CENTER);
            }
            catch
            {
            }
        }

        private sealed class Snapshot
        {
            public string Label = string.Empty;
            public DateTime TimestampUtc;
            public string VesselName = string.Empty;
            public string VesselId = string.Empty;

            public readonly Dictionary<string, string> ActionGroups =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public readonly Dictionary<string, string> VesselState =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public readonly Dictionary<string, string> PartState =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public readonly Dictionary<string, string> IvaState =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public readonly List<string> IvaProps =
                new List<string>();

            public readonly List<string> IvaModules =
                new List<string>();
        }
    }
}
