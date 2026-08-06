using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
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

            Button snapshot = CreateButton("SNAPSHOT");
            snapshot.Click += delegate { SaveSnapshot(); };
            commands.Controls.Add(snapshot);

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

        private void SaveSnapshot()
        {
            VesselTopology topology =
                PropulsionDebugSnapshotStore.GetTopology();

            VesselCapabilitySnapshot snapshot =
                VesselCapabilityBuilder.Build(topology);

            string safeVesselName =
                MakeSafeFileName(
                    string.IsNullOrWhiteSpace(snapshot.VesselName)
                        ? "NoVessel"
                        : snapshot.VesselName);

            using (SaveFileDialog dialog =
                new SaveFileDialog())
            {
                dialog.Title =
                    "Save KMC Capability Snapshot";

                dialog.Filter =
                    "Text files (*.txt)|*.txt|All files (*.*)|*.*";

                dialog.DefaultExt =
                    "txt";

                dialog.AddExtension =
                    true;

                dialog.FileName =
                    "KMC_CapabilitySnapshot_" +
                    safeVesselName +
                    "_" +
                    DateTime.Now.ToString(
                        "yyyyMMdd_HHmmss") +
                    ".txt";

                if (dialog.ShowDialog(this) !=
                    DialogResult.OK)
                {
                    return;
                }

                try
                {
                    File.WriteAllText(
                        dialog.FileName,
                        BuildSnapshotText(snapshot),
                        new UTF8Encoding(
                            false));

                    MessageBox.Show(
                        this,
                        "Snapshot saved successfully." +
                        Environment.NewLine +
                        Environment.NewLine +
                        dialog.FileName,
                        "KMC Capability Debugger",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        this,
                        "The snapshot could not be saved." +
                        Environment.NewLine +
                        Environment.NewLine +
                        exception.Message,
                        "KMC Capability Debugger",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private static string BuildSnapshotText(
            VesselCapabilitySnapshot snapshot)
        {
            StringBuilder text =
                new StringBuilder();

            text.AppendLine(
                "KMC CAPABILITY COMPATIBILITY SNAPSHOT");

            text.AppendLine(
                "Generated UTC\t" +
                DateTime.UtcNow.ToString(
                    "yyyy-MM-dd HH:mm:ss"));

            text.AppendLine(
                "Generated Local\t" +
                DateTime.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss"));

            text.AppendLine(
                "KMC Version\t" +
                GetKmcVersion());

            text.AppendLine(
                "Plugin Version\t" +
                GetPluginVersion());

            text.AppendLine();


            AppendSectionHeader(
                text,
                "OVERVIEW");

            AppendSnapshotValue(
                text,
                "Vessel",
                snapshot.VesselName);

            AppendSnapshotValue(
                text,
                "Topology revision",
                snapshot.TopologyRevision.ToString());

            AppendSnapshotValue(
                text,
                "Current stage",
                snapshot.CurrentStage.ToString());

            AppendSnapshotValue(
                text,
                "Part count",
                snapshot.Parts.Count.ToString());

            AppendSnapshotValue(
                text,
                "Capability count",
                snapshot.Parts.Sum(
                    part =>
                        part.Capabilities.Count)
                    .ToString());

            AppendSnapshotValue(
                text,
                "Unknown resource count",
                snapshot.UnknownResources.Count.ToString());

            text.AppendLine();

            AppendSectionHeader(
                text,
                "UNKNOWN RESOURCES");

            if (snapshot.UnknownResources.Count == 0)
            {
                text.AppendLine("None");
            }
            else
            {
                for (int index = 0;
                     index < snapshot.UnknownResources.Count;
                     index++)
                {
                    text.AppendLine(
                        CleanCell(
                            snapshot.UnknownResources[index]));
                }
            }

            text.AppendLine();

            AppendSectionHeader(
                text,
                "VESSEL DIAGNOSTICS");

            if (snapshot.Diagnostics.Count == 0)
            {
                text.AppendLine("None");
            }
            else
            {
                for (int index = 0;
                     index < snapshot.Diagnostics.Count;
                     index++)
                {
                    text.AppendLine(
                        CleanCell(
                            snapshot.Diagnostics[index]));
                }
            }

            text.AppendLine();

            AppendSectionHeader(
                text,
                "PARTS");

            text.AppendLine(
                "PART ID\tPARENT ID\tHAS PARENT\tTITLE\tNAME\tSEP\tACT\tCAPABILITY COUNT\tRESOURCE COUNT\tDIAGNOSTICS");

            for (int index = 0;
                 index < snapshot.Parts.Count;
                 index++)
            {
                PartCapabilitySnapshot part =
                    snapshot.Parts[index];

                AppendRow(
                    text,
                    part.PartId,
                    part.ParentPartId,
                    part.HasParent,
                    part.PartTitle,
                    part.PartName,
                    part.SeparationStage,
                    part.ActivationStage,
                    part.Capabilities.Count,
                    part.Resources.Count,
                    string.Join(
                        " | ",
                        part.Diagnostics.ToArray()));
            }

            text.AppendLine();

            AppendSectionHeader(
                text,
                "CAPABILITIES");

            text.AppendLine(
                "PART ID\tPART\tTYPE\tSUBTYPE\tSOURCE\tCONFIDENCE\tDESCRIPTION");

            for (int partIndex = 0;
                 partIndex < snapshot.Parts.Count;
                 partIndex++)
            {
                PartCapabilitySnapshot part =
                    snapshot.Parts[partIndex];

                for (int index = 0;
                     index < part.Capabilities.Count;
                     index++)
                {
                    PartCapability capability =
                        part.Capabilities[index];

                    AppendRow(
                        text,
                        part.PartId,
                        part.PartTitle,
                        capability.Type,
                        capability.Subtype,
                        capability.Source,
                        capability.Confidence,
                        capability.Description);
                }
            }

            text.AppendLine();

            AppendSectionHeader(
                text,
                "RESOURCES");

            text.AppendLine(
                "PART ID\tPART\tRESOURCE\tDISPLAY\tCATEGORY\tKNOWN\tSTORED\tCONSUMED\tAMOUNT\tCAPACITY\tRATIO");

            for (int partIndex = 0;
                 partIndex < snapshot.Parts.Count;
                 partIndex++)
            {
                PartCapabilitySnapshot part =
                    snapshot.Parts[partIndex];

                for (int index = 0;
                     index < part.Resources.Count;
                     index++)
                {
                    ResourceDescriptor resource =
                        part.Resources[index];

                    AppendRow(
                        text,
                        part.PartId,
                        part.PartTitle,
                        resource.InternalName,
                        resource.DisplayName,
                        resource.Category,
                        resource.IsKnown,
                        resource.IsStored,
                        resource.IsConsumed,
                        resource.Amount.ToString(
                            "0.###"),
                        resource.Capacity.ToString(
                            "0.###"),
                        resource.RequiredRatio.ToString(
                            "0.###"));
                }
            }

            return text.ToString();
        }

        private static string GetKmcVersion()
        {
            Version version =
                Assembly.GetExecutingAssembly()
                    .GetName()
                    .Version;

            return version != null
                ? version.ToString()
                : "UNKNOWN";
        }

        private static string GetPluginVersion()
        {
            /*
             * The plugin does not currently transmit its runtime assembly
             * version to Mission Control. This value matches the current
             * KMC.Plugin AssemblyVersion in source and is intentionally
             * labeled as expected rather than runtime-verified.
             */
            return "1.0.0.0 (EXPECTED; NOT TELEMETRY-VERIFIED)";
        }

        private static void AppendSectionHeader(
            StringBuilder text,
            string title)
        {
            text.AppendLine(
                "============================================================");

            text.AppendLine(title);

            text.AppendLine(
                "============================================================");
        }

        private static void AppendSnapshotValue(
            StringBuilder text,
            string label,
            string value)
        {
            text.Append(
                CleanCell(label));

            text.Append('\t');

            text.AppendLine(
                CleanCell(
                    string.IsNullOrEmpty(value)
                        ? "--"
                        : value));
        }

        private static void AppendRow(
            StringBuilder text,
            params object[] values)
        {
            for (int index = 0;
                 index < values.Length;
                 index++)
            {
                if (index > 0)
                {
                    text.Append('\t');
                }

                text.Append(
                    CleanCell(
                        values[index] == null
                            ? string.Empty
                            : values[index].ToString()));
            }

            text.AppendLine();
        }

        private static string CleanCell(
            string value)
        {
            return (value ?? string.Empty)
                .Replace(
                    "\r",
                    " ")
                .Replace(
                    "\n",
                    " ")
                .Replace(
                    "\t",
                    " ");
        }

        private static string MakeSafeFileName(
            string value)
        {
            char[] invalid =
                Path.GetInvalidFileNameChars();

            StringBuilder result =
                new StringBuilder(
                    value.Length);

            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                char character =
                    value[index];

                result.Append(
                    invalid.Contains(character)
                        ? '_'
                        : character);
            }

            return result.ToString();
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
