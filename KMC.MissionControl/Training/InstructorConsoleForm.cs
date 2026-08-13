using System;
using System.Drawing;
using System.Windows.Forms;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Themes;

namespace KMC.MissionControl.Training
{
    public sealed class InstructorConsoleForm : Form
    {
        private readonly MissionControlReceiver _receiver;
        private readonly Timer _refreshTimer;

        private readonly Label _vesselLabel;
        private readonly Label _modeLabel;
        private readonly Label _statusLabel;
        private readonly ComboBox _failureSelector;
        private readonly NumericUpDown _delaySeconds;
        private readonly ComboBox _scenarioSelector;
        private readonly ListView _failureList;

        public InstructorConsoleForm(
            MissionControlReceiver receiver)
        {
            if (receiver == null)
            {
                throw new ArgumentNullException(
                    nameof(receiver));
            }

            _receiver = receiver;

            Text =
                "KMC - Instructor Console / Build 14.9";

            ClientSize =
                new Size(
                    1120,
                    720);

            MinimumSize =
                new Size(
                    900,
                    620);

            StartPosition =
                FormStartPosition.CenterParent;

            BackColor =
                ApolloTheme.WindowBackground;

            ForeColor =
                Color.FromArgb(
                    190,
                    255,
                    190);

            Font =
                new Font(
                    "Consolas",
                    10.0f,
                    FontStyle.Regular);

            FormBorderStyle =
                FormBorderStyle.SizableToolWindow;

            _vesselLabel =
                CreateStatusLabel(
                    "VESSEL  --");

            _modeLabel =
                CreateStatusLabel(
                    "MODE  --");

            _statusLabel =
                CreateStatusLabel(
                    "READY");

            _failureSelector =
                CreateFailureSelector();

            _delaySeconds =
                CreateDelayControl();

            _scenarioSelector =
                CreateScenarioSelector();

            _failureList =
                CreateFailureList();

            Controls.Add(
                BuildRootLayout());

            _refreshTimer =
                new Timer
                {
                    Interval = 500
                };

            _refreshTimer.Tick +=
                OnRefreshTimerTick;

            Shown +=
                OnShown;

            FormClosed +=
                OnFormClosed;
        }

        private Control BuildRootLayout()
        {
            TableLayoutPanel root =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding =
                        new Padding(12),
                    Margin =
                        Padding.Empty,
                    BackColor =
                        ApolloTheme.WindowBackground,
                    ColumnCount = 1,
                    RowCount = 6
                };

