using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Themes;

namespace KMC.MissionControl.Training
{
    /// <summary>
    /// Build 14.11.1A stability recovery.
    ///
    /// IMPORTANT:
    /// - The F10 UI never calls live Engine snapshot APIs on the WinForms UI thread.
    /// - Display state comes from EngineeringSnapshotStore only.
    /// - Instructor mutations run on a worker thread with exception containment.
    /// - There is no periodic F10 timer.
    ///
    /// This prevents instructor work from starving the MainForm message loop,
    /// which also owns the Integrated Systems C/W WinForms timer.
    /// </summary>
    public sealed class InstructorConsoleForm : Form
    {
        private readonly MissionControlReceiver _receiver;

        private readonly Label _vesselLabel;
        private readonly Label _modeLabel;
        private readonly Label _statusLabel;
        private readonly ComboBox _failureSelector;
        private readonly NumericUpDown _delaySeconds;
        private readonly ComboBox _scenarioSelector;
        private readonly ListView _failureList;

        private bool _commandRunning;

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
                "KMC - Instructor Console / Stability Recovery";

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
                    "READY / DISPLAY FROM PUBLISHED SNAPSHOT");

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

            Shown +=
                OnShown;
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
                        "INSTRUCTOR / SCENARIO CONTROL\n14.11.3B — ELECTRICAL SOURCE TESTS",
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

