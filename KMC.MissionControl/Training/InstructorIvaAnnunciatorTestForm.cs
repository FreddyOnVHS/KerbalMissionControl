using System;
using System.Drawing;
using System.Windows.Forms;
using KMC.Shared;

namespace KMC.MissionControl.Training
{
    public sealed class InstructorIvaAnnunciatorTestForm : Form
    {
        private readonly Label _status;

        public InstructorIvaAnnunciatorTestForm()
        {
            Text = "KMC - IVA Annunciator Tests";
            ClientSize = new Size(650, 565);
            MinimumSize = new Size(590, 515);
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
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50.0f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38.0f));

            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "IVA ANNUNCIATOR TEST INPUTS\nTest only — telemetry and failure truth are not changed.",
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

            Button clear = CreateButton("CLEAR ALL IVA TESTS");
            clear.Click += delegate
            {
                Send(IvaAnnunciatorTestId.Warp, IvaAnnunciatorTestOperation.ClearAll);
            };
            root.Controls.Add(clear, 0, 2);

            _status = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Text = "READY",
                ForeColor = ForeColor
            };
            root.Controls.Add(_status, 0, 3);
            Controls.Add(root);
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
    }
}