            root.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100.0f));

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    58.0f));

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    88.0f));

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    88.0f));

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100.0f));

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    60.0f));

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    34.0f));

            root.Controls.Add(
                BuildHeader(),
                0,
                0);

            root.Controls.Add(
                BuildFailureCommandPanel(),
                0,
                1);

            root.Controls.Add(
                BuildScenarioPanel(),
                0,
                2);

            root.Controls.Add(
                _failureList,
                0,
                3);

            root.Controls.Add(
                BuildActionPanel(),
                0,
                4);

            root.Controls.Add(
                _statusLabel,
                0,
                5);

            return root;
        }

        private Control BuildHeader()
        {
            TableLayoutPanel panel =
                new TableLayoutPanel
                {
                    Dock =
                        DockStyle.Fill,
                    Margin =
                        Padding.Empty,
                    ColumnCount = 3,
                    RowCount = 1,
                    BackColor =
                        ApolloTheme.WindowBackground
                };

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    52.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    24.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    24.0f));

            Label title =
                new Label
                {
                    Text =
                        "INSTRUCTOR / SCENARIO CONTROL\nBUILD 14.9 — EXPLICIT COMMANDS ONLY",
                    Dock =
                        DockStyle.Fill,
                    TextAlign =
                        ContentAlignment.MiddleLeft,
                    ForeColor =
                        Color.FromArgb(
                            210,
                            255,
                            210),
                    Font =
                        new Font(
                            "Consolas",
                            11.0f,
                            FontStyle.Bold)
                };

            _vesselLabel.TextAlign =
                ContentAlignment.MiddleLeft;

            _modeLabel.TextAlign =
                ContentAlignment.MiddleLeft;

            panel.Controls.Add(
                title,
                0,
                0);

            panel.Controls.Add(
                _vesselLabel,
                1,
                0);

            panel.Controls.Add(
                _modeLabel,
                2,
                0);

            return panel;
        }

        private Control BuildFailureCommandPanel()
        {
            GroupBox box =
                CreateGroupBox(
                    "SINGLE FAILURE INJECTION");

            TableLayoutPanel panel =
                new TableLayoutPanel
                {
                    Dock =
                        DockStyle.Fill,
                    Margin =
                        Padding.Empty,
                    Padding =
                        new Padding(
                            8,
                            2,
                            8,
                            8),
                    ColumnCount = 5,
                    RowCount = 1
                };

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    92.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    88.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    130.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    130.0f));

            panel.Controls.Add(
                _failureSelector,
                0,
                0);

            Label delayLabel =
                new Label
                {
                    Text =
                        "DELAY SEC",
                    Dock =
                        DockStyle.Fill,
                    TextAlign =
                        ContentAlignment.MiddleCenter,
                    ForeColor =
                        ForeColor
                };

            panel.Controls.Add(
                delayLabel,
                1,
                0);

            panel.Controls.Add(
                _delaySeconds,
                2,
                0);

            Button training =
                CreateButton(
                    "TRAINING",
                    OnTrainingModeClick);

            Button inject =
                CreateButton(
                    "INJECT",
                    OnInjectClick);

            panel.Controls.Add(
                training,
                3,
                0);

            panel.Controls.Add(
                inject,
                4,
                0);

            box.Controls.Add(
                panel);

            return box;
        }

        private Control BuildScenarioPanel()
        {
            GroupBox box =
                CreateGroupBox(
                    "PREDEFINED SCENARIO");

            TableLayoutPanel panel =
                new TableLayoutPanel
                {
                    Dock =
                        DockStyle.Fill,
                    Margin =
                        Padding.Empty,
                    Padding =
                        new Padding(
                            8,
                            2,
                            8,
                            8),
                    ColumnCount = 3,
                    RowCount = 1
                };

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    150.0f));

            panel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    150.0f));

            panel.Controls.Add(
                _scenarioSelector,
                0,
                0);

            panel.Controls.Add(
                CreateButton(
                    "SCENARIO MODE",
                    OnScenarioModeClick),
                1,
                0);

            panel.Controls.Add(
                CreateButton(
                    "START SCENARIO",
                    OnStartScenarioClick),
                2,
                0);

            box.Controls.Add(
                panel);

            return box;
        }

        private Control BuildActionPanel()
        {
            TableLayoutPanel panel =
                new TableLayoutPanel
                {
                    Dock =
                        DockStyle.Fill,
                    Margin =
                        new Padding(
                            0,
                            8,
                            0,
                            0),
                    ColumnCount = 4,
                    RowCount = 1,
                    BackColor =
                        ApolloTheme.WindowBackground
                };

            for (int index = 0;
                 index < 4;
                 index++)
            {
                panel.ColumnStyles.Add(
                    new ColumnStyle(
                        SizeType.Percent,
                        25.0f));
            }

            panel.Controls.Add(
                CreateButton(
                    "CLEAR SELECTED",
                    OnClearSelectedClick),
                0,
                0);

            panel.Controls.Add(
                CreateButton(
                    "CLEAR ALL",
                    OnClearAllClick),
                1,
                0);

            panel.Controls.Add(
                CreateButton(
                    "RESET NOMINAL",
                    OnResetNominalClick),
                2,
                0);

            panel.Controls.Add(
                CreateButton(
                    "REFRESH",
                    OnRefreshClick),
                3,
                0);

            return panel;
        }

        private static Label CreateStatusLabel(
            string text)
        {
            return
                new Label
                {
                    Text = text,
                    Dock =
                        DockStyle.Fill,
                    TextAlign =
                        ContentAlignment.MiddleLeft,
                    AutoEllipsis =
                        true
                };
        }

        private ComboBox CreateFailureSelector()
        {
            ComboBox selector =
                new ComboBox
                {
                    Dock =
                        DockStyle.Fill,
                    DropDownStyle =
                        ComboBoxStyle.DropDownList,
                    BackColor =
                        Color.FromArgb(
                            35,
                            45,
                            40),
                    ForeColor =
                        ForeColor,
                    Font =
                        new Font(
                            "Consolas",
                            9.0f,
                            FontStyle.Bold)
                };

            Array values =
                Enum.GetValues(
                    typeof(
                        InstructorFailurePreset));

            for (int index = 0;
                 index < values.Length;
                 index++)
            {
                InstructorFailurePreset preset =
                    (InstructorFailurePreset)
                    values.GetValue(index);

                selector.Items.Add(
                    new EnumChoice<InstructorFailurePreset>(
                        preset,
                        InstructorTrainingText.GetFailurePresetName(
                            preset)));
            }

            if (selector.Items.Count > 0)
            {
                selector.SelectedIndex = 0;
            }

            return selector;
        }

        private NumericUpDown CreateDelayControl()
        {
            return
                new NumericUpDown
                {
                    Dock =
                        DockStyle.Fill,
                    Minimum = 0,
                    Maximum = 300,
                    Increment = 5,
                    Value = 0,
                    DecimalPlaces = 0,
                    BackColor =
                        Color.FromArgb(
                            35,
                            45,
                            40),
                    ForeColor =
                        ForeColor,
                    TextAlign =
                        HorizontalAlignment.Center
                };
        }

        private ComboBox CreateScenarioSelector()
        {
            ComboBox selector =
                new ComboBox
                {
                    Dock =
                        DockStyle.Fill,
                    DropDownStyle =
                        ComboBoxStyle.DropDownList,
                    BackColor =
                        Color.FromArgb(
                            35,
                            45,
                            40),
                    ForeColor =
                        ForeColor,
                    Font =
                        new Font(
                            "Consolas",
                            9.0f,
                            FontStyle.Bold)
                };

            Array values =
                Enum.GetValues(
                    typeof(
                        InstructorScenarioPreset));

            for (int index = 0;
                 index < values.Length;
                 index++)
            {
                InstructorScenarioPreset preset =
                    (InstructorScenarioPreset)
                    values.GetValue(index);

                selector.Items.Add(
                    new EnumChoice<InstructorScenarioPreset>(
                        preset,
                        InstructorTrainingText.GetScenarioName(
                            preset)));
            }

            if (selector.Items.Count > 0)
            {
                selector.SelectedIndex = 0;
            }

            return selector;
        }

        private ListView CreateFailureList()
        {
            ListView list =
                new ListView
                {
                    Dock =
                        DockStyle.Fill,
                    Margin =
                        new Padding(
                            0,
                            8,
                            0,
                            0),
                    View =
                        View.Details,
                    FullRowSelect =
                        true,
                    MultiSelect =
                        false,
                    HideSelection =
                        false,
                    BackColor =
                        Color.FromArgb(
                            23,
                            31,
                            27),
                    ForeColor =
                        ForeColor,
                    BorderStyle =
                        BorderStyle.FixedSingle
                };

            list.Columns.Add(
                "FAILURE ID",
                155);

            list.Columns.Add(
                "TARGET",
                330);

            list.Columns.Add(
                "KIND",
                105);

            list.Columns.Add(
                "SEVERITY",
                100);

            list.Columns.Add(
                "STATE",
                115);

            list.Columns.Add(
                "TIMING",
                160);

            return list;
        }

        private GroupBox CreateGroupBox(
            string title)
        {
            return
                new GroupBox
                {
                    Text = title,
                    Dock =
                        DockStyle.Fill,
                    ForeColor =
                        ForeColor,
                    BackColor =
                        ApolloTheme.WindowBackground,
                    Font =
                        new Font(
                            "Consolas",
                            9.0f,
                            FontStyle.Bold)
                };
        }

        private Button CreateButton(
            string text,
            EventHandler handler)
        {
            Button button =
                new Button
                {
                    Text = text,
                    Dock =
                        DockStyle.Fill,
                    Margin =
                        new Padding(5),
                    FlatStyle =
                        FlatStyle.Flat,
                    BackColor =
                        Color.FromArgb(
                            45,
                            55,
                            49),
                    ForeColor =
                        ForeColor,
                    Font =
                        new Font(
                            "Consolas",
                            9.0f,
                            FontStyle.Bold),
                    TabStop =
                        false
                };

            button.FlatAppearance.BorderColor =
                Color.FromArgb(
                    120,
                    150,
                    125);

            button.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(
                    58,
                    70,
                    62);

            button.Click +=
                handler;

            return button;
        }

        private void OnShown(
            object sender,
            EventArgs e)
        {
            RefreshSnapshot();
            _refreshTimer.Start();
        }

        private void OnFormClosed(
            object sender,
            FormClosedEventArgs e)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        }

        private void OnRefreshTimerTick(
            object sender,
            EventArgs e)
        {
            RefreshSnapshot();
        }

        private void OnRefreshClick(
            object sender,
            EventArgs e)
        {
            RefreshSnapshot();
        }

        private void OnTrainingModeClick(
            object sender,
            EventArgs e)
        {
            string result;

            bool success =
                _receiver.SetInstructorFailureMode(
                    FailureSimulationMode.Training,
                    out result);

            SetCommandResult(
                success,
                result);
        }

        private void OnScenarioModeClick(
            object sender,
            EventArgs e)
        {
            string result;

            bool success =
                _receiver.SetInstructorFailureMode(
                    FailureSimulationMode.Scenario,
                    out result);

            SetCommandResult(
                success,
                result);
        }

        private void OnInjectClick(
            object sender,
            EventArgs e)
        {
            EnumChoice<InstructorFailurePreset> choice =
                _failureSelector.SelectedItem as
                EnumChoice<InstructorFailurePreset>;

            if (choice == null)
            {
                SetCommandResult(
                    false,
                    "NO FAILURE PRESET SELECTED");

                return;
            }

            string failureId;
            string result;

            bool success =
                _receiver.InjectInstructorFailure(
                    choice.Value,
                    (double)_delaySeconds.Value,
                    out failureId,
                    out result);

            SetCommandResult(
                success,
                result);
        }

        private void OnStartScenarioClick(
            object sender,
            EventArgs e)
        {
            EnumChoice<InstructorScenarioPreset> choice =
                _scenarioSelector.SelectedItem as
                EnumChoice<InstructorScenarioPreset>;

            if (choice == null)
            {
                SetCommandResult(
                    false,
                    "NO SCENARIO SELECTED");

                return;
            }

            string result;

            bool success =
                _receiver.StartInstructorScenario(
                    choice.Value,
                    out result);

            SetCommandResult(
                success,
                result);
        }

        private void OnClearSelectedClick(
            object sender,
            EventArgs e)
        {
            if (_failureList.SelectedItems.Count == 0)
            {
                SetCommandResult(
                    false,
                    "SELECT A FAILURE FIRST");

                return;
            }

            string failureId =
                Convert.ToString(
                    _failureList.SelectedItems[0].Tag);

            string result;

            bool success =
                _receiver.ClearInstructorFailure(
                    failureId,
                    out result);

            SetCommandResult(
                success,
                result);
        }

        private void OnClearAllClick(
            object sender,
            EventArgs e)
        {
            string result;

            bool success =
                _receiver.ClearAllInstructorFailures(
                    out result);

            SetCommandResult(
                success,
                result);
        }

        private void OnResetNominalClick(
            object sender,
            EventArgs e)
        {
            DialogResult confirm =
                MessageBox.Show(
                    this,
                    "Clear every non-cleared synthetic failure for the active vessel and return failure mode to NOMINAL?",
                    "Reset Failure Simulation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

            if (confirm !=
                DialogResult.Yes)
            {
                return;
            }

            string result;

            bool success =
                _receiver.ResetInstructorNominal(
                    out result);

            SetCommandResult(
                success,
                result);
        }

        private void SetCommandResult(
            bool success,
            string result)
        {
            _statusLabel.Text =
                (success
                    ? "ACK  "
                    : "REJECT  ") +
                (result ??
                 string.Empty);

            RefreshSnapshot();
        }

        private void RefreshSnapshot()
        {
            string vesselId;
            string vesselName;
            FailureSimulationSnapshot snapshot;
            string result;

            if (!_receiver.TryGetInstructorFailureSnapshot(
                    out vesselId,
                    out vesselName,
                    out snapshot,
                    out result))
            {
                _vesselLabel.Text =
                    "VESSEL  --";

                _modeLabel.Text =
                    "MODE  --";

                _failureList.Items.Clear();

                if (!string.IsNullOrWhiteSpace(
                        result))
                {
                    _statusLabel.Text =
                        result;
                }

                return;
            }

            _vesselLabel.Text =
                "VESSEL  " +
                (string.IsNullOrWhiteSpace(
                     vesselName)
                    ? vesselId
                    : vesselName);

            _modeLabel.Text =
                "MODE  " +
                snapshot.Mode.ToString().ToUpperInvariant() +
                " / ACTIVE " +
                snapshot.ActiveFailureCount.ToString();

            string selectedFailureId =
                _failureList.SelectedItems.Count > 0
                    ? Convert.ToString(
                        _failureList.SelectedItems[0].Tag)
                    : string.Empty;

            _failureList.BeginUpdate();

            try
            {
                _failureList.Items.Clear();

                DateTime now =
                    DateTime.UtcNow;

                for (int index = 0;
                     index < snapshot.Failures.Count;
                     index++)
                {
                    SyntheticFailureRecord failure =
                        snapshot.Failures[index];

                    if (failure == null ||
                        failure.Condition ==
                            SyntheticFailureCondition.Cleared)
                    {
                        continue;
                    }

                    string timing;

                    if (failure.EffectiveNow)
                    {
                        timing = "ACTIVE NOW";
                    }
                    else
                    {
                        double seconds =
                            (failure.ActivateUtc -
                             now).TotalSeconds;

                        timing =
                            seconds > 0.0
                                ? "T+" +
                                  Math.Ceiling(
                                      seconds).ToString("0") +
                                  " SEC"
                                : "ARMED";
                    }

                    ListViewItem item =
                        new ListViewItem(
                            failure.FailureId ??
                            string.Empty);

                    item.SubItems.Add(
                        failure.TargetId ??
                        string.Empty);

                    item.SubItems.Add(
                        failure.Kind.ToString().
                            ToUpperInvariant());

                    item.SubItems.Add(
                        failure.Severity.ToString().
                            ToUpperInvariant());

                    item.SubItems.Add(
                        failure.EffectiveNow
                            ? "ACTIVE"
                            : failure.Condition.ToString().
                                ToUpperInvariant());

                    item.SubItems.Add(
                        timing);

                    item.Tag =
                        failure.FailureId;

                    _failureList.Items.Add(
                        item);

                    if (!string.IsNullOrWhiteSpace(
                            selectedFailureId) &&
                        string.Equals(
                            selectedFailureId,
                            failure.FailureId,
                            StringComparison.Ordinal))
                    {
                        item.Selected = true;
                    }
                }
            }
            finally
            {
                _failureList.EndUpdate();
            }
        }

        private sealed class EnumChoice<T>
            where T : struct
        {
            public EnumChoice(
                T value,
                string text)
            {
                Value = value;
                Text = text ?? string.Empty;
            }

            public T Value { get; private set; }
            public string Text { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }
    }
}