            panel.Controls.Add(
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
                },
                1,
                0);

            panel.Controls.Add(
                _delaySeconds,
                2,
                0);

            panel.Controls.Add(
                CreateButton(
                    "TRAINING",
                    OnTrainingModeClick),
                3,
                0);

            panel.Controls.Add(
                CreateButton(
                    "INJECT",
                    OnInjectClick),
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
            RefreshPublishedSnapshot();
        }

        private void OnRefreshClick(
            object sender,
            EventArgs e)
        {
            RefreshPublishedSnapshot();
        }

        private void OnTrainingModeClick(
            object sender,
            EventArgs e)
        {
            RunCommand(
                delegate
                {
                    string result;

                    bool success =
                        _receiver.SetInstructorFailureMode(
                            FailureSimulationMode.Training,
                            out result);

                    return
                        new CommandResult(
                            success,
                            result);
                });
        }

        private void OnScenarioModeClick(
            object sender,
            EventArgs e)
        {
            RunCommand(
                delegate
                {
                    string result;

                    bool success =
                        _receiver.SetInstructorFailureMode(
                            FailureSimulationMode.Scenario,
                            out result);

                    return
                        new CommandResult(
                            success,
                            result);
                });
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
                SetStatus(
                    false,
                    "NO FAILURE PRESET SELECTED");

                return;
            }

            InstructorFailurePreset preset =
                choice.Value;

            double delay =
                (double)_delaySeconds.Value;

            RunCommand(
                delegate
                {
                    string failureId;
                    string result;

                    bool success;

                    if (preset ==
                            InstructorFailurePreset.GeneratorA ||
                        preset ==
                            InstructorFailurePreset.GeneratorB ||
                        preset ==
                            InstructorFailurePreset.GeneratorADegraded50)
                    {
                        string sourceId =
                            preset ==
                                InstructorFailurePreset.GeneratorB
                                ? "SRC_GEN_B"
                                : "SRC_GEN_A";

                        SpacecraftSystemHealth sourceHealth =
                            preset ==
                                InstructorFailurePreset.GeneratorADegraded50
                                ? SpacecraftSystemHealth.Degraded
                                : SpacecraftSystemHealth.Failed;

                        success =
                            InstructorElectricalSourceFailureBridge
                                .InjectGeneratorFailure(
                                    _receiver,
                                    sourceId,
                                    sourceHealth,
                                    delay,
                                    out failureId,
                                    out result);
                    }
                    else if (
                        preset ==
                            InstructorFailurePreset.GenAContactorFailedOpen ||
                        preset ==
                            InstructorFailurePreset.MainATransferFailedOpen ||
                        preset ==
                            InstructorFailurePreset.GuidABreakerTripped ||
                        preset ==
                            InstructorFailurePreset.GenAContactorFalseOpenIndication ||
                        preset ==
                            InstructorFailurePreset.GenAContactorWeldedClosed)
                    {
                        string switchId;
                        SyntheticElectricalSwitchFailureMode switchMode;

                        switch (preset)
                        {
                            case InstructorFailurePreset.GenAContactorFailedOpen:
                                switchId = "CONT_GEN_A";
                                switchMode =
                                    SyntheticElectricalSwitchFailureMode.FailedOpen;
                                break;

                            case InstructorFailurePreset.MainATransferFailedOpen:
                                switchId = "XFER_MAIN_A";
                                switchMode =
                                    SyntheticElectricalSwitchFailureMode.FailedOpen;
                                break;

                            case InstructorFailurePreset.GuidABreakerTripped:
                                switchId = "BRK_GUID_A";
                                switchMode =
                                    SyntheticElectricalSwitchFailureMode.TrippedOpen;
                                break;

                            case InstructorFailurePreset.GenAContactorFalseOpenIndication:
                                switchId = "CONT_GEN_A";
                                switchMode =
                                    SyntheticElectricalSwitchFailureMode.FalseOpenIndication;
                                break;

                            default:
                                switchId = "CONT_GEN_A";
                                switchMode =
                                    SyntheticElectricalSwitchFailureMode.WeldedClosed;
                                break;
                        }

                        success =
                            InstructorElectricalSourceFailureBridge
                                .InjectSwitchFailure(
                                    _receiver,
                                    switchId,
                                    switchMode,
                                    delay,
                                    out failureId,
                                    out result);
                    }
                    else if (
                        preset ==
                            InstructorFailurePreset.EngineFeedValveClosed)
                    {
                        success =
                            InstructorPropulsionFeedFailureBridge
                                .InjectExactEngineFeedPathFailure(
                                    _receiver,
                                    delay,
                                    out failureId,
                                    out result);
                    }
                    else
                    {
                        success =
                            _receiver.InjectInstructorFailure(
                                preset,
                                delay,
                                out failureId,
                                out result);
                    }

                    return
                        new CommandResult(
                            success,
                            result);
                });
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
                SetStatus(
                    false,
                    "NO SCENARIO SELECTED");

                return;
            }

            InstructorScenarioPreset scenario =
                choice.Value;

            RunCommand(
                delegate
                {
                    string result;

                    bool success =
                        _receiver.StartInstructorScenario(
                            scenario,
                            out result);

                    return
                        new CommandResult(
                            success,
                            result);
                });
        }

        private void OnClearSelectedClick(
            object sender,
            EventArgs e)
        {
            if (_failureList.SelectedItems.Count == 0)
            {
                SetStatus(
                    false,
                    "SELECT A FAILURE FIRST");

                return;
            }

            string failureId =
                Convert.ToString(
                    _failureList.SelectedItems[0].Tag);

            RunCommand(
                delegate
                {
                    string result;

                    bool success =
                        _receiver.ClearInstructorFailure(
                            failureId,
                            out result);

                    return
                        new CommandResult(
                            success,
                            result);
                });
        }

        private void OnClearAllClick(
            object sender,
            EventArgs e)
        {
            /*
             * Clear All is explicitly exception-contained on the worker.
             * A failure-engine or integration exception is reported to the
             * instructor status line instead of escaping through WinForms.
             */
            RunCommand(
                delegate
                {
                    string result;

                    bool success =
                        _receiver.ClearAllInstructorFailures(
                            out result);

                    return
                        new CommandResult(
                            success,
                            result);
                });
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

            RunCommand(
                delegate
                {
                    string result;

                    bool success =
                        _receiver.ResetInstructorNominal(
                            out result);

                    return
                        new CommandResult(
                            success,
                            result);
                });
        }

        private async void RunCommand(
            Func<CommandResult> command)
        {
            if (_commandRunning)
            {
                SetStatus(
                    false,
                    "COMMAND ALREADY RUNNING");

                return;
            }

            _commandRunning =
                true;

            SetCommandControlsEnabled(
                false);

            _statusLabel.Text =
                "WORKING / UI REMAINS LIVE";

            CommandResult result;

            try
            {
                result =
                    await Task.Run(
                        delegate
                        {
                            try
                            {
                                return
                                    command != null
                                        ? command()
                                        : new CommandResult(
                                            false,
                                            "NO COMMAND");
                            }
                            catch (Exception ex)
                            {
                                return
                                    new CommandResult(
                                        false,
                                        "COMMAND EXCEPTION / " +
                                        ex.GetType().Name +
                                        " / " +
                                        ex.Message);
                            }
                        });
            }
            catch (Exception ex)
            {
                result =
                    new CommandResult(
                        false,
                        "WORKER EXCEPTION / " +
                        ex.GetType().Name +
                        " / " +
                        ex.Message);
            }
            finally
            {
                _commandRunning =
                    false;

                SetCommandControlsEnabled(
                    true);
            }

            SetStatus(
                result.Success,
                result.Text);

            /*
             * Read only the last published engineering snapshot. No direct
             * failure-engine query occurs on the UI thread.
             */
            RefreshPublishedSnapshot();
        }

        private void SetCommandControlsEnabled(
            bool enabled)
        {
            _failureSelector.Enabled =
                enabled;

            _delaySeconds.Enabled =
                enabled;

            _scenarioSelector.Enabled =
                enabled;

            /*
             * Leave REFRESH/list/window painting alive. Disable only buttons
             * whose click would issue another command.
             */
            foreach (Control control in Controls)
            {
                SetCommandButtonsEnabledRecursive(
                    control,
                    enabled);
            }
        }

        private static void SetCommandButtonsEnabledRecursive(
            Control control,
            bool enabled)
        {
            if (control == null)
            {
                return;
            }

            Button button =
                control as Button;

            if (button != null)
            {
                if (!string.Equals(
                        button.Text,
                        "REFRESH",
                        StringComparison.Ordinal))
                {
                    button.Enabled =
                        enabled;
                }
            }

            foreach (Control child in control.Controls)
            {
                SetCommandButtonsEnabledRecursive(
                    child,
                    enabled);
            }
        }

        private void RefreshPublishedSnapshot()
        {
            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore.TryGetLatest(
                    out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.Vessel == null ||
                result.Snapshot.SpacecraftSystems == null ||
                result.Snapshot.SpacecraftSystems
                    .FailureSimulation == null)
            {
                _vesselLabel.Text =
                    "VESSEL  --";

                _modeLabel.Text =
                    "MODE  --";

                _failureList.Items.Clear();

                return;
            }

            string vesselId =
                result.Snapshot.Vessel.VesselId;

            string vesselName =
                result.Snapshot.Vessel.VesselName ??
                string.Empty;

            FailureSimulationSnapshot snapshot =
                result.Snapshot.SpacecraftSystems
                    .FailureSimulation;

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

            RebuildFailureList(
                snapshot);
        }

        private void RebuildFailureList(
            FailureSimulationSnapshot snapshot)
        {
            string selectedFailureId =
                _failureList.SelectedItems.Count > 0
                    ? Convert.ToString(
                        _failureList.SelectedItems[0].Tag)
                    : string.Empty;

            _failureList.BeginUpdate();

            try
            {
                _failureList.Items.Clear();

                if (snapshot == null)
                {
                    return;
                }

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
                        timing =
                            "ACTIVE NOW";
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
                        item.Selected =
                            true;
                    }
                }
            }
            finally
            {
                _failureList.EndUpdate();
            }
        }

        private void SetStatus(
            bool success,
            string result)
        {
            _statusLabel.Text =
                (success
                    ? "ACK  "
                    : "REJECT  ") +
                (result ??
                 string.Empty);
        }

        private sealed class CommandResult
        {
            public CommandResult(
                bool success,
                string text)
            {
                Success =
                    success;

                Text =
                    text ?? string.Empty;
            }

            public bool Success
            {
                get;
                private set;
            }

            public string Text
            {
                get;
                private set;
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
