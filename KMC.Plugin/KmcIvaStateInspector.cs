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
    /// Temporary development-only IVA runtime state inspector.
    ///
    /// Purpose:
    /// Capture the live IVA prop hierarchy once while powered and once while
    /// genuinely unpowered, then report exactly which runtime states changed.
    ///
    /// Read-only by design:
    /// - does not alter renderers/materials/textures/transforms
    /// - does not send network traffic
    /// - does not participate in KMC electrical truth
    /// - does not touch RPM cache/wake logic
    ///
    /// Toggle window: Ctrl+Shift+I
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class KmcIvaStateInspector :
        MonoBehaviour
    {
        private const int WindowId =
            0x4B4D4318;

        private Rect _windowRect =
            new Rect(
                40f,
                120f,
                560f,
                430f);

        private bool _visible;

        private Vector2 _scroll;

        private Snapshot _poweredSnapshot;
        private Snapshot _currentSnapshot;

        private string _status =
            "READY";

        private string _lastReportPath =
            string.Empty;

        private bool _hotkeyWasDown;

        private static readonly BindingFlags
            DiagnosticBindingFlags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

        private static readonly string[]
            InterestingMemberNames =
            {
                "variableName",
                "animationName",
                "controlledTransform",
                "transformName",
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
                    "KMC IVA Inspector: Ctrl+Shift+I",
                    5f,
                    ScreenMessageStyle.UPPER_CENTER);

                Debug.Log(
                    "[KMC IVA INSPECTOR] Started. " +
                    "Toggle with Ctrl+Shift+I.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC IVA INSPECTOR] Start failed: " +
                    ex);
            }
        }

        public void Update()
        {
            bool hotkeyDown =
                (Input.GetKey(KeyCode.LeftControl) ||
                 Input.GetKey(KeyCode.RightControl)) &&
                (Input.GetKey(KeyCode.LeftShift) ||
                 Input.GetKey(KeyCode.RightShift)) &&
                Input.GetKey(KeyCode.I);

            if (hotkeyDown &&
                !_hotkeyWasDown)
            {
                _visible =
                    !_visible;
            }

            _hotkeyWasDown =
                hotkeyDown;
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
                    "KMC IVA STATE INSPECTOR");
        }

        private void DrawWindow(
            int windowId)
        {
            GUILayout.Label(
                "DEVELOPMENT / READ-ONLY DIAGNOSTIC");

            GUILayout.Label(
                "Capture the same IVA once powered, then again in the " +
                "real zero-power state.");

            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                "CAPTURE POWERED",
                GUILayout.Height(34f)))
            {
                CapturePowered();
            }

            if (GUILayout.Button(
                "CAPTURE CURRENT",
                GUILayout.Height(34f)))
            {
                CaptureCurrent();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(
                "COMPARE + WRITE REPORT",
                GUILayout.Height(34f)))
            {
                CompareAndWrite();
            }

            if (GUILayout.Button(
                "WRITE FULL CURRENT DUMP",
                GUILayout.Height(34f)))
            {
                WriteCurrentDump();
            }

            GUILayout.EndHorizontal();

            if (GUILayout.Button(
                "CLEAR SNAPSHOTS",
                GUILayout.Height(28f)))
            {
                _poweredSnapshot =
                    null;

                _currentSnapshot =
                    null;

                _status =
                    "SNAPSHOTS CLEARED";
            }

            GUILayout.Space(8f);

            GUILayout.Label(
                "STATUS: " +
                _status);

            GUILayout.Label(
                "POWERED: " +
                DescribeSnapshot(
                    _poweredSnapshot));

            GUILayout.Label(
                "CURRENT: " +
                DescribeSnapshot(
                    _currentSnapshot));

            if (!string.IsNullOrEmpty(
                _lastReportPath))
            {
                GUILayout.Label(
                    "LAST REPORT:");

                _scroll =
                    GUILayout.BeginScrollView(
                        _scroll,
                        GUILayout.Height(115f));

                GUILayout.TextArea(
                    _lastReportPath);

                GUILayout.EndScrollView();
            }

            GUILayout.Space(5f);

            GUILayout.Label(
                "Output folder: <KSP>/KMC_IvaInspector");

            GUILayout.Label(
                "This tool observes runtime state only. " +
                "It never powers props on/off.");

            GUI.DragWindow();
        }

        private void CapturePowered()
        {
            try
            {
                _poweredSnapshot =
                    CaptureSnapshot(
                        "POWERED");

                _status =
                    "POWERED CAPTURED / " +
                    _poweredSnapshot.Props.Count +
                    " PROPS";
            }
            catch (Exception ex)
            {
                HandleError(
                    "Powered capture failed",
                    ex);
            }
        }

        private void CaptureCurrent()
        {
            try
            {
                _currentSnapshot =
                    CaptureSnapshot(
                        "CURRENT");

                _status =
                    "CURRENT CAPTURED / " +
                    _currentSnapshot.Props.Count +
                    " PROPS";
            }
            catch (Exception ex)
            {
                HandleError(
                    "Current capture failed",
                    ex);
            }
        }

        private void CompareAndWrite()
        {
            try
            {
                if (_poweredSnapshot == null)
                {
                    _status =
                        "CAPTURE POWERED FIRST";

                    return;
                }

                if (_currentSnapshot == null)
                {
                    _status =
                        "CAPTURE CURRENT FIRST";

                    return;
                }

                string report =
                    CreateComparisonReport(
                        _poweredSnapshot,
                        _currentSnapshot);

                _lastReportPath =
                    WriteReport(
                        "COMPARE",
                        report);

                _status =
                    "COMPARE REPORT WRITTEN";

                ScreenMessages.PostScreenMessage(
                    "KMC IVA compare report written",
                    5f,
                    ScreenMessageStyle.UPPER_CENTER);
            }
            catch (Exception ex)
            {
                HandleError(
                    "Compare failed",
                    ex);
            }
        }

        private void WriteCurrentDump()
        {
            try
            {
                Snapshot snapshot =
                    _currentSnapshot ??
                    CaptureSnapshot(
                        "CURRENT");

                _currentSnapshot =
                    snapshot;

                string report =
                    CreateFullDump(
                        snapshot);

                _lastReportPath =
                    WriteReport(
                        "FULL",
                        report);

                _status =
                    "FULL CURRENT DUMP WRITTEN";

                ScreenMessages.PostScreenMessage(
                    "KMC IVA full dump written",
                    5f,
                    ScreenMessageStyle.UPPER_CENTER);
            }
            catch (Exception ex)
            {
                HandleError(
                    "Full dump failed",
                    ex);
            }
        }

        private static Snapshot CaptureSnapshot(
            string label)
        {
            Snapshot snapshot =
                new Snapshot
                {
                    Label =
                        label ?? string.Empty,

                    TimestampUtc =
                        DateTime.UtcNow,

                    VesselName =
                        FlightGlobals.ActiveVessel != null
                            ? FlightGlobals.ActiveVessel.vesselName
                            : string.Empty
                };

            UnityEngine.Object[] found =
                Resources.FindObjectsOfTypeAll(
                    typeof(InternalProp));

            if (found == null)
                return snapshot;

            for (int i = 0;
                 i < found.Length;
                 i++)
            {
                InternalProp prop =
                    found[i]
                    as InternalProp;

                if (prop == null ||
                    prop.gameObject == null)
                {
                    continue;
                }

                if (!LooksLikeLiveIvaProp(
                    prop))
                {
                    continue;
                }

                PropSnapshot propSnapshot =
                    CaptureProp(
                        prop);

                snapshot.Props[
                    propSnapshot.InstanceId] =
                        propSnapshot;

                if (string.IsNullOrEmpty(
                    snapshot.InternalName))
                {
                    snapshot.InternalName =
                        propSnapshot.InternalRoot;
                }
            }

            return snapshot;
        }

        private static bool LooksLikeLiveIvaProp(
            InternalProp prop)
        {
            if (prop == null ||
                prop.gameObject == null)
            {
                return false;
            }

            Transform cursor =
                prop.transform;

            while (cursor != null)
            {
                Component[] components =
                    cursor.GetComponents<Component>();

                for (int i = 0;
                     i < components.Length;
                     i++)
                {
                    Component component =
                        components[i];

                    if (component == null)
                        continue;

                    if (string.Equals(
                        component.GetType().Name,
                        "InternalModel",
                        StringComparison.Ordinal))
                    {
                        return cursor.gameObject
                            .activeInHierarchy;
                    }
                }

                cursor =
                    cursor.parent;
            }

            return false;
        }

        private static PropSnapshot CaptureProp(
            InternalProp prop)
        {
            PropSnapshot snapshot =
                new PropSnapshot
                {
                    InstanceId =
                        prop.GetInstanceID(),

                    PropName =
                        GetMemberText(
                            prop,
                            "propName"),

                    PropId =
                        GetMemberText(
                            prop,
                            "propID"),

                    GameObjectName =
                        prop.gameObject.name,

                    TransformPath =
                        BuildTransformPath(
                            prop.transform),

                    InternalRoot =
                        FindInternalRootName(
                            prop.transform)
                };

            if (string.IsNullOrEmpty(
                snapshot.PropName))
            {
                snapshot.PropName =
                    prop.name;
            }

            Transform[] transforms =
                prop.GetComponentsInChildren<Transform>(
                    true);

            for (int i = 0;
                 i < transforms.Length;
                 i++)
            {
                Transform t =
                    transforms[i];

                if (t == null)
                    continue;

                string path =
                    BuildRelativePath(
                        prop.transform,
                        t);

                snapshot.State[
                    "TRANSFORM|" + path] =
                        CreateTransformSignature(
                            t);
            }

            Renderer[] renderers =
                prop.GetComponentsInChildren<Renderer>(
                    true);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null)
                    continue;

                string path =
                    BuildRelativePath(
                        prop.transform,
                        renderer.transform);

                snapshot.State[
                    "RENDERER|" +
                    path +
                    "|" +
                    renderer.GetType().FullName] =
                        CreateRendererSignature(
                            renderer);
            }

            MonoBehaviour[] behaviours =
                prop.GetComponentsInChildren<MonoBehaviour>(
                    true);

            for (int i = 0;
                 i < behaviours.Length;
                 i++)
            {
                MonoBehaviour behaviour =
                    behaviours[i];

                if (behaviour == null)
                    continue;

                string path =
                    BuildRelativePath(
                        prop.transform,
                        behaviour.transform);

                string key =
                    "MODULE|" +
                    path +
                    "|" +
                    behaviour.GetType().FullName +
                    "|" +
                    i.ToString(
                        CultureInfo.InvariantCulture);

                snapshot.State[key] =
                    CreateBehaviourSignature(
                        behaviour);
            }

            return snapshot;
        }

        private static string CreateTransformSignature(
            Transform transform)
        {
            if (transform == null)
                return "NULL";

            Vector3 p =
                transform.localPosition;

            Quaternion r =
                transform.localRotation;

            Vector3 s =
                transform.localScale;

            return
                "activeSelf=" +
                Bool01(
                    transform.gameObject.activeSelf) +
                ";activeHierarchy=" +
                Bool01(
                    transform.gameObject.activeInHierarchy) +
                ";localPosition=" +
                Vec3(
                    p) +
                ";localRotation=" +
                Quat(
                    r) +
                ";localScale=" +
                Vec3(
                    s);
        }

        private static string CreateRendererSignature(
            Renderer renderer)
        {
            StringBuilder sb =
                new StringBuilder();

            sb.Append(
                "enabled=");

            sb.Append(
                Bool01(
                    renderer.enabled));

            sb.Append(
                ";activeSelf=");

            sb.Append(
                Bool01(
                    renderer.gameObject.activeSelf));

            sb.Append(
                ";activeHierarchy=");

            sb.Append(
                Bool01(
                    renderer.gameObject.activeInHierarchy));

            Material[] materials =
                renderer.sharedMaterials;

            sb.Append(
                ";sharedMaterials=");

            sb.Append(
                materials != null
                    ? materials.Length
                    : 0);

            if (materials != null)
            {
                for (int i = 0;
                     i < materials.Length;
                     i++)
                {
                    Material material =
                        materials[i];

                    sb.Append(
                        ";m");

                    sb.Append(
                        i.ToString(
                            CultureInfo.InvariantCulture));

                    sb.Append(
                        "=");

                    if (material == null)
                    {
                        sb.Append(
                            "<null>");

                        continue;
                    }

                    sb.Append(
                        material.name);

                    try
                    {
                        if (material.HasProperty(
                            "_EmissionColor"))
                        {
                            Color c =
                                material.GetColor(
                                    "_EmissionColor");

                            sb.Append(
                                "/emission=");

                            sb.Append(
                                ColorText(
                                    c));
                        }
                    }
                    catch
                    {
                        // Diagnostics must never disturb IVA behavior.
                    }
                }
            }

            return sb.ToString();
        }

        private static string CreateBehaviourSignature(
            MonoBehaviour behaviour)
        {
            StringBuilder sb =
                new StringBuilder();

            sb.Append(
                "enabled=");

            sb.Append(
                Bool01(
                    behaviour.enabled));

            Type type =
                behaviour.GetType();

            for (int i = 0;
                 i < InterestingMemberNames.Length;
                 i++)
            {
                string name =
                    InterestingMemberNames[i];

                string value =
                    GetMemberText(
                        behaviour,
                        name);

                if (string.IsNullOrEmpty(
                    value))
                {
                    continue;
                }

                sb.Append(
                    ";");

                sb.Append(
                    name);

                sb.Append(
                    "=");

                sb.Append(
                    value.Replace(
                        "\r",
                        " ")
                    .Replace(
                        "\n",
                        " "));
            }

            return sb.ToString();
        }

        private static string GetMemberText(
            object instance,
            string memberName)
        {
            if (instance == null ||
                string.IsNullOrEmpty(
                    memberName))
            {
                return string.Empty;
            }

            Type type =
                instance.GetType();

            try
            {
                FieldInfo field =
                    type.GetField(
                        memberName,
                        DiagnosticBindingFlags);

                if (field != null)
                {
                    object value =
                        field.GetValue(
                            instance);

                    return SafeToString(
                        value);
                }
            }
            catch
            {
            }

            try
            {
                PropertyInfo property =
                    type.GetProperty(
                        memberName,
                        DiagnosticBindingFlags);

                if (property != null &&
                    property.CanRead &&
                    property.GetIndexParameters().Length == 0)
                {
                    object value =
                        property.GetValue(
                            instance,
                            null);

                    return SafeToString(
                        value);
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string SafeToString(
            object value)
        {
            if (value == null)
                return string.Empty;

            try
            {
                return Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture) ??
                    string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string CreateComparisonReport(
            Snapshot powered,
            Snapshot current)
        {
            StringBuilder sb =
                CreateHeader(
                    "IVA POWERED VS CURRENT COMPARISON",
                    powered,
                    current);

            int changedProps =
                0;

            int changedStates =
                0;

            SortedSet<int> ids =
                new SortedSet<int>();

            foreach (int id in
                     powered.Props.Keys)
            {
                ids.Add(id);
            }

            foreach (int id in
                     current.Props.Keys)
            {
                ids.Add(id);
            }

            foreach (int id in ids)
            {
                PropSnapshot before;
                PropSnapshot after;

                bool hadBefore =
                    powered.Props.TryGetValue(
                        id,
                        out before);

                bool hasAfter =
                    current.Props.TryGetValue(
                        id,
                        out after);

                if (!hadBefore ||
                    !hasAfter)
                {
                    changedProps++;

                    sb.AppendLine();
                    sb.AppendLine(
                        !hadBefore
                            ? "PROP ADDED IN CURRENT"
                            : "PROP MISSING IN CURRENT");

                    AppendPropIdentity(
                        sb,
                        hadBefore
                            ? before
                            : after);

                    continue;
                }

                List<StateDiff> diffs =
                    CompareProp(
                        before,
                        after);

                if (diffs.Count == 0)
                    continue;

                changedProps++;
                changedStates +=
                    diffs.Count;

                sb.AppendLine();
                sb.AppendLine(
                    "============================================================");

                AppendPropIdentity(
                    sb,
                    after);

                sb.AppendLine(
                    "CHANGED STATES: " +
                    diffs.Count);

                for (int i = 0;
                     i < diffs.Count;
                     i++)
                {
                    StateDiff diff =
                        diffs[i];

                    sb.AppendLine(
                        "  [" +
                        diff.Key +
                        "]");

                    sb.AppendLine(
                        "    POWERED: " +
                        diff.Before);

                    sb.AppendLine(
                        "    CURRENT: " +
                        diff.After);
                }
            }

            sb.AppendLine();
            sb.AppendLine(
                "============================================================");

            sb.AppendLine(
                "SUMMARY");

            sb.AppendLine(
                "Changed props: " +
                changedProps);

            sb.AppendLine(
                "Changed state entries: " +
                changedStates);

            sb.AppendLine();
            sb.AppendLine(
                "Interpretation note:");

            sb.AppendLine(
                "This report records observed runtime differences only. " +
                "It does not claim that every difference is caused by power.");

            return sb.ToString();
        }

        private static List<StateDiff> CompareProp(
            PropSnapshot before,
            PropSnapshot after)
        {
            List<StateDiff> diffs =
                new List<StateDiff>();

            SortedSet<string> keys =
                new SortedSet<string>(
                    StringComparer.Ordinal);

            foreach (string key in
                     before.State.Keys)
            {
                keys.Add(key);
            }

            foreach (string key in
                     after.State.Keys)
            {
                keys.Add(key);
            }

            foreach (string key in keys)
            {
                string beforeValue;
                string afterValue;

                bool hadBefore =
                    before.State.TryGetValue(
                        key,
                        out beforeValue);

                bool hasAfter =
                    after.State.TryGetValue(
                        key,
                        out afterValue);

                if (!hadBefore)
                    beforeValue = "<missing>";

                if (!hasAfter)
                    afterValue = "<missing>";

                if (string.Equals(
                    beforeValue,
                    afterValue,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                diffs.Add(
                    new StateDiff
                    {
                        Key =
                            key,

                        Before =
                            beforeValue,

                        After =
                            afterValue
                    });
            }

            return diffs;
        }

        private static string CreateFullDump(
            Snapshot snapshot)
        {
            StringBuilder sb =
                CreateHeader(
                    "IVA FULL CURRENT RUNTIME DUMP",
                    snapshot,
                    null);

            List<int> ids =
                new List<int>(
                    snapshot.Props.Keys);

            ids.Sort();

            for (int i = 0;
                 i < ids.Count;
                 i++)
            {
                PropSnapshot prop =
                    snapshot.Props[
                        ids[i]];

                sb.AppendLine();
                sb.AppendLine(
                    "============================================================");

                AppendPropIdentity(
                    sb,
                    prop);

                List<string> keys =
                    new List<string>(
                        prop.State.Keys);

                keys.Sort(
                    StringComparer.Ordinal);

                for (int k = 0;
                     k < keys.Count;
                     k++)
                {
                    string key =
                        keys[k];

                    sb.AppendLine(
                        "  [" +
                        key +
                        "] " +
                        prop.State[key]);
                }
            }

            return sb.ToString();
        }

        private static StringBuilder CreateHeader(
            string title,
            Snapshot primary,
            Snapshot secondary)
        {
            StringBuilder sb =
                new StringBuilder();

            sb.AppendLine(
                "KMC IVA STATE INSPECTOR");

            sb.AppendLine(
                title);

            sb.AppendLine(
                "Generated UTC: " +
                DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture));

            if (primary != null)
            {
                sb.AppendLine(
                    "Primary snapshot: " +
                    primary.Label);

                sb.AppendLine(
                    "Primary UTC: " +
                    primary.TimestampUtc.ToString(
                        "O",
                        CultureInfo.InvariantCulture));

                sb.AppendLine(
                    "Vessel: " +
                    primary.VesselName);

                sb.AppendLine(
                    "Internal root: " +
                    primary.InternalName);

                sb.AppendLine(
                    "Props: " +
                    primary.Props.Count);
            }

            if (secondary != null)
            {
                sb.AppendLine(
                    "Secondary snapshot: " +
                    secondary.Label);

                sb.AppendLine(
                    "Secondary UTC: " +
                    secondary.TimestampUtc.ToString(
                        "O",
                        CultureInfo.InvariantCulture));

                sb.AppendLine(
                    "Secondary props: " +
                    secondary.Props.Count);
            }

            return sb;
        }

        private static void AppendPropIdentity(
            StringBuilder sb,
            PropSnapshot prop)
        {
            if (prop == null)
                return;

            sb.AppendLine(
                "PROP INSTANCE: " +
                prop.InstanceId);

            sb.AppendLine(
                "PROP NAME: " +
                prop.PropName);

            sb.AppendLine(
                "PROP ID: " +
                prop.PropId);

            sb.AppendLine(
                "GAMEOBJECT: " +
                prop.GameObjectName);

            sb.AppendLine(
                "INTERNAL ROOT: " +
                prop.InternalRoot);

            sb.AppendLine(
                "TRANSFORM PATH: " +
                prop.TransformPath);
        }

        private static string WriteReport(
            string prefix,
            string contents)
        {
            string root =
                KSPUtil.ApplicationRootPath;

            string folder =
                Path.Combine(
                    root,
                    "KMC_IvaInspector");

            Directory.CreateDirectory(
                folder);

            string filename =
                "KMC_IVA_" +
                SanitizeFileName(
                    prefix) +
                "_" +
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss",
                    CultureInfo.InvariantCulture) +
                ".txt";

            string path =
                Path.Combine(
                    folder,
                    filename);

            File.WriteAllText(
                path,
                contents ?? string.Empty,
                Encoding.UTF8);

            Debug.Log(
                "[KMC IVA INSPECTOR] Report written: " +
                path);

            return path;
        }

        private static string SanitizeFileName(
            string text)
        {
            if (string.IsNullOrEmpty(
                text))
            {
                return "REPORT";
            }

            char[] invalid =
                Path.GetInvalidFileNameChars();

            StringBuilder sb =
                new StringBuilder(
                    text.Length);

            for (int i = 0;
                 i < text.Length;
                 i++)
            {
                char c =
                    text[i];

                bool bad =
                    false;

                for (int j = 0;
                     j < invalid.Length;
                     j++)
                {
                    if (c == invalid[j])
                    {
                        bad =
                            true;

                        break;
                    }
                }

                sb.Append(
                    bad
                        ? '_'
                        : c);
            }

            return sb.ToString();
        }

        private static string FindInternalRootName(
            Transform start)
        {
            Transform cursor =
                start;

            while (cursor != null)
            {
                Component[] components =
                    cursor.GetComponents<Component>();

                for (int i = 0;
                     i < components.Length;
                     i++)
                {
                    Component component =
                        components[i];

                    if (component == null)
                        continue;

                    if (string.Equals(
                        component.GetType().Name,
                        "InternalModel",
                        StringComparison.Ordinal))
                    {
                        string internalName =
                            GetMemberText(
                                component,
                                "internalName");

                        if (!string.IsNullOrEmpty(
                            internalName))
                        {
                            return internalName;
                        }

                        return cursor.gameObject.name;
                    }
                }

                cursor =
                    cursor.parent;
            }

            return string.Empty;
        }

        private static string BuildTransformPath(
            Transform transform)
        {
            if (transform == null)
                return string.Empty;

            List<string> names =
                new List<string>();

            Transform cursor =
                transform;

            while (cursor != null)
            {
                names.Add(
                    cursor.name);

                cursor =
                    cursor.parent;
            }

            names.Reverse();

            return string.Join(
                "/",
                names.ToArray());
        }

        private static string BuildRelativePath(
            Transform root,
            Transform transform)
        {
            if (transform == null)
                return string.Empty;

            if (root == null)
            {
                return BuildTransformPath(
                    transform);
            }

            if (transform == root)
                return ".";

            List<string> names =
                new List<string>();

            Transform cursor =
                transform;

            while (cursor != null &&
                   cursor != root)
            {
                names.Add(
                    cursor.name);

                cursor =
                    cursor.parent;
            }

            if (cursor != root)
            {
                return BuildTransformPath(
                    transform);
            }

            names.Reverse();

            return string.Join(
                "/",
                names.ToArray());
        }

        private static string DescribeSnapshot(
            Snapshot snapshot)
        {
            if (snapshot == null)
                return "<none>";

            return
                snapshot.Props.Count +
                " props / " +
                snapshot.InternalName +
                " / " +
                snapshot.TimestampUtc.ToString(
                    "HH:mm:ss",
                    CultureInfo.InvariantCulture) +
                " UTC";
        }

        private static string Bool01(
            bool value)
        {
            return value
                ? "1"
                : "0";
        }

        private static string Vec3(
            Vector3 value)
        {
            return
                value.x.ToString(
                    "R",
                    CultureInfo.InvariantCulture) +
                "," +
                value.y.ToString(
                    "R",
                    CultureInfo.InvariantCulture) +
                "," +
                value.z.ToString(
                    "R",
                    CultureInfo.InvariantCulture);
        }

        private static string Quat(
            Quaternion value)
        {
            return
                value.x.ToString(
                    "R",
                    CultureInfo.InvariantCulture) +
                "," +
                value.y.ToString(
                    "R",
                    CultureInfo.InvariantCulture) +
                "," +
                value.z.ToString(
                    "R",
                    CultureInfo.InvariantCulture) +
                "," +
                value.w.ToString(
                    "R",
                    CultureInfo.InvariantCulture);
        }

        private static string ColorText(
            Color value)
        {
            return
                value.r.ToString(
                    "R",
                    CultureInfo.InvariantCulture) +
                "," +
                value.g.ToString(
                    "R",
                    CultureInfo.InvariantCulture) +
                "," +
                value.b.ToString(
                    "R",
                    CultureInfo.InvariantCulture) +
                "," +
                value.a.ToString(
                    "R",
                    CultureInfo.InvariantCulture);
        }

        private void HandleError(
            string context,
            Exception ex)
        {
            _status =
                context.ToUpperInvariant();

            Debug.LogError(
                "[KMC IVA INSPECTOR] " +
                context +
                ": " +
                ex);
        }

        private sealed class Snapshot
        {
            public string Label =
                string.Empty;

            public DateTime TimestampUtc;

            public string VesselName =
                string.Empty;

            public string InternalName =
                string.Empty;

            public readonly Dictionary<int, PropSnapshot>
                Props =
                    new Dictionary<int, PropSnapshot>();
        }

        private sealed class PropSnapshot
        {
            public int InstanceId;

            public string PropName =
                string.Empty;

            public string PropId =
                string.Empty;

            public string GameObjectName =
                string.Empty;

            public string TransformPath =
                string.Empty;

            public string InternalRoot =
                string.Empty;

            public readonly Dictionary<string, string>
                State =
                    new Dictionary<string, string>(
                        StringComparer.Ordinal);
        }

        private sealed class StateDiff
        {
            public string Key =
                string.Empty;

            public string Before =
                string.Empty;

            public string After =
                string.Empty;
        }
    }
}
