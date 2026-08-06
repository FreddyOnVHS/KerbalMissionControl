using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using KMC.MissionControl.Debugging;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Debugging.Electrical
{
    public sealed class ElectricalTopologyDebuggerForm :
        Form
    {
        private readonly TabControl _tabs;
        private readonly TextBox _overview;
        private readonly DataGridView _sections;
        private readonly DataGridView _parts;
        private readonly DataGridView _rawTopology;
        private readonly ElectricalStackPreview _preview;
        private readonly CheckBox _autoRefresh;
        private readonly Timer _timer;

        private string _rawSnapshot =
            string.Empty;

        public ElectricalTopologyDebuggerForm()
        {
            Text =
                "KMC Electrical Topology Debugger";

            StartPosition =
                FormStartPosition.CenterParent;

            Size =
                new Size(
                    1380,
                    860);

            MinimumSize =
                new Size(
                    960,
                    640);

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

            _sections =
                CreateGrid();

            _parts =
                CreateGrid();

            _rawTopology =
                CreateGrid();

            _preview =
                new ElectricalStackPreview
                {
                    Dock =
                        DockStyle.Fill
                };

            _tabs.TabPages.Add(
                CreatePage(
                    "OVERVIEW",
                    _overview));

            _tabs.TabPages.Add(
                CreatePage(
                    "SECTIONS",
                    _sections));

            _tabs.TabPages.Add(
                CreatePage(
                    "ELECTRICAL PARTS",
                    _parts));

            _tabs.TabPages.Add(
                CreatePage(
                    "STACK PREVIEW",
                    _preview));

            _tabs.TabPages.Add(
                CreatePage(
                    "RAW TOPOLOGY",
                    _rawTopology));

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
                            _rawSnapshot))
                    {
                        Clipboard.SetText(
                            _rawSnapshot);
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
            VesselTopology topology =
                PropulsionDebugSnapshotStore
                    .GetTopology();

            ElectricalTopologyModel model =
                ElectricalTopologyBuilder.Build(
                    topology);

            _overview.Text =
                BuildOverview(
                    model);

            PopulateSections(
                model);

            PopulateParts(
                model);

            PopulateRawTopology(
                topology,
                model);

            _preview.SetModel(
                model);

            _rawSnapshot =
                BuildRawSnapshot(
                    topology,
                    model);
        }

        private static string BuildOverview(
            ElectricalTopologyModel model)
        {
            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine(
                "KMC ELECTRICAL TOPOLOGY DEBUGGER");

            builder.AppendLine(
                DateTime.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss"));

            builder.AppendLine();

            Append(
                builder,
                "Vessel",
                string.IsNullOrEmpty(
                    model.VesselName)
                    ? "--"
                    : model.VesselName);

            Append(
                builder,
                "Topology revision",
                model.TopologyRevision.ToString());

            Append(
                builder,
                "Current stage",
                model.CurrentStage.ToString());

            Append(
                builder,
                "Generated sections",
                model.Sections.Count.ToString());

            Append(
                builder,
                "Topology parts",
                model.Parts.Count.ToString());

            int electricalParts =
                0;

            double amount =
                0.0;

            double capacity =
                0.0;

            for (int index = 0;
                 index < model.Parts.Count;
                 index++)
            {
                ElectricalPartModel part =
                    model.Parts[index];

                if (part.IsElectricalPart)
                {
                    electricalParts++;
                }

                amount +=
                    part.ElectricChargeAmount;

                capacity +=
                    part.ElectricChargeCapacity;
            }

            Append(
                builder,
                "Electrical parts",
                electricalParts.ToString());

            Append(
                builder,
                "ElectricCharge",
                amount.ToString("0.###") +
                " / " +
                capacity.ToString("0.###"));

            builder.AppendLine();
            builder.AppendLine(
                "GENERATED SECTION ORDER");

            for (int index = 0;
                 index < model.Sections.Count;
                 index++)
            {
                ElectricalSectionModel section =
                    model.Sections[index];

                builder.Append(
                    index.ToString("00"));

                builder.Append(
                    "  ");

                builder.Append(
                    section.Name.PadRight(
                        22));

                builder.Append(
                    " KEY ");

                builder.Append(
                    section.Key.PadRight(
                        18));

                builder.Append(
                    " SEP ");

                builder.Append(
                    FormatStage(
                        section.SeparationStage));

                builder.Append(
                    "  Y ");

                builder.Append(
                    section.AverageY.ToString(
                        "0.000"));

                builder.Append(
                    "  EC ");

                builder.Append(
                    section.ElectricChargePercent
                        .ToString("0"));

                builder.AppendLine(
                    "%");
            }

            builder.AppendLine();
            builder.AppendLine(
                "DIAGNOSTICS");

            for (int index = 0;
                 index < model.Diagnostics.Count;
                 index++)
            {
                builder.Append(
                    "- ");

                builder.AppendLine(
                    model.Diagnostics[index]);
            }

            return builder.ToString();
        }

        private void PopulateSections(
            ElectricalTopologyModel model)
        {
            _sections.Columns.Clear();
            _sections.Rows.Clear();

            AddColumn(
                _sections,
                "ORDER");

            AddColumn(
                _sections,
                "NAME");

            AddColumn(
                _sections,
                "KEY");

            AddColumn(
                _sections,
                "TYPE");

            AddColumn(
                _sections,
                "SEP STAGE");

            AddColumn(
                _sections,
                "ACT STAGE");

            AddColumn(
                _sections,
                "AVG Y");

            AddColumn(
                _sections,
                "PARTS");

            AddColumn(
                _sections,
                "ELEC PARTS");

            AddColumn(
                _sections,
                "EC AMOUNT");

            AddColumn(
                _sections,
                "EC CAP");

            AddColumn(
                _sections,
                "EC %");

            AddColumn(
                _sections,
                "BAT");

            AddColumn(
                _sections,
                "SOLAR");

            AddColumn(
                _sections,
                "GEN");

            AddColumn(
                _sections,
                "FUEL CELL");

            AddColumn(
                _sections,
                "COMMAND");

            AddColumn(
                _sections,
                "DOCK");

            AddColumn(
                _sections,
                "BRANCH ROOT");

            AddColumn(
                _sections,
                "SYMMETRY");

            for (int index = 0;
                 index < model.Sections.Count;
                 index++)
            {
                ElectricalSectionModel section =
                    model.Sections[index];

                _sections.Rows.Add(
                    section.DisplayOrder,
                    section.Name,
                    section.Key,
                    section.IsCommandSection
                        ? "COMMAND"
                        : section.IsRadialSection
                            ? "RADIAL"
                            : "CORE",
                    section.SeparationStage,
                    section.ActivationStage,
                    section.AverageY.ToString(
                        "0.000"),
                    section.PartCount,
                    section.ElectricalPartCount,
                    section.ElectricChargeAmount
                        .ToString("0.###"),
                    section.ElectricChargeCapacity
                        .ToString("0.###"),
                    section.ElectricChargePercent
                        .ToString("0.0"),
                    section.BatteryPartCount,
                    section.SolarPartCount,
                    section.GeneratorPartCount,
                    section.FuelCellPartCount,
                    section.CommandPartCount,
                    section.DockingPortCount,
                    section.BranchRootPartId,
                    section.SymmetryGroupId);
            }
        }

        private void PopulateParts(
            ElectricalTopologyModel model)
        {
            _parts.Columns.Clear();
            _parts.Rows.Clear();

            AddColumn(
                _parts,
                "PART ID");

            AddColumn(
                _parts,
                "PARENT");

            AddColumn(
                _parts,
                "TITLE");

            AddColumn(
                _parts,
                "SECTION");

            AddColumn(
                _parts,
                "ELECTRICAL ROLE");

            AddColumn(
                _parts,
                "SEP STAGE");

            AddColumn(
                _parts,
                "ACT STAGE");

            AddColumn(
                _parts,
                "DEPTH");

            AddColumn(
                _parts,
                "X");

            AddColumn(
                _parts,
                "Y");

            AddColumn(
                _parts,
                "Z");

            AddColumn(
                _parts,
                "EC AMOUNT");

            AddColumn(
                _parts,
                "EC CAP");

            AddColumn(
                _parts,
                "BRANCH ROOT");

            AddColumn(
                _parts,
                "SYMMETRY");

            AddColumn(
                _parts,
                "ALL ROLES");

            for (int index = 0;
                 index < model.Parts.Count;
                 index++)
            {
                ElectricalPartModel part =
                    model.Parts[index];

                if (!part.IsElectricalPart)
                {
                    continue;
                }

                _parts.Rows.Add(
                    part.PartId,
                    part.HasParent
                        ? part.ParentPartId
                        : 0,
                    part.Title,
                    part.SectionKey,
                    part.ElectricalRole,
                    part.SeparationStage,
                    part.ActivationStage,
                    part.StructuralDepth,
                    part.VesselX.ToString(
                        "0.000"),
                    part.VesselY.ToString(
                        "0.000"),
                    part.VesselZ.ToString(
                        "0.000"),
                    part.ElectricChargeAmount
                        .ToString("0.###"),
                    part.ElectricChargeCapacity
                        .ToString("0.###"),
                    part.BranchRootPartId,
                    part.SymmetryGroupId,
                    part.Roles);
            }
        }

        private void PopulateRawTopology(
            VesselTopology topology,
            ElectricalTopologyModel model)
        {
            _rawTopology.Columns.Clear();
            _rawTopology.Rows.Clear();

            AddColumn(
                _rawTopology,
                "PART ID");

            AddColumn(
                _rawTopology,
                "PARENT");

            AddColumn(
                _rawTopology,
                "TITLE");

            AddColumn(
                _rawTopology,
                "CATEGORY");

            AddColumn(
                _rawTopology,
                "ROLES");

            AddColumn(
                _rawTopology,
                "ATTACHMENT");

            AddColumn(
                _rawTopology,
                "SEP STAGE");

            AddColumn(
                _rawTopology,
                "BOUNDARY");

            AddColumn(
                _rawTopology,
                "NEXT SEP");

            AddColumn(
                _rawTopology,
                "DEPTH");

            AddColumn(
                _rawTopology,
                "BRANCH ROOT");

            AddColumn(
                _rawTopology,
                "SYMMETRY");

            AddColumn(
                _rawTopology,
                "X");

            AddColumn(
                _rawTopology,
                "Y");

            AddColumn(
                _rawTopology,
                "Z");

            AddColumn(
                _rawTopology,
                "CANDIDATE SECTION");

            if (topology == null)
            {
                return;
            }

            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (node == null)
                {
                    continue;
                }

                string section =
                    FindSectionKey(
                        model,
                        node.PartId);

                _rawTopology.Rows.Add(
                    node.PartId,
                    node.HasParent
                        ? node.ParentPartId
                        : 0,
                    node.PartTitle,
                    node.Category,
                    node.Roles,
                    node.AttachmentType,
                    node.SeparationStage,
                    node.IsSeparationBoundary,
                    node.WillSeparateOnNextStage,
                    node.StructuralDepth,
                    node.BranchRootPartId,
                    node.SymmetryGroupId,
                    node.VesselX.ToString(
                        "0.000"),
                    node.VesselY.ToString(
                        "0.000"),
                    node.VesselZ.ToString(
                        "0.000"),
                    section);
            }
        }

        private static string BuildRawSnapshot(
            VesselTopology topology,
            ElectricalTopologyModel model)
        {
            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine(
                BuildOverview(
                    model));

            builder.AppendLine();
            builder.AppendLine(
                "ELECTRICAL PARTS");

            for (int index = 0;
                 index < model.Parts.Count;
                 index++)
            {
                ElectricalPartModel part =
                    model.Parts[index];

                if (!part.IsElectricalPart)
                {
                    continue;
                }

                builder.Append(
                    part.PartId);

                builder.Append(
                    " | ");

                builder.Append(
                    part.SectionKey);

                builder.Append(
                    " | ");

                builder.Append(
                    part.Title);

                builder.Append(
                    " | ");

                builder.Append(
                    part.ElectricalRole);

                builder.Append(
                    " | SEP ");

                builder.Append(
                    part.SeparationStage);

                builder.Append(
                    " | POS ");

                builder.Append(
                    part.VesselX.ToString(
                        "0.000"));

                builder.Append(
                    ",");

                builder.Append(
                    part.VesselY.ToString(
                        "0.000"));

                builder.Append(
                    ",");

                builder.Append(
                    part.VesselZ.ToString(
                        "0.000"));

                builder.Append(
                    " | EC ");

                builder.Append(
                    part.ElectricChargeAmount
                        .ToString("0.###"));

                builder.Append(
                    "/");

                builder.AppendLine(
                    part.ElectricChargeCapacity
                        .ToString("0.###"));
            }

            return builder.ToString();
        }

        private void OnSaveSnapshot(
            object sender,
            EventArgs e)
        {
            using (SaveFileDialog dialog =
                new SaveFileDialog())
            {
                dialog.Filter =
                    "Text files (*.txt)|*.txt|All files (*.*)|*.*";

                dialog.FileName =
                    "kmc-electrical-topology-" +
                    DateTime.Now.ToString(
                        "yyyyMMdd-HHmmss") +
                    ".txt";

                if (dialog.ShowDialog(
                        this) ==
                    DialogResult.OK)
                {
                    File.WriteAllText(
                        dialog.FileName,
                        _rawSnapshot);
                }
            }
        }

        private static TextBox CreateTextView()
        {
            return new TextBox
            {
                Dock =
                    DockStyle.Fill,

                Multiline =
                    true,

                ReadOnly =
                    true,

                ScrollBars =
                    ScrollBars.Both,

                WordWrap =
                    false,

                BackColor =
                    Color.FromArgb(
                        2,
                        12,
                        16),

                ForeColor =
                    Color.FromArgb(
                        170,
                        255,
                        190),

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
                            2,
                            12,
                            16),

                    BorderStyle =
                        BorderStyle.None,

                    GridColor =
                        Color.FromArgb(
                            45,
                            95,
                            90),

                    ForeColor =
                        Color.FromArgb(
                            170,
                            255,
                            190),

                    Font =
                        new Font(
                            "Consolas",
                            9.0f),

                    RowHeadersVisible =
                        false
                };

            grid.EnableHeadersVisualStyles =
                false;

            grid.ColumnHeadersDefaultCellStyle
                .BackColor =
                    Color.FromArgb(
                        22,
                        40,
                        42);

            grid.ColumnHeadersDefaultCellStyle
                .ForeColor =
                    Color.FromArgb(
                        190,
                        245,
                        225);

            grid.DefaultCellStyle
                .BackColor =
                    Color.FromArgb(
                        2,
                        12,
                        16);

            grid.DefaultCellStyle
                .ForeColor =
                    Color.FromArgb(
                        170,
                        255,
                        190);

            grid.DefaultCellStyle
                .SelectionBackColor =
                    Color.FromArgb(
                        35,
                        70,
                        72);

            grid.DefaultCellStyle
                .SelectionForeColor =
                    Color.White;

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
                    4,
                    14,
                    18);

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

                BackColor =
                    Color.FromArgb(
                        18,
                        42,
                        40),

                ForeColor =
                    Color.FromArgb(
                        165,
                        255,
                        205),

                Font =
                    new Font(
                        "Consolas",
                        9.0f,
                        FontStyle.Bold)
            };
        }

        private static void AddColumn(
            DataGridView grid,
            string title)
        {
            grid.Columns.Add(
                title,
                title);
        }

        private static void Append(
            StringBuilder builder,
            string label,
            string value)
        {
            builder.Append(
                label.PadRight(
                    28));

            builder.Append(
                ": ");

            builder.AppendLine(
                value);
        }

        private static string FormatStage(
            int stage)
        {
            return
                stage >= 0
                    ? stage.ToString("00")
                    : "--";
        }

        private static string FindSectionKey(
            ElectricalTopologyModel model,
            uint partId)
        {
            for (int index = 0;
                 index < model.Parts.Count;
                 index++)
            {
                ElectricalPartModel part =
                    model.Parts[index];

                if (part.PartId ==
                    partId)
                {
                    return
                        part.SectionKey;
                }
            }

            return "--";
        }
    }
}
