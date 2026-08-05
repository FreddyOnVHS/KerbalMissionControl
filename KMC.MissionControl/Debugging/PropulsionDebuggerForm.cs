using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using KMC.MissionControl.Rendering.Propulsion;
using KMC.Shared;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Debugging
{
    public sealed class PropulsionDebuggerForm :
        Form
    {
        private readonly TabControl _tabs;
        private readonly TextBox _overview;
        private readonly DataGridView _engines;
        private readonly DataGridView _resources;
        private readonly TextBox _raw;
        private readonly CheckBox _autoRefresh;
        private readonly Timer _timer;

        public PropulsionDebuggerForm()
        {
            Text =
                "KMC Propulsion Debugger";

            StartPosition =
                FormStartPosition.CenterParent;

            Size =
                new Size(
                    1250,
                    820);

            MinimumSize =
                new Size(
                    900,
                    600);

            BackColor =
                Color.FromArgb(
                    4,
                    14,
                    18);

            ForeColor =
                Color.FromArgb(
                    170,
                    255,
                    190);

            Font =
                new Font(
                    "Consolas",
                    10.0f);

            KeyPreview =
                true;

            _tabs =
                new TabControl
                {
                    Dock =
                        DockStyle.Fill
                };

            _overview =
                CreateTextView();

            _engines =
                CreateGrid();

            _resources =
                CreateGrid();

            _raw =
                CreateTextView();

            _tabs.TabPages.Add(
                CreatePage(
                    "OVERVIEW",
                    _overview));

            _tabs.TabPages.Add(
                CreatePage(
                    "ENGINES",
                    _engines));

            _tabs.TabPages.Add(
                CreatePage(
                    "RESOURCES",
                    _resources));

            _tabs.TabPages.Add(
                CreatePage(
                    "RAW SNAPSHOT",
                    _raw));

            FlowLayoutPanel commands =
                new FlowLayoutPanel
                {
                    Dock =
                        DockStyle.Top,
                    Height =
                        42,
                    FlowDirection =
                        FlowDirection.LeftToRight,
                    Padding =
                        new Padding(
                            6),
                    BackColor =
                        Color.FromArgb(
                            24,
                            34,
                            34)
                };

            Button refresh =
                CreateButton(
                    "REFRESH");

            refresh.Click +=
                delegate
                {
                    RefreshSnapshot();
                };

            Button copy =
                CreateButton(
                    "COPY RAW");

            copy.Click +=
                delegate
                {
                    if (!string.IsNullOrEmpty(
                            _raw.Text))
                    {
                        Clipboard.SetText(
                            _raw.Text);
                    }
                };

            Button save =
                CreateButton(
                    "SAVE SNAPSHOT");

            save.Click +=
                OnSaveSnapshot;

            _autoRefresh =
                new CheckBox
                {
                    Text =
                        "AUTO REFRESH",
                    AutoSize =
                        true,
                    Checked =
                        true,
                    ForeColor =
                        ForeColor,
                    Padding =
                        new Padding(
                            8,
                            5,
                            0,
                            0)
                };

            commands.Controls.Add(
                refresh);

            commands.Controls.Add(
                copy);

            commands.Controls.Add(
                save);

            commands.Controls.Add(
                _autoRefresh);

            Controls.Add(
                _tabs);

            Controls.Add(
                commands);

            _timer =
                new Timer
                {
                    Interval =
                        500
                };

            _timer.Tick +=
                delegate
                {
                    if (_autoRefresh.Checked)
                    {
                        RefreshSnapshot();
                    }
                };

            _timer.Start();

            FormClosed +=
                delegate
                {
                    _timer.Stop();
                    _timer.Dispose();
                };

            KeyDown +=
                delegate(
                    object sender,
                    KeyEventArgs e)
                {
                    if (e.KeyCode ==
                        Keys.Escape)
                    {
                        Close();
                    }
                };
        }

        public void RefreshSnapshot()
        {
            TelemetryPacket telemetry =
                PropulsionDebugSnapshotStore
                    .GetTelemetry();

            VesselTopology topology =
                PropulsionDebugSnapshotStore
                    .GetTopology();

            PropulsionRenderGraph graph =
                PropulsionGraphStore
                    .GetCurrent();

            PropulsionAnalysis analysis =
                graph != null
                    ? PropulsionAnalysisCache
                        .GetOrBuild(graph)
                    : null;

            _overview.Text =
                BuildOverview(
                    telemetry,
                    topology,
                    graph,
                    analysis);

            PopulateEngines(
                topology,
                graph,
                analysis);

            PopulateResources(
                telemetry,
                topology);

            _raw.Text =
                BuildRawSnapshot(
                    telemetry,
                    topology,
                    graph,
                    analysis);
        }

        private static string BuildOverview(
            TelemetryPacket telemetry,
            VesselTopology topology,
            PropulsionRenderGraph graph,
            PropulsionAnalysis analysis)
        {
            int topologyEngines =
                CountTopologyCategory(
                    topology,
                    VesselNodeCategory.Engine);

            int topologyBoosters =
                CountTopologyCategory(
                    topology,
                    VesselNodeCategory.SolidBooster);

            int graphEngines =
                CountGraphCategory(
                    graph,
                    VesselNodeCategory.Engine);

            int graphBoosters =
                CountGraphCategory(
                    graph,
                    VesselNodeCategory.SolidBooster);

            int groups =
                analysis != null &&
                analysis.SystemModel != null
                    ? analysis.SystemModel
                        .EngineGroups.Count
                    : 0;

            int projectionPoints =
                analysis != null &&
                analysis.EngineCluster != null &&
                analysis.EngineCluster.Engines != null
                    ? analysis.EngineCluster.Engines.Count
                    : 0;

            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine(
                "KMC PROPULSION DEBUGGER");
            builder.AppendLine(
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine();

            builder.AppendLine("TELEMETRY");
            Append(builder, "Vessel",
                telemetry != null ? telemetry.VesselName : "--");
            Append(builder, "Current stage",
                telemetry != null ? telemetry.CurrentStage.ToString() : "--");
            Append(builder, "Engine count",
                telemetry != null ? telemetry.EngineCount.ToString() : "--");
            Append(builder, "Ignited",
                telemetry != null ? telemetry.IgnitedEngineCount.ToString() : "--");
            Append(builder, "Producing",
                telemetry != null ? telemetry.ProducingThrustEngineCount.ToString() : "--");
            Append(builder, "Flameout",
                telemetry != null ? telemetry.FlameoutEngineCount.ToString() : "--");
            builder.AppendLine();

            builder.AppendLine("TOPOLOGY");
            Append(builder, "Revision",
                topology != null ? topology.Revision.ToString() : "--");
            Append(builder, "Nodes",
                topology != null ? topology.Nodes.Count.ToString() : "--");
            Append(builder, "Liquid engines",
                topologyEngines.ToString());
            Append(builder, "Solid boosters",
                topologyBoosters.ToString());
            Append(builder, "RCS nodes",
                CountTopologyCategory(
                    topology,
                    VesselNodeCategory.RcsThruster).ToString());
            builder.AppendLine();

            builder.AppendLine("RENDER GRAPH / ANALYSIS");
            Append(builder, "Graph revision",
                graph != null ? graph.TopologyRevision.ToString() : "--");
            Append(builder, "Graph nodes",
                graph != null ? graph.Nodes.Count.ToString() : "--");
            Append(builder, "Liquid engine nodes",
                graphEngines.ToString());
            Append(builder, "Solid booster nodes",
                graphBoosters.ToString());
            Append(builder, "Engine groups",
                groups.ToString());
            Append(builder, "Projection points",
                projectionPoints.ToString());
            builder.AppendLine();

            builder.AppendLine("RESOURCE TELEMETRY");
            AppendResource(
                builder,
                "LIQUID FUEL",
                telemetry != null ? telemetry.StageLiquidFuelAmount : 0.0,
                telemetry != null ? telemetry.StageLiquidFuelCapacity : 0.0,
                telemetry != null ? telemetry.TotalLiquidFuelAmount : 0.0,
                telemetry != null ? telemetry.TotalLiquidFuelCapacity : 0.0);
            AppendResource(
                builder,
                "OXIDIZER",
                telemetry != null ? telemetry.StageOxidizerAmount : 0.0,
                telemetry != null ? telemetry.StageOxidizerCapacity : 0.0,
                telemetry != null ? telemetry.TotalOxidizerAmount : 0.0,
                telemetry != null ? telemetry.TotalOxidizerCapacity : 0.0);
            AppendResource(
                builder,
                "MONOPROPELLANT",
                telemetry != null ? telemetry.StageMonopropellantAmount : 0.0,
                telemetry != null ? telemetry.StageMonopropellantCapacity : 0.0,
                telemetry != null ? telemetry.TotalMonopropellantAmount : 0.0,
                telemetry != null ? telemetry.TotalMonopropellantCapacity : 0.0);

            builder.AppendLine();
            builder.AppendLine("AUTOMATIC CHECKS");

            AppendCheck(
                builder,
                "Telemetry engines vs topology propulsion nodes",
                telemetry == null
                    ? null
                    : (bool?)(telemetry.EngineCount ==
                        topologyEngines +
                        topologyBoosters),
                telemetry == null
                    ? "NO TELEMETRY"
                    : telemetry.EngineCount +
                      " vs " +
                      (topologyEngines + topologyBoosters));

            AppendCheck(
                builder,
                "Graph retains all solid boosters",
                topology == null || graph == null
                    ? null
                    : (bool?)(topologyBoosters ==
                        graphBoosters),
                topologyBoosters +
                " vs " +
                graphBoosters);

            AppendCheck(
                builder,
                "Projection represents propulsion nodes",
                analysis == null
                    ? null
                    : (bool?)(projectionPoints >=
                        graphEngines +
                        graphBoosters),
                projectionPoints +
                " vs " +
                (graphEngines + graphBoosters));

            AppendCheck(
                builder,
                "Mono ACTIVE has positive capacity",
                telemetry == null
                    ? null
                    : (bool?)(telemetry.StageMonopropellantAmount <= 0.0 ||
                              telemetry.StageMonopropellantCapacity > 0.0),
                FormatPair(
                    telemetry != null ? telemetry.StageMonopropellantAmount : 0.0,
                    telemetry != null ? telemetry.StageMonopropellantCapacity : 0.0));

            return builder.ToString();
        }

        private void PopulateEngines(
            VesselTopology topology,
            PropulsionRenderGraph graph,
            PropulsionAnalysis analysis)
        {
            _engines.Columns.Clear();
            _engines.Rows.Clear();

            AddColumn(_engines, "SOURCE");
            AddColumn(_engines, "PART ID");
            AddColumn(_engines, "TITLE");
            AddColumn(_engines, "CATEGORY");
            AddColumn(_engines, "ACT STAGE");
            AddColumn(_engines, "SEP STAGE");
            AddColumn(_engines, "PROPELLANTS");
            AddColumn(_engines, "SOURCES");

            if (topology != null)
            {
                for (int index = 0;
                     index < topology.Nodes.Count;
                     index++)
                {
                    VesselTopologyNode node =
                        topology.Nodes[index];

                    if (node == null ||
                        (node.Category != VesselNodeCategory.Engine &&
                         node.Category != VesselNodeCategory.SolidBooster &&
                         node.Category != VesselNodeCategory.RcsThruster))
                    {
                        continue;
                    }

                    _engines.Rows.Add(
                        "TOPOLOGY",
                        node.PartId,
                        node.PartTitle,
                        node.Category,
                        node.ActivationStage,
                        node.SeparationStage,
                        JoinRequirements(node),
                        JoinSources(node));
                }
            }

            if (graph != null)
            {
                for (int index = 0;
                     index < graph.Nodes.Count;
                     index++)
                {
                    PropulsionGraphNode node =
                        graph.Nodes[index];

                    if (node.Category != VesselNodeCategory.Engine &&
                        node.Category != VesselNodeCategory.SolidBooster &&
                        node.Category != VesselNodeCategory.RcsThruster)
                    {
                        continue;
                    }

                    _engines.Rows.Add(
                        "GRAPH",
                        node.PartId,
                        node.Title,
                        node.Category,
                        node.ActivationStage,
                        node.SeparationStage,
                        string.Join(", ", node.PropellantNames.ToArray()),
                        JoinUInts(node.SourcePartIds));
                }
            }
        }

        private void PopulateResources(
            TelemetryPacket telemetry,
            VesselTopology topology)
        {
            _resources.Columns.Clear();
            _resources.Rows.Clear();

            AddColumn(_resources, "PART ID");
            AddColumn(_resources, "PART");
            AddColumn(_resources, "CATEGORY");
            AddColumn(_resources, "RESOURCE");
            AddColumn(_resources, "AMOUNT");
            AddColumn(_resources, "CAPACITY");
            AddColumn(_resources, "FLOW");
            AddColumn(_resources, "CROSSFEED");

            if (topology == null)
            {
                return;
            }

            for (int nodeIndex = 0;
                 nodeIndex < topology.Nodes.Count;
                 nodeIndex++)
            {
                VesselTopologyNode node =
                    topology.Nodes[nodeIndex];

                if (node == null)
                {
                    continue;
                }

                for (int resourceIndex = 0;
                     resourceIndex < node.Resources.Count;
                     resourceIndex++)
                {
                    VesselResourceState resource =
                        node.Resources[resourceIndex];

                    _resources.Rows.Add(
                        node.PartId,
                        node.PartTitle,
                        node.Category,
                        resource.Name,
                        resource.Amount.ToString("0.###"),
                        resource.Capacity.ToString("0.###"),
                        resource.FlowEnabled,
                        node.AllowsCrossFeed);
                }
            }
        }

        private static string BuildRawSnapshot(
            object telemetry,
            object topology,
            object graph,
            object analysis)
        {
            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine(
                "KMC PROPULSION DEBUG SNAPSHOT");
            builder.AppendLine(
                DateTime.Now.ToString("O"));
            builder.AppendLine();

            DumpObject(
                builder,
                "TELEMETRY",
                telemetry,
                0,
                new HashSet<object>(
                    ReferenceEqualityComparer.Instance));

            DumpObject(
                builder,
                "TOPOLOGY",
                topology,
                0,
                new HashSet<object>(
                    ReferenceEqualityComparer.Instance));

            DumpObject(
                builder,
                "RENDER GRAPH",
                graph,
                0,
                new HashSet<object>(
                    ReferenceEqualityComparer.Instance));

            DumpObject(
                builder,
                "PROPULSION ANALYSIS",
                analysis,
                0,
                new HashSet<object>(
                    ReferenceEqualityComparer.Instance));

            DumpObject(
                builder,
                "CACHE",
                PropulsionAnalysisCache.GetSnapshot(),
                0,
                new HashSet<object>(
                    ReferenceEqualityComparer.Instance));

            return builder.ToString();
        }

        private static void DumpObject(
            StringBuilder builder,
            string name,
            object value,
            int depth,
            ISet<object> visited)
        {
            string indent =
                new string(
                    ' ',
                    depth * 2);

            if (depth > 6)
            {
                builder.AppendLine(
                    indent + name + ": <MAX DEPTH>");
                return;
            }

            if (value == null)
            {
                builder.AppendLine(
                    indent + name + ": <NULL>");
                return;
            }

            Type type =
                value.GetType();

            if (IsSimple(type))
            {
                builder.AppendLine(
                    indent + name + ": " + value);
                return;
            }

            if (!type.IsValueType &&
                !visited.Add(value))
            {
                builder.AppendLine(
                    indent + name + ": <ALREADY SHOWN>");
                return;
            }

            IEnumerable enumerable =
                value as IEnumerable;

            if (enumerable != null &&
                !(value is string))
            {
                builder.AppendLine(
                    indent + name + ":");

                int index =
                    0;

                foreach (object item in enumerable)
                {
                    DumpObject(
                        builder,
                        "[" + index + "]",
                        item,
                        depth + 1,
                        visited);

                    index++;

                    if (index >= 500)
                    {
                        builder.AppendLine(
                            indent + "  <TRUNCATED>");
                        break;
                    }
                }

                return;
            }

            builder.AppendLine(
                indent + name + " (" + type.Name + "):");

            PropertyInfo[] properties =
                type.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public);

            Array.Sort(
                properties,
                delegate(
                    PropertyInfo left,
                    PropertyInfo right)
                {
                    return string.Compare(
                        left.Name,
                        right.Name,
                        StringComparison.Ordinal);
                });

            for (int index = 0;
                 index < properties.Length;
                 index++)
            {
                PropertyInfo property =
                    properties[index];

                if (!property.CanRead ||
                    property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                object propertyValue;

                try
                {
                    propertyValue =
                        property.GetValue(
                            value,
                            null);
                }
                catch (Exception ex)
                {
                    propertyValue =
                        "<ERROR " +
                        ex.GetType().Name +
                        ">";
                }

                DumpObject(
                    builder,
                    property.Name,
                    propertyValue,
                    depth + 1,
                    visited);
            }

            FieldInfo[] fields =
                type.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public);

            for (int index = 0;
                 index < fields.Length;
                 index++)
            {
                FieldInfo field =
                    fields[index];

                DumpObject(
                    builder,
                    field.Name,
                    field.GetValue(value),
                    depth + 1,
                    visited);
            }
        }

        private static bool IsSimple(
            Type type)
        {
            return
                type.IsPrimitive ||
                type.IsEnum ||
                type == typeof(string) ||
                type == typeof(decimal) ||
                type == typeof(DateTime) ||
                type == typeof(TimeSpan) ||
                type == typeof(Guid);
        }

        private static int CountTopologyCategory(
            VesselTopology topology,
            VesselNodeCategory category)
        {
            int count =
                0;

            if (topology == null)
            {
                return count;
            }

            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (node != null &&
                    node.Category == category)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountGraphCategory(
            PropulsionRenderGraph graph,
            VesselNodeCategory category)
        {
            int count =
                0;

            if (graph == null)
            {
                return count;
            }

            for (int index = 0;
                 index < graph.Nodes.Count;
                 index++)
            {
                if (graph.Nodes[index].Category ==
                    category)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Append(
            StringBuilder builder,
            string label,
            string value)
        {
            builder.Append(
                label.PadRight(30));

            builder.AppendLine(
                value ?? "--");
        }

        private static void AppendResource(
            StringBuilder builder,
            string label,
            double activeAmount,
            double activeCapacity,
            double totalAmount,
            double totalCapacity)
        {
            builder.Append(
                label.PadRight(20));

            builder.Append(
                "ACTIVE ");

            builder.Append(
                FormatPair(
                    activeAmount,
                    activeCapacity));

            builder.Append(
                "    TOTAL ");

            builder.AppendLine(
                FormatPair(
                    totalAmount,
                    totalCapacity));
        }

        private static string FormatPair(
            double amount,
            double capacity)
        {
            return amount.ToString("0.###") +
                " / " +
                capacity.ToString("0.###");
        }

        private static void AppendCheck(
            StringBuilder builder,
            string label,
            bool? passed,
            string detail)
        {
            string state =
                !passed.HasValue
                    ? "UNKNOWN"
                    : passed.Value
                        ? "PASS"
                        : "FAIL";

            builder.Append(
                ("[" + state + "]").PadRight(12));

            builder.Append(
                label);

            builder.Append(
                "  ");

            builder.AppendLine(
                detail);
        }

        private static string JoinRequirements(
            VesselTopologyNode node)
        {
            List<string> result =
                new List<string>();

            for (int index = 0;
                 index < node.PropellantRequirements.Count;
                 index++)
            {
                result.Add(
                    node.PropellantRequirements[index].Name);
            }

            return string.Join(
                ", ",
                result.ToArray());
        }

        private static string JoinSources(
            VesselTopologyNode node)
        {
            List<uint> result =
                new List<uint>();

            for (int requirementIndex = 0;
                 requirementIndex <
                    node.PropellantRequirements.Count;
                 requirementIndex++)
            {
                VesselPropellantRequirement requirement =
                    node.PropellantRequirements[
                        requirementIndex];

                for (int sourceIndex = 0;
                     sourceIndex <
                        requirement.ReachableSourcePartIds.Count;
                     sourceIndex++)
                {
                    uint id =
                        requirement.ReachableSourcePartIds[
                            sourceIndex];

                    if (!result.Contains(id))
                    {
                        result.Add(id);
                    }
                }
            }

            return JoinUInts(result);
        }

        private static string JoinUInts(
            IList<uint> values)
        {
            string[] result =
                new string[values.Count];

            for (int index = 0;
                 index < values.Count;
                 index++)
            {
                result[index] =
                    values[index].ToString();
            }

            return string.Join(
                ", ",
                result);
        }

        private static TextBox CreateTextView()
        {
            return new TextBox
            {
                Multiline =
                    true,
                ReadOnly =
                    true,
                ScrollBars =
                    ScrollBars.Both,
                WordWrap =
                    false,
                Dock =
                    DockStyle.Fill,
                BackColor =
                    Color.FromArgb(
                        1,
                        13,
                        17),
                ForeColor =
                    Color.FromArgb(
                        160,
                        255,
                        185),
                Font =
                    new Font(
                        "Consolas",
                        10.0f)
            };
        }

        private static DataGridView CreateGrid()
        {
            DataGridView grid =
                new DataGridView
                {
                    Dock =
                        DockStyle.Fill,
                    ReadOnly =
                        true,
                    AllowUserToAddRows =
                        false,
                    AllowUserToDeleteRows =
                        false,
                    AllowUserToOrderColumns =
                        true,
                    AutoSizeColumnsMode =
                        DataGridViewAutoSizeColumnsMode
                            .DisplayedCells,
                    BackgroundColor =
                        Color.FromArgb(
                            1,
                            13,
                            17),
                    ForeColor =
                        Color.FromArgb(
                            160,
                            255,
                            185),
                    GridColor =
                        Color.FromArgb(
                            45,
                            90,
                            85),
                    RowHeadersVisible =
                        false
                };

            grid.DefaultCellStyle.BackColor =
                grid.BackgroundColor;

            grid.DefaultCellStyle.ForeColor =
                grid.ForeColor;

            grid.DefaultCellStyle.SelectionBackColor =
                Color.FromArgb(
                    30,
                    90,
                    80);

            grid.ColumnHeadersDefaultCellStyle.BackColor =
                Color.FromArgb(
                    24,
                    34,
                    34);

            grid.ColumnHeadersDefaultCellStyle.ForeColor =
                grid.ForeColor;

            grid.EnableHeadersVisualStyles =
                false;

            return grid;
        }

        private static TabPage CreatePage(
            string title,
            Control control)
        {
            TabPage page =
                new TabPage(
                    title);

            page.BackColor =
                Color.FromArgb(
                    1,
                    13,
                    17);

            page.Controls.Add(
                control);

            return page;
        }

        private static Button CreateButton(
            string text)
        {
            return new Button
            {
                Text =
                    text,
                AutoSize =
                    true,
                FlatStyle =
                    FlatStyle.Flat,
                ForeColor =
                    Color.FromArgb(
                        160,
                        255,
                        185),
                BackColor =
                    Color.FromArgb(
                        20,
                        35,
                        34)
            };
        }

        private static void AddColumn(
            DataGridView grid,
            string title)
        {
            grid.Columns.Add(
                title.Replace(
                    " ",
                    string.Empty),
                title);
        }

        private void OnSaveSnapshot(
            object sender,
            EventArgs e)
        {
            using (SaveFileDialog dialog =
                new SaveFileDialog())
            {
                dialog.Title =
                    "Save KMC Propulsion Debug Snapshot";

                dialog.Filter =
                    "Text files (*.txt)|*.txt|All files (*.*)|*.*";

                dialog.FileName =
                    "KMC-Propulsion-Debug-" +
                    DateTime.Now.ToString(
                        "yyyyMMdd-HHmmss") +
                    ".txt";

                if (dialog.ShowDialog(this) ==
                    DialogResult.OK)
                {
                    File.WriteAllText(
                        dialog.FileName,
                        _raw.Text,
                        Encoding.UTF8);
                }
            }
        }

        private sealed class ReferenceEqualityComparer :
            IEqualityComparer<object>
        {
            public static readonly
                ReferenceEqualityComparer Instance =
                    new ReferenceEqualityComparer();

            public new bool Equals(
                object left,
                object right)
            {
                return ReferenceEquals(
                    left,
                    right);
            }

            public int GetHashCode(
                object value)
            {
                return System.Runtime.CompilerServices
                    .RuntimeHelpers
                    .GetHashCode(value);
            }
        }
    }
}
