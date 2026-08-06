using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using KMC.MissionControl.Capabilities;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Debugging.Capabilities
{
    public sealed class CapabilityDebuggerForm :
        Form
    {
        private readonly TextBox _overview;
        private readonly DataGridView _parts;
        private readonly DataGridView _capabilities;
        private readonly DataGridView _resources;
        private readonly Timer _timer;

        public CapabilityDebuggerForm()
        {
            Text = "KMC Capability Compatibility Debugger";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1420, 860);
            MinimumSize = new Size(980, 640);
            BackColor = Color.FromArgb(4, 14, 18);
            ForeColor = Color.FromArgb(170, 255, 190);
            Font = new Font("Consolas", 10.0f);
            KeyPreview = true;

            TabControl tabs =
                new TabControl
                {
                    Dock = DockStyle.Fill
                };

            _overview = CreateTextView();
            _parts = CreateGrid();
            _capabilities = CreateGrid();
            _resources = CreateGrid();

            tabs.TabPages.Add(CreatePage("OVERVIEW", _overview));
            tabs.TabPages.Add(CreatePage("PARTS", _parts));
            tabs.TabPages.Add(CreatePage("CAPABILITIES", _capabilities));
            tabs.TabPages.Add(CreatePage("RESOURCES", _resources));

            FlowLayoutPanel commands =
                new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = 42,
                    Padding = new Padding(6),
                    BackColor = Color.FromArgb(24, 34, 34)
                };

            Button refresh = CreateButton("REFRESH");
            refresh.Click += delegate { RefreshSnapshot(); };
            commands.Controls.Add(refresh);

            Controls.Add(tabs);
            Controls.Add(commands);

            _timer = new Timer { Interval = 750 };
            _timer.Tick += delegate { RefreshSnapshot(); };
            _timer.Start();

            FormClosed += delegate
            {
                _timer.Stop();
                _timer.Dispose();
            };

            KeyDown += delegate(
                object sender,
                KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Close();
                }
            };
        }

        public void RefreshSnapshot()
        {
            VesselTopology topology =
                PropulsionDebugSnapshotStore.GetTopology();

            VesselCapabilitySnapshot snapshot =
                VesselCapabilityBuilder.Build(topology);

            PopulateOverview(snapshot);
            PopulateParts(snapshot);
            PopulateCapabilities(snapshot);
            PopulateResources(snapshot);
        }

        private void PopulateOverview(
            VesselCapabilitySnapshot snapshot)
        {
            StringBuilder text = new StringBuilder();

            text.AppendLine("KMC CAPABILITY COMPATIBILITY DEBUGGER");
            text.AppendLine();
            Append(text, "Vessel", snapshot.VesselName);
            Append(text, "Topology revision", snapshot.TopologyRevision.ToString());
            Append(text, "Current stage", snapshot.CurrentStage.ToString());
            Append(text, "Part count", snapshot.Parts.Count.ToString());
            Append(
                text,
                "Capability count",
                snapshot.Parts.Sum(
                    part => part.Capabilities.Count)
                    .ToString());

            Append(
                text,
                "Unknown resources",
                snapshot.UnknownResources.Count.ToString());

            text.AppendLine();
            text.AppendLine("UNKNOWN RESOURCES");

            if (snapshot.UnknownResources.Count == 0)
            {
                text.AppendLine("- None");
            }
            else
            {
                for (int i = 0;
                     i < snapshot.UnknownResources.Count;
                     i++)
                {
                    text.Append("- ");
                    text.AppendLine(snapshot.UnknownResources[i]);
                }
            }

            text.AppendLine();
            text.AppendLine("PHASE 1 DIAGNOSTICS");

            for (int i = 0;
                 i < snapshot.Diagnostics.Count;
                 i++)
            {
                text.Append("- ");
                text.AppendLine(snapshot.Diagnostics[i]);
            }

            _overview.Text = text.ToString();
        }

        private void PopulateParts(
            VesselCapabilitySnapshot snapshot)
        {
            ResetGrid(
                _parts,
                "PART ID", "TITLE", "NAME", "SEP", "ACT",
                "CAPABILITIES", "RESOURCES", "DIAGNOSTICS");

            for (int i = 0; i < snapshot.Parts.Count; i++)
            {
                PartCapabilitySnapshot part = snapshot.Parts[i];

                _parts.Rows.Add(
                    part.PartId,
                    part.PartTitle,
                    part.PartName,
                    part.SeparationStage,
                    part.ActivationStage,
                    part.Capabilities.Count,
                    part.Resources.Count,
                    string.Join(" | ", part.Diagnostics.ToArray()));
            }
        }

        private void PopulateCapabilities(
            VesselCapabilitySnapshot snapshot)
        {
            ResetGrid(
                _capabilities,
                "PART ID", "PART", "TYPE", "SUBTYPE",
                "SOURCE", "CONFIDENCE", "DESCRIPTION");

            for (int p = 0; p < snapshot.Parts.Count; p++)
            {
                PartCapabilitySnapshot part = snapshot.Parts[p];

                for (int i = 0; i < part.Capabilities.Count; i++)
                {
                    PartCapability capability = part.Capabilities[i];

                    _capabilities.Rows.Add(
                        part.PartId,
                        part.PartTitle,
                        capability.Type,
                        capability.Subtype,
                        capability.Source,
                        capability.Confidence,
                        capability.Description);
                }
            }
        }

        private void PopulateResources(
            VesselCapabilitySnapshot snapshot)
        {
            ResetGrid(
                _resources,
                "PART ID", "PART", "RESOURCE", "DISPLAY", "CATEGORY",
                "KNOWN", "STORED", "CONSUMED", "AMOUNT", "CAPACITY", "RATIO");

            for (int p = 0; p < snapshot.Parts.Count; p++)
            {
                PartCapabilitySnapshot part = snapshot.Parts[p];

                for (int i = 0; i < part.Resources.Count; i++)
                {
                    ResourceDescriptor resource = part.Resources[i];

                    _resources.Rows.Add(
                        part.PartId,
                        part.PartTitle,
                        resource.InternalName,
                        resource.DisplayName,
                        resource.Category,
                        resource.IsKnown,
                        resource.IsStored,
                        resource.IsConsumed,
                        resource.Amount.ToString("0.###"),
                        resource.Capacity.ToString("0.###"),
                        resource.RequiredRatio.ToString("0.###"));
                }
            }
        }

        private static void ResetGrid(
            DataGridView grid,
            params string[] columns)
        {
            grid.Columns.Clear();
            grid.Rows.Clear();

            for (int i = 0; i < columns.Length; i++)
            {
                grid.Columns.Add(columns[i], columns[i]);
            }
        }

        private static TextBox CreateTextView()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = Color.FromArgb(2, 12, 16),
                ForeColor = Color.FromArgb(170, 255, 190),
                Font = new Font("Consolas", 10.0f)
            };
        }

        private static DataGridView CreateGrid()
        {
            DataGridView grid =
                new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AutoSizeColumnsMode =
                        DataGridViewAutoSizeColumnsMode.DisplayedCells,
                    BackgroundColor = Color.FromArgb(2, 12, 16),
                    BorderStyle = BorderStyle.None,
                    GridColor = Color.FromArgb(45, 95, 90),
                    RowHeadersVisible = false,
                    Font = new Font("Consolas", 9.0f)
                };

            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor =
                Color.FromArgb(22, 40, 42);
            grid.ColumnHeadersDefaultCellStyle.ForeColor =
                Color.FromArgb(190, 245, 225);
            grid.DefaultCellStyle.BackColor =
                Color.FromArgb(2, 12, 16);
            grid.DefaultCellStyle.ForeColor =
                Color.FromArgb(170, 255, 190);
            grid.DefaultCellStyle.SelectionBackColor =
                Color.FromArgb(35, 70, 72);
            grid.DefaultCellStyle.SelectionForeColor =
                Color.White;

            return grid;
        }

        private static TabPage CreatePage(
            string title,
            Control control)
        {
            TabPage page =
                new TabPage(title)
                {
                    BackColor = Color.FromArgb(4, 14, 18)
                };

            page.Controls.Add(control);
            return page;
        }

        private static Button CreateButton(string text)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(18, 42, 40),
                ForeColor = Color.FromArgb(165, 255, 205),
                Font = new Font(
                    "Consolas",
                    9.0f,
                    FontStyle.Bold)
            };
        }

        private static void Append(
            StringBuilder text,
            string label,
            string value)
        {
            text.Append(label.PadRight(26));
            text.Append(": ");
            text.AppendLine(
                string.IsNullOrEmpty(value)
                    ? "--"
                    : value);
        }
    }
}
