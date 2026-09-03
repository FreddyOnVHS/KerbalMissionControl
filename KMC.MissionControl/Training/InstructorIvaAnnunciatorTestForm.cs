using System;
using System.Drawing;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.Models;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Engineering;
using KMC.Shared;

namespace KMC.MissionControl.Training
{
    public sealed class InstructorIvaAnnunciatorTestForm : Form
    {
        private readonly Label _status;

        public InstructorIvaAnnunciatorTestForm()
        {
            Text = "KMC - IVA / System Tests";
            ClientSize = new Size(760, 830);
            MinimumSize = new Size(680, 720);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            BackColor = Color.FromArgb(18, 24, 21);
            ForeColor = Color.FromArgb(190, 255, 190);
            Font = new Font("Consolas", 9.0f, FontStyle.Regular);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 6
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38.0f));

            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text =
                    "IVA ANNUNCIATOR + SYSTEM AUTHORITY TEST INPUTS\n" +
                    "Authority rows change KMC state; KSP only executes leased consequences.",
                ForeColor = Color.FromArgb(220, 255, 220),
                Font = new Font("Consolas", 10.0f, FontStyle.Bold)
            }, 0, 0);

            TableLayoutPanel tests = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 8,
                Margin = Padding.Empty
            };
            tests.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
            tests.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100.0f));
            tests.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100.0f));
            tests.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90.0f));

            AddTestRow(tests, 0, "WARP", "MAIN A", IvaAnnunciatorTestId.Warp);
            AddTestRow(tests, 1, "MECO", "MAIN B", IvaAnnunciatorTestId.Meco);
            AddTestRow(tests, 2, "ENGINE FLAME OUT", "ESS", IvaAnnunciatorTestId.EngineFailure);
            AddTestRow(tests, 3, "ENGINE OVERHEAT", "ESS", IvaAnnunciatorTestId.EngineOverheat);
            AddTestRow(tests, 4, "LOW TWR", "MAIN A", IvaAnnunciatorTestId.LowTwr);
            AddTestRow(tests, 5, "HIGH SLOPE", "ESS", IvaAnnunciatorTestId.HighSlope);
            AddTestRow(tests, 6, "GROUND PROX", "ESS", IvaAnnunciatorTestId.GroundProximity);
            AddTestRow(tests, 7, "LANDING GEAR", "MAIN B", IvaAnnunciatorTestId.LandingGear);
            root.Controls.Add(tests, 0, 1);

            root.Controls.Add(
                BuildRcsAuthorityPanel(),
                0,
                2);

            root.Controls.Add(
                BuildSystemAuthorityPanel(),
                0,
                3);

            Button clear = CreateButton("CLEAR ALL IVA TESTS + RESTORE ALL AUTHORITY");
            clear.Click += delegate
            {
                Send(
                    IvaAnnunciatorTestId.Warp,
                    IvaAnnunciatorTestOperation.ClearAll);

                SetRcsAuthority(
                    false,
                    false);

                RestoreAllSystemAuthority(
                    false);
            };
            root.Controls.Add(clear, 0, 4);

            _status = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Text = "READY",
                ForeColor = ForeColor
            };
            root.Controls.Add(_status, 0, 5);
            Controls.Add(root);
        }

        private Control BuildRcsAuthorityPanel()
        {
            GroupBox box =
                new GroupBox
                {
                    Text = "RCS AUTHORITY / BUILD 14.18.7",
                    Dock = DockStyle.Fill,
                    ForeColor = ForeColor,
                    BackColor = BackColor
                };

            TableLayoutPanel panel =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 3,
                    RowCount = 1,
                    Padding = new Padding(4)
                };

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    145.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    145.0f));

            panel.Controls.Add(
                new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign =
                        ContentAlignment.MiddleLeft,
                    Text =
                        "VESSEL-WIDE RCS HARDWARE AUTHORITY"
                },
                0,
                0);

            Button inhibit =
                CreateButton(
                    "INHIBIT RCS");

            inhibit.Click +=
                delegate
                {
                    SetRcsAuthority(
                        true,
                        true);
                };

            panel.Controls.Add(
                inhibit,
                1,
                0);

            Button restore =
                CreateButton(
                    "RESTORE RCS");

            restore.Click +=
                delegate
                {
                    SetRcsAuthority(
                        false,
                        true);
                };

            panel.Controls.Add(
                restore,
                2,
                0);

            box.Controls.Add(
                panel);

            return box;
        }

        private Control BuildSystemAuthorityPanel()
        {
            GroupBox box =
                new GroupBox
                {
                    Text = "SYSTEM COMMAND AUTHORITY / BUILD 14.19.1",
                    Dock = DockStyle.Fill,
                    ForeColor = ForeColor,
                    BackColor = BackColor
                };

            TableLayoutPanel panel =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 3,
                    RowCount = 4,
                    Padding = new Padding(4)
                };

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    145.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    145.0f));

            AddSystemAuthorityRow(
                panel,
                0,
                "SAS / AUTOPILOT AUTHORITY",
                SystemAuthorityKind.Sas);

            AddSystemAuthorityRow(
                panel,
                1,
                "LANDING GEAR ACTUATION",
                SystemAuthorityKind.Gear);

            AddSystemAuthorityRow(
                panel,
                2,
                "WHEEL BRAKE AUTHORITY",
                SystemAuthorityKind.Brakes);

            AddSystemAuthorityRow(
                panel,
                3,
                "EXTERNAL LIGHT OUTPUT",
                SystemAuthorityKind.Lights);

            box.Controls.Add(panel);
            return box;
        }

        private void AddSystemAuthorityRow(
            TableLayoutPanel panel,
            int row,
            string label,
            SystemAuthorityKind authority)
        {
            panel.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    25.0f));

            panel.Controls.Add(
                new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign =
                        ContentAlignment.MiddleLeft,
                    Text = label
                },
                0,
                row);

            Button inhibit =
                CreateButton("INHIBIT");

            inhibit.Click +=
                delegate
                {
                    SetSystemAuthority(
                        authority,
                        true,
                        true);
                };

            panel.Controls.Add(
                inhibit,
                1,
                row);

            Button restore =
                CreateButton("RESTORE");

            restore.Click +=
                delegate
                {
                    SetSystemAuthority(
                        authority,
                        false,
                        true);
                };

            panel.Controls.Add(
                restore,
                2,
                row);
        }

        private void AddTestRow(
            TableLayoutPanel panel,
            int row,
            string label,
            string bus,
            IvaAnnunciatorTestId testId)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5f));

            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = label,
                Font = new Font("Consolas", 9.0f, FontStyle.Bold)
            }, 0, row);

            Button on = CreateButton("TEST ON");
            on.Click += delegate { Send(testId, IvaAnnunciatorTestOperation.On); };
            panel.Controls.Add(on, 1, row);

            Button off = CreateButton("TEST OFF");
            off.Click += delegate { Send(testId, IvaAnnunciatorTestOperation.Off); };
            panel.Controls.Add(off, 2, row);

            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = bus,
                ForeColor = Color.FromArgb(160, 210, 180)
            }, 3, row);
        }

        private static Button CreateButton(string text)
        {
            Button button = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 55, 49),
                ForeColor = Color.FromArgb(190, 255, 190),
                Font = new Font("Consolas", 9.0f, FontStyle.Bold),
                TabStop = false
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(120, 150, 125);
            return button;
        }

        private void Send(
            IvaAnnunciatorTestId testId,
            IvaAnnunciatorTestOperation operation)
        {
            string result;
            bool success =
                InstructorIvaAnnunciatorTestBridge.Send(
                    testId,
                    operation,
                    out result);

            // This transport has no return ACK; do not claim that KSP received it.
            _status.Text = (success ? "SENT  " : "REJECT  ") + result;
        }

        private void SetRcsAuthority(
            bool inhibit,
            bool writeStatus)
        {
            AnalysisPipelineResult latest;

            if (!EngineeringSnapshotStore
                    .TryGetLatest(
                        out latest) ||
                latest == null ||
                latest.Snapshot == null ||
                latest.Snapshot.Vessel == null ||
                latest.Snapshot.Capabilities == null ||
                string.IsNullOrWhiteSpace(
                    latest.Snapshot.Vessel.VesselId))
            {
                if (writeStatus)
                {
                    _status.Text =
                        "REJECT  NO ACTIVE ENGINEERING VESSEL";
                }

                return;
            }

            int count =
                latest.Snapshot.Capabilities
                    .GetPartCount(
                        VesselCapabilityType
                            .ReactionControl);

            if (inhibit &&
                count <= 0)
            {
                if (writeStatus)
                {
                    _status.Text =
                        "REJECT  NO RCS HARDWARE DETECTED";
                }

                return;
            }

            string vesselId =
                latest.Snapshot.Vessel.VesselId;

            RcsAuthorityStore
                .SetInstructorInhibit(
                    vesselId,
                    inhibit);

            if (writeStatus)
            {
                _status.Text =
                    inhibit
                        ? "KMC STATE  RCS AUTHORITY INHIBITED / AWAIT LEASE CYCLE"
                        : "KMC STATE  RCS AUTHORITY RESTORED / AWAIT LEASE CYCLE";
            }
        }
        private void SetSystemAuthority(
            SystemAuthorityKind authority,
            bool inhibit,
            bool writeStatus)
        {
            AnalysisPipelineResult latest;

            if (!EngineeringSnapshotStore
                    .TryGetLatest(
                        out latest) ||
                latest == null ||
                latest.Snapshot == null ||
                latest.Snapshot.Vessel == null ||
                string.IsNullOrWhiteSpace(
                    latest.Snapshot.Vessel.VesselId))
            {
                if (writeStatus)
                {
                    _status.Text =
                        "REJECT  NO ACTIVE ENGINEERING VESSEL";
                }

                return;
            }

            string vesselId =
                latest.Snapshot.Vessel.VesselId;

            SystemAuthorityStore
                .SetInstructorInhibit(
                    vesselId,
                    authority,
                    inhibit);

            if (writeStatus)
            {
                _status.Text =
                    "KMC STATE  " +
                    authority.ToString().ToUpperInvariant() +
                    " AUTHORITY " +
                    (inhibit
                        ? "INHIBITED"
                        : "RESTORED") +
                    " / AWAIT LEASE CYCLE";
            }
        }

        private void RestoreAllSystemAuthority(
            bool writeStatus)
        {
            AnalysisPipelineResult latest;

            if (!EngineeringSnapshotStore
                    .TryGetLatest(
                        out latest) ||
                latest == null ||
                latest.Snapshot == null ||
                latest.Snapshot.Vessel == null ||
                string.IsNullOrWhiteSpace(
                    latest.Snapshot.Vessel.VesselId))
            {
                return;
            }

            string vesselId =
                latest.Snapshot.Vessel.VesselId;

            SystemAuthorityStore.RestoreAll(
                vesselId);

            if (writeStatus)
            {
                _status.Text =
                    "KMC STATE  ALL SYSTEM AUTHORITY RESTORED / AWAIT LEASE CYCLE";
            }
        }

    }
}
